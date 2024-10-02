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
