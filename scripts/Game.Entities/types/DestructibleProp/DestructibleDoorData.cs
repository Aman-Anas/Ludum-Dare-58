namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using MemoryPack;

[GlobalClass]
[MemoryPackable]
public partial class DestructibleDoorData : EntityData, IHealth, IDoor
{
    [Export]
    public ToggleComponent DoorState { get; set; } = new();

    [Export]
    public HealthComponent HealthState { get; set; } = new();

    public DestructibleDoorData()
    {
        ComponentRegistry = new(this, [DoorState, HealthState]);
    }
}
