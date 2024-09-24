namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

public partial class PhysicsProjectileServer : RigidBody3D, INetEntity
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

    public override void _PhysicsProcess(double delta)
    {
        this.UpdateTransform();
    }

    private void OnCollision(Node body)
    {
        _data.DestroyEntity();
        if (body is not INetEntity entity)
            return;

        if (entity.Data is IHealth healthData)
        {
            healthData.ChangeHealthBy(-_data.DamageValue);
        }
    }
}
