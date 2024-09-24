namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

public partial class PhysicsProjectileClient : RigidBody3D, INetEntity
{
    public EntityData Data
    {
        get => _data;
        set => _data = (PhysicsProjectileData)value;
    }

    PhysicsProjectileData _data;

    public override void _Ready()
    {
        BodyEntered += OnCollision;

        // Add auto destroy on timeout
        var timer = GetTree().CreateTimer(_data.DecayTime, false);
        timer.Timeout += () => _data.DestroyEntity();

        // Add some speeeeed
        LinearVelocity = new(0, _data.InitialVelocity, 0);
    }

    private void OnCollision(Node body)
    {
        QueueFree(); // usually we wouldn't need this
    }
}
