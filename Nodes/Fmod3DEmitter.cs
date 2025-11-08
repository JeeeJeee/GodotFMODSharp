using Godot;

namespace GodotFMODSharp.Emitters;

[GlobalClass, Icon("res://addons/gdsharpfmod/icons/fmod_emitter.png")]
public partial class Fmod3DEmitter : Node3D
{
    [Export] public string EventGuid;
    [Export] public string EventPath;
    [Export] public bool Autoplay;
    
    public EventInstance CurrentEventInstance;
    public EventDescription EventDescription;
    
    private Fmod3DAttributes _fmod3DAttributes;

    public override void _Ready()
    {
        _fmod3DAttributes = new Fmod3DAttributes(this);
        if (Autoplay && string.IsNullOrEmpty(EventPath) || string.IsNullOrEmpty(EventGuid))
        {
            GD.PrintErr($"{0} has autoplay set to true, but no event path or guid was provided.", GetPath());
        }
        if (!string.IsNullOrEmpty(EventGuid))
        {
            SetEvent(EventGuid);
            if (Autoplay) { Start(); }
        }
    }

    public bool SetEvent(string guid)
    {
        return FmodServer.FmodStudioSystem.GetEventByGuid(EventGuid, out EventDescription);
    }

    public void Start(bool reset = true)
    {
        if (reset)
        {
            Stop();
        }
        CurrentEventInstance = EventDescription.CreateInstance();
        CurrentEventInstance.Set3DAttributes(ref _fmod3DAttributes);
        CurrentEventInstance.Start();
    }

    public override void _Process(double delta)
    {
        if(CurrentEventInstance == null) { return; }
        _fmod3DAttributes.Update(this);
        CurrentEventInstance.Set3DAttributes(ref _fmod3DAttributes);
    }

    public void Stop(FmodStopMode stopMode = FmodStopMode.IMMIDIATE)
    {
        CurrentEventInstance?.Stop(stopMode);
    }

    public bool IsPlaying()
    {
        if (CurrentEventInstance == null || !CurrentEventInstance.IsValid())
        {
            return false;
        }
        
        return !CurrentEventInstance.IsPaused();
    }
}