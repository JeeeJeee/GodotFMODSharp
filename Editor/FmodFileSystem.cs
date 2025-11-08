using System;
using System.Collections.Concurrent;
using Godot;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using FMOD;
using Error = Godot.Error;
using Thread = System.Threading.Thread;

namespace GodotFMODSharp.Editor;

public enum FmodReadPriority
{
    NORMAL,
    HIGH
}

public class GodotFileHandle(FileAccess fileAccess)
{
    public FileAccess FileAccess = fileAccess;
}

/// <summary>
/// Performs the reading operation for fmod using Godot's filesystem
/// </summary>
public sealed partial class FmodGodotFileSystem : IDisposable
{
    public class AsyncReadRequest(ref IntPtr fmodAsyncReadInfoPtr)
    {
        // A FMOD_ASYNCREADINFO pointer
        public IntPtr FmodAsyncReadInfoPtr = fmodAsyncReadInfoPtr;

        public ASYNCREADINFO Info => Marshal.PtrToStructure<ASYNCREADINFO>(FmodAsyncReadInfoPtr);

        // The data the caller expects us to fill: buffer pointer, offset, length...
        public uint Offset;
        public uint SizeBytes;
        public int Priority;
        public IntPtr UserData;
        
        public RESULT Result;
        public readonly TaskCompletionSource<RESULT> Completion = new();

        // Cancellation flag manipulated by cancelReadRequest
        private int _cancelled;
        public bool IsCancelled => _cancelled != 0;
        public void MarkCancelled() => Interlocked.Exchange(ref _cancelled, 1);
        
        
        public void Complete(RESULT result)
        {
            Result = result;
            Completion.TrySetResult(result);
        }
    }
    
    public static FmodGodotFileSystem Instance => _instance;
    private static readonly FmodGodotFileSystem _instance = new();
    
    private readonly ConcurrentQueue<AsyncReadRequest> _highPrioQueue = new();
    private readonly ConcurrentQueue<AsyncReadRequest> _lowPrioQueue = new();
    
    private readonly AutoResetEvent _workEvent = new(false);

    private Thread _thread;
    private readonly CancellationTokenSource _cts = new();

    private readonly object _currentLock = new();
    private AsyncReadRequest _currentRequest;
    private readonly ConcurrentDictionary<IntPtr, AsyncReadRequest> _allRequests = new();
    
    private FmodGodotFileSystem(){}
    
    public void Start()
    {
        if (_thread != null && _thread.IsAlive) { return; }

        _thread = new Thread(Run)
        {
            IsBackground = true,
            Name = "FMOD Godot File System"
        };
        _thread.Start();
    }

    public void Finish()
    {
        _cts.Cancel();
        _workEvent.Set();
        _thread?.Join();
        _thread = null;
        
        while (_highPrioQueue.TryDequeue(out _)) { }
        while (_lowPrioQueue.TryDequeue(out _)) { }
    }
    
    public void Dispose()
    {
        Finish();
        _cts.Dispose();
        _workEvent.Dispose();
    }
    
    public Task<RESULT> QueueReadRequest(AsyncReadRequest request, FmodReadPriority priority)
    {
        _allRequests[request.FmodAsyncReadInfoPtr] = request;

        if (priority == FmodReadPriority.HIGH)
        {
            _highPrioQueue.Enqueue(request);   
        }
        else
        {
            _lowPrioQueue.Enqueue(request);   
        }

        // signal worker thread
        _workEvent.Set();

        // Return Task for the caller to await completion/result
        return request.Completion.Task;
    }
    
    public RESULT CancelReadRequest(IntPtr nativePtr)
    {
        if (_allRequests.TryGetValue(nativePtr, out AsyncReadRequest req))
        {
            req.MarkCancelled();

            // If it's still queued (not the current), complete it immediately as cancelled
            bool removedFromQueues = false;

            // Try to remove it from high queue => no direct remove; we will drain it when dequeued
            // To provide immediate early completion for queued requests we mark it cancelled and
            // also attempt to complete it if it hasn't started.
            lock (_currentLock)
            {
                if (!ReferenceEquals(_currentRequest, req))
                {
                    // Not currently processing — complete immediately
                    req.Complete(/* RESULT.OK or specific cancel code */ 0);
                    _allRequests.TryRemove(nativePtr, out _);
                    removedFromQueues = true;
                }
                else
                {
                    // Currently processing: processing loop should check IsCancelled and abort as soon as possible
                    // We still leave it in _allRequests until processing loop finishes.
                }
            }

            if (removedFromQueues)
                return 0; // RESULT.OK placeholder
            else
                return 0; // RESULT.OK but request is marked cancelled and will be aborted
        }
        
        return RESULT.ERR_FILE_DISKEJECTED;
    }
    
