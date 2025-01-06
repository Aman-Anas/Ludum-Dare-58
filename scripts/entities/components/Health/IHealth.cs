namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

public interface IHealth : IEntityData
{
    public int Health { get; set; }

    public Action HealthDepleted { get; set; }
}

public static class HealthExt
{
    public static void SetHealthServer(this IHealth data, int newHealth)
    {
        // If new health would be less than zero, call the action
        if (newHealth <= 0)
        {
            data.HealthDepleted?.Invoke();
        }

        data.Health = newHealth;

        // Only send from server
        data.CurrentSector?.EchoToSector(new HealthUpdate(data.EntityID, newHealth));
    }

    public static void ChangeHealthBy(this IHealth data, int amount)
    {
        data.SetHealthServer(data.Health + amount);
    }
}
