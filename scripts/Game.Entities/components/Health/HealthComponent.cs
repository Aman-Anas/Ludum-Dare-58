namespace Game.Entities;

using System;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using System.Reflection;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface IHealth : IEntityData
{
    public HealthComponent HealthState { get; }
}

[GlobalClass]
[MemoryPackable]
public partial class HealthComponent : NetComponent<EntityData>
{
    [Export]
    public int InternalHealth { get; set; }

    /// <summary>
    /// Action emitted when health is about to go below zero.
    /// int parameter is the new health value.
    /// </summary>
    public event Action<int>? OnHealthDepleted;

    [MemoryPackIgnore]
    public int Health
    {
        get => InternalHealth;
        set
        {
            if (value <= 0)
            {
                OnHealthDepleted?.Invoke(value);
            }
            InternalHealth = value;

            // Update the sector
            this.NetUpdate();
        }
    }
}
