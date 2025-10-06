using System;
using Game;
using Godot;

public partial class SheepProjectile : RigidBody3D
{
    const float MinimumVelocity = 2.5f;

    [Export]
    PackedScene pickupScene = null!;

    ulong spawnTime;

    const float MinGrabTimeMs = 200;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        BodyEntered += CheckPlayerHit;
        spawnTime = Time.GetTicksMsec();
    }

    private void CheckPlayerHit(Node body)
    {
        if (body is Farmer && ((Time.GetTicksMsec() - spawnTime) >= MinGrabTimeMs))
        {
            Manager.Instance.Data.CurrentSheepCount++;
            QueueFree();
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (LinearVelocity.LengthSquared() <= (MinimumVelocity * MinimumVelocity))
        {
            // Replace this with a sheep pickup
            var newPickup = pickupScene.Instantiate<SheepPickup>();
            newPickup.AmountGiven = 1;
            GetTree().CurrentScene.AddChild(newPickup);
            newPickup.GlobalPosition = GlobalPosition;
            newPickup.GlobalRotation = GlobalRotation;

            QueueFree();
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
