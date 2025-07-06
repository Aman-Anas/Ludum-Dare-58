namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

public partial class DestructibleProp : StaticBody3D, INetEntity<DestructiblePropData>
{
    public DestructiblePropData Data { get; set; } = null!;

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
