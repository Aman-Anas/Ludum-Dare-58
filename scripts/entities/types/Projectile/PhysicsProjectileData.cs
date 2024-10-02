namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
public partial class PhysicsProjectileData : EntityData
{
    [Export]
    public int DamageValue { get; set; }

    [Export]
    public uint DamageType { get; set; } // make this an enum later ?

    [Export]
    public float InitialVelocity { get; set; }

    [Export]
    public float DecayTime { get; set; }
}
