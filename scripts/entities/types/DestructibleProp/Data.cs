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
