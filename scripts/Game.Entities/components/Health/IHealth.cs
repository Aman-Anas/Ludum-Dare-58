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

[MemoryPackable]
public partial class HealthComponent : Component<IHealth>
{
    public int InternalHealth { get; set; }

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
            Update();
        }
    }

    public void Update()
    {
        data.CurrentSector?.EchoToSector(new HealthUpdate(data.EntityID, InternalHealth));
    }
}
