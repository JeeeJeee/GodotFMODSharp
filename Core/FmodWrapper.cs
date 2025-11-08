using System;
using System.Linq;
using FMOD.Studio;
using FMOD;
using Godot;

namespace GodotFMODSharp
{
    // For convenience when iterating over different types
    public interface IFmodPath
    {
        string GetPath();
    }

    public struct FmodListener
    {
        public Node Node;
        public float Weight;
    }

    public enum FmodSpeakerMode
    {
        DEFAULT,
        RAW,
        MONO,
        STEREO,
        QUAD,
        SURROUND,
        _5POINT1,
        _7POINT1,
        _7POINT1POINT4,
        MAX,
    }
    
    public enum FmodLoadBankFlags
    {
        NORMAL,
        NONBLOCKING,
        DECOMPRESS_SAMPLE,
        UNENCRYPTED
    }
    
    /// <summary>
    /// Mirrors Fmods internal PLAYBACKSTATE enum. Just created here so users don't have to include the core files
    /// </summary>
    public enum FmodPlaybackState
    {
        PLAYING,
        SUSTAINING,
        STOPPED,
        STARTING,
        STOPPING,
    }

    public enum FmodStopMode
    {
        ALLOWFADEOUT,
        IMMIDIATE
    }
    
    public struct Fmod3DAttributes
    {
        public Fmod3DAttributes(Node3D node)
        {
            SetFromNode(node);
        }

        public Fmod3DAttributes(Node2D node)
        {
            SetFromNode(node);
        }

        public Fmod3DAttributes(Node node)
        {
            if (node is Node3D node3D)
            {
                SetFromNode(node3D);
            }

            if (node is Node2D node2D)
            {
                SetFromNode(node2D);
            }
        }

        public Fmod3DAttributes(ATTRIBUTES_3D attributes)
        {
            Position = new Vector3(attributes.position.x, attributes.position.y, -attributes.position.z);
            Velocity = new Vector3(attributes.velocity.x, attributes.velocity.y, -attributes.velocity.z);
            Forward = new Vector3(attributes.forward.x, attributes.forward.y, -attributes.forward.z);
            Up = new Vector3(attributes.up.x, attributes.up.y, -attributes.up.z);
        }

        public Vector3 Position;
        public Vector3 Velocity;
        public Vector3 Forward;
        public Vector3 Up;

        public void Update(Node3D node)
        {
            SetFromNode(node);
        }

        private void SetFromNode(Node3D node)
        {
            Position = node.GlobalPosition;
            Velocity = Vector3.Zero;
            Forward = -node.GlobalTransform.Basis.Z.Normalized();
            Up = node.GlobalTransform.Basis.Y.Normalized();
        }

        private void SetFromNode(Node2D node)
        {
            Position = new Vector3(node.Position.X, node.Position.Y, 0);
            Velocity = Vector3.Zero;
            Forward = Vector3.Forward;
            Up = Vector3.Up;
        }

        public ATTRIBUTES_3D ToFmodAttributes()
        {
            VECTOR ToFmodVector(ref Vector3 v)
            {
                var result = new VECTOR();
                result.x = v.X;
                result.y = v.Y;
                result.z = -v.Z; // FMOD uses a right-handed coordinate system so flip the Z axis
                return result;
            }

            return new ATTRIBUTES_3D
            {
                position = ToFmodVector(ref Position),
                velocity = ToFmodVector(ref Velocity),
                forward = ToFmodVector(ref Forward),
                up = ToFmodVector(ref Up)
            };
        }
    }

