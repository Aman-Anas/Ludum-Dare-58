namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

public partial class DestructibleProp : StaticBody3D, INetEntity<DestructiblePropData>
{
    public uint EntityID { get; set; }

    // public EntityData Data
    // {
    //     get => _data;
    //     set => _data = (DestructiblePropData)value;
    // }

    public DestructiblePropData Data { get; set; }

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (DestructiblePropData)value;
    }

    public override void _Ready()
    {
        Data.HealthDepleted += Data.DestroyEntity;
    }
}
