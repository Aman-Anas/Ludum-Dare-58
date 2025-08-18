namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
public partial class DestructibleDoorData : EntityData, IHealth, IToggleable
{
    public HealthComponent HealthState { get; set; } = new();

    public ToggleComponent[] Toggles { get; } =
        [
            new ToggleComponent(), // Door
        ];

    [MemoryPackIgnore]
    public ToggleComponent Door => Toggles[0];

    public DestructibleDoorData()
    {
        Toggles.InitializeAll(this);
        HealthState.Initialize(this);
    }
}
