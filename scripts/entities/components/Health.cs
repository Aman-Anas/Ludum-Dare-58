namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct HealthUpdate(uint EntityID, int Health) : IEntityUpdate
{
    public readonly MessageType GetMessageType() => MessageType.HealthUpdate;

    public readonly void OnClient(ClientManager client) => this.UpdateEntity(client);

    public readonly void OnServer(NetPeer peer, ServerManager server)
    {
        // Maybe we don't want the clients to be able to directly affect other object's health
        // this.UpdateEntity(peer, server);
    }

    public readonly void UpdateEntity(INetEntity entity)
    {
        // Set health directly (the update* methods are for emitting this message, so we
        // don't want to call them)
        ((IHealth)entity.Data).Health = Health;
    }
}

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
