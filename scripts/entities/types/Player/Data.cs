namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
public partial class PlayerEntityData : EntityData, IHealth
{
    [Export]
    public int Health { get; set; }

    // Why save this? because it's funny
    public float TorsoPivotAngle { get; set; }

    // For now, use a simple color to differentiate players
    public Color PlayerColor { get; set; }

    [MemoryPackIgnore]
    public Action HealthDepleted { get; set; }
}
