using Godot;
using GodotFMODSharp;

namespace GodotFMODSharp.Emitters;

/// <summary>
/// Global classes can't be subclassed, so instead this class is created as a child of the GlobalClasses 3D listener and 2D listener
/// </summary>
public partial class FmodListener : Node
{
    public Node LinkedListenerNode;
    public float Weight = 1;
    
    public override void _Ready()
    {
        FmodServer.FmodStudioSystem.AddListener(LinkedListenerNode, Weight);
    }
}