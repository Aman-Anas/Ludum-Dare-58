namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

public interface IHealth : IEntityData
{
    public int Health { get; set; }

    public Action HealthDepleted { get; set; }

    public virtual void UpdateHealth(int newHealth)
    {
        // If new health would be less than zero, call the action

        if (newHealth <= 0)
        {
            HealthDepleted?.Invoke();
        }

        Health = newHealth;

        // Only send from server
        CurrentSector?.EchoToSector(new HealthUpdate(EntityID, newHealth));
    }

    public virtual void ChangeHealthBy(int amount)
    {
        UpdateHealth(Health + amount);
    }
}
