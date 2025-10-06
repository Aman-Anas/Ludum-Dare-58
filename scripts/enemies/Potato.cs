using System;
using Godot;

public partial class Potato : RigidBody3D
{
    [Export]
    Timer timeThing = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        timeThing.Timeout += QueueFree;
        // timeThing.Start();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
