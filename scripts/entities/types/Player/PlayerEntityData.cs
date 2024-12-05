namespace Game.Entities;

using System;
using System.Diagnostics.CodeAnalysis;
using Game.Networking;
using Godot;
using MemoryPack;

[GlobalClass]
[MemoryPackable]
public partial class PlayerEntityData : EntityData, IHealth, IBasicAnim
{
    [Export]
    public int Health { get; set; }

    [Export]
    // Why save this? because it's funny
    public float TorsoPivotAngle { get; set; }

    [Export]
    // For now, use a simple color to differentiate players
    public Color PlayerColor { get; set; }

    [MemoryPackIgnore]
    public Vector3 HeadRotation { get; set; } = Vector3.Zero;

    [MemoryPackIgnore]
    public byte CurrentAnim { get; set; }

    [MemoryPackIgnore]
    public Action HealthDepleted { get; set; }

    static readonly StringName IdleName = new(nameof(PlayerAnims.Idle));
    static readonly StringName RunningName = new(nameof(PlayerAnims.Run));

    public StringName GetAnimation()
    {
        return (PlayerAnims)CurrentAnim switch
        {
            PlayerAnims.Idle => IdleName,
            PlayerAnims.Run => RunningName,
            _ => IdleName
        };
    }
}

public enum PlayerAnims : byte
{
    Idle,
    Run
}
