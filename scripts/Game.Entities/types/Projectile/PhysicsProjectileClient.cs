namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

public partial class PhysicsProjectileClient : RigidBody3D, INetEntity<PhysicsProjectileData>
{
    public PhysicsProjectileData Data { get; set; } = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (PhysicsProjectileData)value;
    }

    public override void _Ready()
    {
        BodyEntered += OnCollision;

        // Add auto destroy on timeout
        var timer = GetTree().CreateTimer(Data.DecayTime, false);
        timer.Timeout += () => Data.DestroyEntity();

        // Add some speeeeed
        LinearVelocity = new(0, Data.InitialVelocity, 0);
    }

    private void OnCollision(Node body)
    {
        Data.DestroyEntity();
    }
}