    private void Run()
    {
        var token = _cts.Token;
        try
        {
            while (!token.IsCancellationRequested)
            {
                // Try to pick next item
                AsyncReadRequest next;

                // I am not actually sure why the utopia rise implementation uses priority queuing.
                // But I didn't want to not implement it only to run into some weird issue
                if (!_highPrioQueue.TryDequeue(out next))
                {
                    if (!_lowPrioQueue.TryDequeue(out next))
                    {
                        // nothing queued: wait for signal or cancellation
                        WaitHandle.WaitAny([_workEvent, token.WaitHandle]);
                        if (token.IsCancellationRequested)
                        {
                            break;   
                        }
                        
                        continue;
                    }
                }

                if (next == null) { continue; }

                // If request was cancelled before starting, mark completed and continue
                if (next.IsCancelled)
                {
                    // Use appropriate FMOD cancellation result code as needed
                    next.Complete(/* cancel RESULT */ 0);
                    _allRequests.TryRemove(next.FmodAsyncReadInfoPtr, out _);
                    continue;
                }

                // Mark current
                lock (_currentLock)
                {
                    _currentRequest = next;
                }
                
                RESULT result;
                try
                {
                    result = PerformRead(next, token);
                }
                catch (OperationCanceledException)
                {
                    result = /* appropriate cancel result */ 0;
                }
                catch (Exception ex)
                {
                    // Map to an FMOD error code
                    Console.WriteLine($"Read failed: {ex}");
                    result = 0;
                }

                // Complete and cleanup
                next.Complete(result);
                _allRequests.TryRemove(next.FmodAsyncReadInfoPtr, out _);

                lock (_currentLock)
                {
                    _currentRequest = null;
                }
            }
        }
        finally
        {
            // Mark all remaining queued requests as failed/cancelled
            foreach (var kv in _allRequests)
            {
                AsyncReadRequest r = kv.Value;
                if (!r.Completion.Task.IsCompleted)
                {
                    r.Complete(/* code indicating runner shutdown */ 0);   
                }
            }
            _allRequests.Clear();
        }
    }
    
    private RESULT PerformRead(AsyncReadRequest request, CancellationToken token)
    {
        var asyncInfo = request.Info;
        FileAccess fileAccess = ((GodotFileHandle) GCHandle.FromIntPtr(asyncInfo.handle).Target)?.FileAccess;
        if (fileAccess == null) { return RESULT.ERR_FILE_DISKEJECTED; }
        
        fileAccess.Seek(asyncInfo.offset);
        byte[] buffer = fileAccess.GetBuffer(asyncInfo.sizebytes);
        int size = buffer.Length;
        
        Marshal.Copy(buffer, 0, asyncInfo.buffer, size);
        
        asyncInfo.bytesread = (uint)size;
        Marshal.StructureToPtr(asyncInfo, request.FmodAsyncReadInfoPtr, false);
        
        asyncInfo.done(request.FmodAsyncReadInfoPtr, RESULT.OK);
        return RESULT.OK;
    }
}

/// <summary>
/// Fmod delegate callbacks
/// </summary>
public sealed partial class FmodGodotFileSystem
{
    public RESULT FmodAsyncCancelCallback(IntPtr info, IntPtr userData)
    {
        Instance.CancelReadRequest(info);
        return RESULT.OK;
    }
    
    public RESULT FmodAsyncReadCallback(IntPtr info, IntPtr userData)
    {
        Instance.QueueReadRequest(new(ref info), FmodReadPriority.NORMAL);
        return RESULT.OK;
    }
    
    public RESULT FmodFileOpenCallback(IntPtr name, ref uint filesize, ref IntPtr handle, IntPtr userdata)
    {
        string inFilePath = Marshal.PtrToStringUTF8(name);
        FileAccess openFile = FileAccess.Open(inFilePath, FileAccess.ModeFlags.Read);
        if (openFile != null && openFile.GetError() == Error.Ok)
        {
            filesize = (uint)openFile.GetLength();
            GodotFileHandle godotFileHandle = new(openFile);
            GCHandle gch = GCHandle.Alloc(godotFileHandle, GCHandleType.Normal);
            handle = GCHandle.ToIntPtr(gch);
            return RESULT.OK;
        }
        
        return RESULT.ERR_FILE_NOTFOUND;
    }
    
    public RESULT FmodFileCloseCallback(IntPtr handle, IntPtr userdata)
    {
        if(handle == IntPtr.Zero) { return RESULT.ERR_INVALID_PARAM; }
        FileAccess fileAccess = GCHandle.FromIntPtr(handle).Target as FileAccess;
        fileAccess?.Close();
        GCHandle.FromIntPtr(handle).Free();
        return RESULT.OK;
    }
}