namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

public partial class DestructibleDoor : StaticBody3D, INetEntity<DestructibleDoorData>
{
    public DestructibleDoorData Data { get; set; } = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (DestructibleDoorData)value;
    }

    public override void _Ready()
    {
        Data.HealthState.OnHealthDepleted += (_) => Data.DestroyEntity();
    }
}