    public static class FmodGuidUtils
    {
        /// <summary>
        /// Converts an FMOD.GUID (four ints) to a string in standard GUID format.
        /// </summary>
        public static string ToStringExact(this GUID guid)
        {
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data1), 0, bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data2), 0, bytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data3), 0, bytes, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data4), 0, bytes, 12, 4);

            // System.Guid takes care of endianness internally
            Guid systemGuid = new Guid(bytes);
            return systemGuid.ToString("D").ToUpperInvariant();
        }

        /// <summary>
        /// Parses a standard GUID string into an FMOD.GUID (four ints).
        /// </summary>
        public static GUID FromString(this string guidString)
        {
            if (string.IsNullOrWhiteSpace(guidString))
            {
                throw new ArgumentNullException(nameof(guidString));
            }

            Guid systemGuid = Guid.Parse(guidString);
            byte[] bytes = systemGuid.ToByteArray();

            return new GUID
            {
                Data1 = BitConverter.ToInt32(bytes, 0),
                Data2 = BitConverter.ToInt32(bytes, 4),
                Data3 = BitConverter.ToInt32(bytes, 8),
                Data4 = BitConverter.ToInt32(bytes, 12)
            };
        }

        /// <summary>
        /// Converts an FMOD.GUID (four ints) to a System.Guid.
        /// </summary>
        public static Guid ToSystemGuid(this GUID guid)
        {
            byte[] bytes = new byte[16];
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data1), 0, bytes, 0, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data2), 0, bytes, 4, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data3), 0, bytes, 8, 4);
            Buffer.BlockCopy(BitConverter.GetBytes(guid.Data4), 0, bytes, 12, 4);
            return new Guid(bytes);
        }

        /// <summary>
        /// Converts a System.Guid to an FMOD.GUID (four ints).
        /// </summary>
        public static GUID FromSystemGuid(this Guid guid)
        {
            byte[] bytes = guid.ToByteArray();
            return new GUID
            {
                Data1 = BitConverter.ToInt32(bytes, 0),
                Data2 = BitConverter.ToInt32(bytes, 4),
                Data3 = BitConverter.ToInt32(bytes, 8),
                Data4 = BitConverter.ToInt32(bytes, 12)
            };
        }
    }

    public class FmodStudioSystem
    {
        private FMOD.Studio.System _handle;
        private FmodSystem _fmodCoreSystem; // <- Can you have more than one core system??? Probably not?

        internal FmodStudioSystem(ref FMOD.Studio.System handle)
        {
            _handle = handle;
            _handle.getCoreSystem(out FMOD.System coreSystem);
            _fmodCoreSystem = new FmodSystem(ref coreSystem);
        }

        public void Update()
        {
            _handle.update();
        }

        public bool GetBank(string path, out Bank outBank)
        {
            var result = _handle.loadBankFile(path, LOAD_BANK_FLAGS.NORMAL, out FMOD.Studio.Bank bank);
            outBank = new Bank(ref bank);
            return result == RESULT.OK;
        }

        public Bank LoadBankFile(string path, FmodLoadBankFlags flags = FmodLoadBankFlags.NORMAL)
        {
            if (FmodCache.IsBankLoaded(path, out Bank loadedBank)) { return loadedBank;}

            var result = _handle.loadBankFile(path, (LOAD_BANK_FLAGS) flags, out FMOD.Studio.Bank bank);
            if(result != RESULT.OK) { GD.PrintErr("FMOD: " + result + " . Failed to load bank file: " + path); }
            Bank newBank = new Bank(ref bank);
            FmodCache.AddBank(path, newBank);
            return newBank;
        }

        public Bank[] GetBanks()
        {
            return FmodCache.GetLoadedBanks();
            // var result = _handle.getBankList(out FMOD.Studio.Bank[] banks);
            // return banks.Select(b => new Bank(ref b)).ToArray();
        }

        public FmodSystem GetCoreSystem()
        {
            return _fmodCoreSystem;
        }

        public EventDescription GetEventByPath(string path)
        {
            var result = _handle.getEvent(path, out FMOD.Studio.EventDescription eventDescription);
            return result == RESULT.OK ? new EventDescription(ref eventDescription) : null;
        }

        public bool GetEventByPath(string path, out EventDescription eventDescription)
        {
            var result = _handle.getEvent(path, out FMOD.Studio.EventDescription _eventDescription);
            eventDescription = new EventDescription(ref _eventDescription);
            return result == RESULT.OK;
        }

        public bool GetEventByGuid(string guid, out EventDescription eventDescription)
        {
            var result = _handle.getEventByID(guid.FromString(), out FMOD.Studio.EventDescription _eventDescription);
            if (result != RESULT.OK)
            {
                GD.PrintErr("FMOD: Failed to get event by guid: " + guid);
                eventDescription = null;
                return false;
            }

            eventDescription = new EventDescription(ref _eventDescription);
            return result == RESULT.OK;
        }

        public void SetListenerWeight(int index, float weight) => _handle.setListenerWeight(index, weight);

        public float GetListenerWeight(int index)
        {
            _handle.getListenerWeight(index, out float weight);
            return weight;
        }

        public void SetNumListeners(int numListeners) => _handle.setNumListeners(numListeners);

        public int GetNumListeners()
        {
            _handle.getNumListeners(out int numListeners);
            return numListeners;
        }

        public void AddListener(Node node, float weight = 1.0f)
        {
            if (FmodServer.Listeners.Count >= FmodServer.MaxListenerCount)
            {
                GD.PrintErr("FMOD: Too many listeners. Max is " + FmodServer.MaxListenerCount);
                return;
            }

            FmodListener listener = new FmodListener
            {
                Node = node,
                Weight = weight
            };

            FmodServer.Listeners.Add(listener);
        }
        
        public void SetParameterByName(string paramName, float value)
        {
            var result = _handle.setParameterByName(paramName, value);
            if (result != RESULT.OK)
            {
                GD.PrintErr("FMOD: Failed to set parameter by name: " + paramName);
            }
        }

        public bool GetParameterByName(string paramName, out float value)
        {
            var result = _handle.getParameterByName(paramName, out value);
            return result == RESULT.OK;
        }
    }

    public class FmodSystem
    {
        private readonly FMOD.System _handle;
        
        internal FmodSystem(ref FMOD.System handle)
        {
            _handle = handle;
            GD.Print("FMOD: System created");
        }

        public void SetSoftwareFormat(int sampleRate, FmodSpeakerMode speakerMode, int numRawSpeakers)
        {
            _handle.setSoftwareFormat(sampleRate, (SPEAKERMODE)speakerMode, numRawSpeakers);
        }
    }

    public class Bus : IFmodPath
    {
        private FMOD.Studio.Bus _handle;

        internal Bus(ref FMOD.Studio.Bus handle)
        {
            _handle = handle;
        }

        public string GetPath()
        {
            _handle.getPath(out string path);
            return path;
        }

        public bool IsValid() => _handle.isValid();

        public bool IsPaused()
        {
            _handle.getPaused(out bool paused);
            return paused;
        }

        public void SetVolume(float volume) => _handle.setVolume(volume);
        public float GetVolume()
        {
            _handle.getVolume(out float volume);
            return volume;
        }

        public void Mute(bool muted) => _handle.setMute(muted);

        public bool IsMuted()
        {
            _handle.getMute(out bool muted);
            return muted;
        }
    }

    public class Bank : IFmodPath
    {
        private FMOD.Studio.Bank _handle;

        internal Bank(ref FMOD.Studio.Bank handle)
        {
            _handle = handle;
        }

        public Guid GetGuid()
        {
            _handle.getID(out GUID guid);
            return guid.ToSystemGuid();
        }
        
        public bool IsValid() => _handle.isValid();

        public string GetPath()
        {
            _handle.getPath(out string path);
            return path;
        }

        public int GetBusCount()
        {
            _handle.getBusCount(out int count);
            return count;
        }

        public Bus[] GetBusses()
        {
            _handle.getBusList(out FMOD.Studio.Bus[] busses);
            return busses.Select(b => new Bus(ref b)).ToArray();
        }

        public int GetVCACount()
        {
            _handle.getVCACount(out int count);
            return count;
        }

        public VCA[] GetVCAs()
        {
            _handle.getVCAList(out FMOD.Studio.VCA[] vcas);
            return vcas.Select(v => new VCA(ref v)).ToArray();
        }

        public void Unload() => _handle.unload();

        public int GetEventCount()
        {
            _handle.getEventCount(out int count);
            return count;
        }

        public EventDescription[] GetEvents()
        {
            _handle.getEventList(out FMOD.Studio.EventDescription[] events);
            return events.Select(e => new EventDescription(ref e)).ToArray();
        }
    }

    public class VCA : IFmodPath
    {
        private FMOD.Studio.VCA _handle;

        internal VCA(ref FMOD.Studio.VCA handle)
        {
            _handle = handle;
        }

        public string GetPath()
        {
            _handle.getPath(out string path);
            return path;
        }

        public bool IsValid() => _handle.isValid();

        public void SetVolume(float volume) => _handle.setVolume(volume);

        public float GetVolume()
        {
            _handle.getVolume(out float volume);
            return volume;
        }

        public void ClearHandle() => _handle.clearHandle();
    }

    public class EventDescription : IFmodPath
    {
        private FMOD.Studio.EventDescription _handle;

        internal EventDescription(ref FMOD.Studio.EventDescription handle)
        {
            if (!handle.isValid()) { throw new ArgumentException("Invalid FMOD handle passed to EventDescription"); }
            _handle = handle;
        }

        public string GetPath()
        {
            _handle.getPath(out string path);
            return path;
        }
        
        public IntPtr GetHandle() => _handle.handle;

        public int GetInstanceCount()
        {
            _handle.getInstanceCount(out int count);
            return count;
        }

        public EventInstance[] GetEventInstances()
        {
            _handle.getInstanceList(out FMOD.Studio.EventInstance[] instances);
            return instances.Select(i => new EventInstance(ref i)).ToArray();
        }

        public GUID GetGuid()
        {
            _handle.getID(out GUID guid);
            return guid;
        }

        public bool IsValid() => _handle.isValid();

        public EventInstance CreateInstance()
        {
            var result = _handle.createInstance(out FMOD.Studio.EventInstance instance);
            return new EventInstance(ref instance);
        }

        public float GetLength()
        {
            _handle.getLength(out int length);
            return length;
        }

        public bool Is3D()
        {
            _handle.is3D(out bool is3D);
            return is3D;
        }

        public (float minDistance, float maxDistance) GetMinMaxDistance()
        {
            _handle.getMinMaxDistance(out float min, out float max);
            return (min, max);
        }

        public bool IsOneshot()
        {
            _handle.isOneshot(out bool isOneshot);
            return isOneshot;
        }

        public bool IsSnapshot()
        {
            _handle.isSnapshot(out bool isSnapshot);
            return isSnapshot;
        }

        public bool IsStream()
        {
            _handle.isStream(out bool isStream);
            return isStream;
        }
    }

    public class EventInstance
    {
        private FMOD.Studio.EventInstance _handle;

        internal EventInstance(ref FMOD.Studio.EventInstance handle)
        {
            if (!handle.isValid()) { throw new ArgumentException("Invalid FMOD handle passed to EventInstance"); }
            _handle = handle;
        }

        ~EventInstance()
        {
            Release();
        }
        
        public bool IsValid() => _handle.isValid();

        public void Start()
        {
            var result = _handle.start();
            if (result != RESULT.OK)
            {
                GD.PrintErr("FMOD: Failed to start event instance: " + result);
            }
        }
        
        public IntPtr GetHandle() => _handle.handle;

        public void Stop(FmodStopMode stopMode = FmodStopMode.IMMIDIATE)
        {
            _handle.stop((STOP_MODE) stopMode);
            Release();
        }

        public EventDescription GetDescription()
        {
            var result = _handle.getDescription(out FMOD.Studio.EventDescription description);
            return result == RESULT.OK ? new EventDescription(ref description) : null;
        }

        public (float minDistance, float maxDistance) GetMinMaxDistance()
        {
            _handle.getMinMaxDistance(out float min, out float max);
            return (min, max);
        }

        public void SetPitch(float pitch) => _handle.setPitch(pitch);

        public float GetPitch()
        {
            _handle.getPitch(out float pitch);
            return pitch;
        }

        public void SetVolume(float volume) => _handle.setVolume(volume);

        public float GetVolume()
        {
            _handle.getVolume(out float volume);
            return volume;
        }

        public void Set3DAttributes(ref Fmod3DAttributes attributes)
        {
            _handle.set3DAttributes(attributes.ToFmodAttributes());
        }

        public Fmod3DAttributes Get3DAttributes()
        {
            _handle.get3DAttributes(out ATTRIBUTES_3D attributes);
            return new Fmod3DAttributes(attributes);
        }

        public void SetTimelinePosition(int position) => _handle.setTimelinePosition(position);

        public int GetTimelinePosition()
        {
            _handle.getTimelinePosition(out int position);
            return position;
        }

        public void SetPaused(bool paused) => _handle.setPaused(paused);

        public bool IsPaused()
        {
            _handle.getPaused(out bool paused);
            return paused;
        }

        public void SetReverbLevel(int index, float level) => _handle.setReverbLevel(index, level);

        public float GetReverbLevel(int index)
        {
            _handle.getReverbLevel(index, out float level);
            return level;
        }

        public bool IsVirtual()
        {
            _handle.isVirtual(out bool isVirtual);
            return isVirtual;
        }

        public FmodPlaybackState GetPlaybackState()
        {
            _handle.getPlaybackState(out PLAYBACK_STATE state);
            return (FmodPlaybackState) state;
        }

        public EventDescription GetEventDescription()
        {
            var result = _handle.getDescription(out FMOD.Studio.EventDescription description);
            return result == RESULT.OK ? new EventDescription(ref description) : null;
        }

        public void Release()
        {
            _handle.release();
            _handle.clearHandle();
        }
    }
}