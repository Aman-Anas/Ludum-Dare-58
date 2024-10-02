namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

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
