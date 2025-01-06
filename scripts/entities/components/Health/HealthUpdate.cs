namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct HealthUpdate(ulong EntityID, int Health)
    : IEntityUpdate<IHealth>
{
    public MessageType MessageType => MessageType.HealthUpdate;

    public void OnClient(ClientManager client) =>
        this.UpdateClientEntity<HealthUpdate, IHealth>(client);

    public void OnServer(NetPeer peer, ServerManager server) { }

    public void UpdateEntity(INetEntity<IHealth> entity)
    {
        // Set health directly (the update* methods are for emitting this message, so we
        // don't want to call them)
        entity.Data.Health = Health;
    }
}
