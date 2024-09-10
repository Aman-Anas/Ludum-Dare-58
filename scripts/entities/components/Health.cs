namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct HealthUpdate(uint EntityID, uint Health) : IEntityUpdate
{
    public readonly MessageType GetMessageType() => MessageType.HealthUpdate;

    public readonly void OnClient(ClientManager client) => this.UpdateEntity(client);

    public readonly void OnServer(NetPeer peer, ServerManager server) =>
        this.UpdateEntity(peer, server);

    public void UpdateEntity(EntityData data)
    {
        ((IHealth)data).Health = Health;
    }
}

public interface IHealth : INetEntity
{
    public uint Health { get; set; }

    public void UpdateHealth(NetPeer peer)
    {
        peer.EncodeAndSend(new HealthUpdate(EntityID, Health));
    }
}
