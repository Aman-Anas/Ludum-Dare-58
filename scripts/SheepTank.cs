using System;
using Game;
using Godot;

public partial class SheepTank : Node3D
{
    [Export]
    AnimationPlayer player;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Manager.Instance.Data.FinalThingy += FinalEvent;
    }

    private void FinalEvent()
    {
        Visible = true;
        player.Play("end");

        Manager.Instance.Data.CurrentSheepCount = 2000;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.

    public override void _Process(double delta) { }
}
