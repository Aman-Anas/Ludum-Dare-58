namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
public partial class DestructiblePropData : EntityData, IHealth
{
    [Export]
    public int Health { get; set; }

    [MemoryPackIgnore]
    public Action HealthDepleted { get; set; }
}

public partial class DestructibleProp : StaticBody3D, INetEntity
{
    public uint EntityID { get; set; }

    public EntityData Data
    {
        get => _data;
        set => _data = (DestructiblePropData)value;
    }

    DestructiblePropData _data;

    public override void _Ready()
    {
        _data.HealthDepleted += _data.DestroyEntity;
    }
}
