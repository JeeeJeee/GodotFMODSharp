using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using FMOD;
using Godot;
using GodotFMODSharp.Editor;

namespace GodotFMODSharp;
    
/// <summary>
/// Added to project autloads by the plugin.
/// Serves as the main way to interact with Fmods API
/// </summary>
[Tool]
public partial class FmodServer : Node
{
    private FMOD.System _fmodSystem;
    private FMOD.Studio.System _fmodStudioSystem;

    public static FmodStudioSystem FmodStudioSystem;
    public static FmodSystem FmodSystem;

    public static List<FmodListener> Listeners = new();
    public static readonly int MaxListenerCount = 1;
        
    private static List<EventInstance> _runningEventInstances = new();
    
    public override void _Ready()
    {
        // Load the FMOD dlls for editor play manually. This way we don't have to clutter up the project root directory with dlls.
        // An exported game will instead have the fmod dlls exported directly next to the executable
        if (OS.HasFeature("editor") || OS.HasFeature("editor_runtime") || OS.HasFeature("editor_hint"))
        {
            FmodDllLoader.LoadFmodDllsForEditor();
        }
        
        // Register the plugin settings again at runtime to make sure the default values are set 
        FmodPluginSettings.RegisterFmodSettings();
        
        InitializeFmodStudio();
    }

    private void InitializeFmodStudio()
    {
        // Start the custom filesystem for loading fmod events from godot packages
        FmodGodotFileSystem.Instance.Start();
        
        // Initialize FMOD Studio & Core
        FMOD.Studio.System.create(out _fmodStudioSystem);
        // Might have to be called after the fmodStudio initialize function? I am not certain
        _fmodStudioSystem.getCoreSystem(out _fmodSystem);
        
        // Initialize FMOD Studio
        int maxChannels = (int)ProjectSettings.GetSetting(FmodPluginSettings.MaxChannels.propertyPath);
        var result = _fmodStudioSystem.initialize(maxChannels, FMOD.Studio.INITFLAGS.NORMAL, FMOD.INITFLAGS.NORMAL, IntPtr.Zero);
        if (result != RESULT.OK)
        {
            GD.PrintErr("Failed to initialize FmodStudio: " + result);
        }
        
        // Set the FMOD file system to be compatible with Godot's filesystem
        SetFmodFileSystem();

        // Initialize the FMOD wrapper classes
        FmodStudioSystem = new FmodStudioSystem(ref _fmodStudioSystem);
        FmodSystem = FmodStudioSystem.GetCoreSystem();
        
        // Set Fmod config based on project settings
        int sampleRate = (int)ProjectSettings.GetSetting(FmodPluginSettings.SampleRate.propertyPath);
        FmodSpeakerMode speakerMode = (FmodSpeakerMode)(int)ProjectSettings.GetSetting(FmodPluginSettings.SpeakerMode.propertyPath);
        int rawSpeakerCount = (int)ProjectSettings.GetSetting(FmodPluginSettings.RawSpeakerCount.propertyPath);
        FmodSystem.SetSoftwareFormat(sampleRate, speakerMode, rawSpeakerCount);
                
        // Fmod in its own documentation states that the master banks should be loaded at the start and usually are never unloaded
        LoadMasterBanks();
#if TOOLS
        if (Engine.IsEditorHint())
        {
            // Just load every .bank file we can find in the project folder so the FmodExplorer works correctly.
            LoadAllBanksInProject();   
        }
#endif
        
        // Listeners aren't needed in the editor
        FmodStudioSystem.SetNumListeners(MaxListenerCount);
        Listeners.Capacity = MaxListenerCount;
    }

    /// <summary>
    /// Sets the Fmod file system to be compatible with Godots filesystem
    /// Basically lets us pass res:\ paths directly to Fmod and it will load the files correctly
    /// </summary>
    private void SetFmodFileSystem()
    {
        FILE_OPEN_CALLBACK fileOpenCallback = FmodGodotFileSystem.Instance.FmodFileOpenCallback;
        FILE_CLOSE_CALLBACK closeCallback = FmodGodotFileSystem.Instance.FmodFileCloseCallback;
        FILE_ASYNCREAD_CALLBACK syncRead = FmodGodotFileSystem.Instance.FmodAsyncReadCallback;
        FILE_ASYNCCANCEL_CALLBACK syncCancel = FmodGodotFileSystem.Instance.FmodAsyncCancelCallback;
        _fmodSystem.setFileSystem(fileOpenCallback, closeCallback, null, null, syncRead, syncCancel, -1);
    }

#if TOOLS
    /// <summary>
    /// Loads every bank in the project directory. EXCEPT the master banks since those are already loaded
    /// </summary>
    private void LoadAllBanksInProject()
    {
        // Filter out "Master" banks. Might be an issue if users name their banks something with "Master" in the name.
        FmodEditorHelpers.GetAllFilesAtPath("res://", ".bank")
            .Where(x => !x.Contains("Master"))
            .ToList()
            .ForEach(x => LoadBankAtPath(x));
    }
#endif

    public override void _Process(double delta)
    {
        // Updating FmodStudio also updates the core system under the hood
        if(FmodStudioSystem != null) { FmodStudioSystem.Update(); }

        // Listeners only need to be updated in actual games
        if (!Engine.IsEditorHint())
        {
            UpdateListeners();   
        }
    }

    private void LoadMasterBanks()
    {
        string banksPath = (string)ProjectSettings.GetSetting(FmodPluginSettings.BanksPath.propertyPath);
        if (string.IsNullOrEmpty(banksPath))
        {
            GD.PrintErr("Banks path is empty in the GodotFMODSharp plugin settings! The plugin wont work correctly! Set the path and restart Godot.");
            return;
        }

        string [] masterBanks = DirAccess.Open(banksPath).GetFiles().Where(x =>
        {
            if(x.EndsWith(".bank") && x.Contains("Master")) { return true; }
            return false;
        }).ToArray();
        
        foreach (var masterBank in masterBanks)
        {
            var resPath = Path.Combine(banksPath, masterBank);
            LoadBankAtPath(resPath);
        }
    }

    public static Bank LoadBankAtPath(string path, FmodLoadBankFlags flags = FmodLoadBankFlags.NORMAL)
    {
        Bank newBank = FmodStudioSystem.LoadBankFile(path, flags);
        return newBank;
    }

    private void UpdateListeners()
    {
        for (int i = 0; i < Listeners.Count; i++)
        {
            var listenerNode = Listeners[i].Node;
            if(listenerNode == null) { continue; }
            var attributes = new Fmod3DAttributes(listenerNode);
            _fmodStudioSystem.setListenerAttributes(i, attributes.ToFmodAttributes());
            _fmodStudioSystem.setListenerWeight(i, Listeners[i].Weight);
        }
    }
}