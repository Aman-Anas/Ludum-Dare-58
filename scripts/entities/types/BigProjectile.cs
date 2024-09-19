namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
public partial class BigProjectileData : EntityData
{
    [Export]
    public int DamageValue { get; set; }

    [Export]
    public uint DamageType { get; set; } // make this an enum later ?

    [Export]
    public float InitialVelocity { get; set; }

    [Export]
    public float DecayTime { get; set; }
}

public partial class BigProjectileServer : RigidBody3D, INetEntity
{
    public EntityData Data
    {
        get => _data;
        set => _data = (BigProjectileData)value;
    }

    BigProjectileData _data;

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

public partial class BigProjectileClient : Area3D, INetEntity
{
    public EntityData Data
    {
        get => _data;
        set => _data = (BigProjectileData)value;
    }

    BigProjectileData _data;

    public override void _Ready()
    {
        BodyEntered += OnCollision;

        // Add auto destroy on timeout
        var timer = GetTree().CreateTimer(_data.DecayTime, false);
        timer.Timeout += () => _data.DestroyEntity();
    }

    private void OnCollision(Node body)
    {
        _data.DestroyEntity();
    }
}
