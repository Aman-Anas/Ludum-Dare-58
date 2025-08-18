namespace Game.World;

using System;
using Godot;

public partial class WorldBackgroundScene : Node3D
{
    [Export]
    Node3D mainScene = null!;

    Viewport backgroundView = null!;
    Viewport mainView = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        backgroundView = GetViewport();
        mainView = mainScene.GetViewport();
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        var backCam = backgroundView.GetCamera3D();
        var mainCam = mainView.GetCamera3D();
        backCam.Fov = mainCam.Fov;
        backCam.GlobalBasis = mainCam.GlobalBasis;
        backCam.GlobalPosition = mainCam.GlobalPosition; // * 0.5f;
    }
}
