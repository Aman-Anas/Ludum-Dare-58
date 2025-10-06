using System;
using Game;
using Godot;

public partial class SheepPickup : Area3D
{
    public int AmountGiven { get; set; } = 3;

    const float SpinSpeed = 35;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        BodyEntered += OnBodyEntered;
    }

    private void OnBodyEntered(Node3D body)
    {
        if (body is Farmer)
        {
            Manager.Instance.Data.CurrentSheepCount += AmountGiven;
            QueueFree();
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.

    public override void _Process(double delta)
    {
        RotationDegrees = new(0, RotationDegrees.Y + (float)(SpinSpeed * delta), 0);
    }
}
