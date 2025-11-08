using Godot;

namespace GodotFMODSharp.Emitters;

[GlobalClass]
public partial class FmodListener3D : Node3D
{
    private FmodListener _fmodListener;
    
    public override void _Ready()
    {
        _fmodListener = new FmodListener();
        _fmodListener.LinkedListenerNode = this;
        _fmodListener.Weight = 1;
        AddChild(_fmodListener);
    }
}