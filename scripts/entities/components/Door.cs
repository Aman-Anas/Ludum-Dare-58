namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct DoorUpdate(uint EntityID, bool DoorState) : IEntityUpdate
{
    public readonly MessageType GetMessageType() => MessageType.DoorUpdate;

    public readonly void OnClient(ClientManager client) => this.UpdateEntity(client);

    public readonly void OnServer(NetPeer peer, ServerManager server) =>
        this.UpdateEntity(peer, server);

    public readonly void UpdateEntity(EntityData data)
    {
        ((IDoor)data).DoorState = DoorState;
    }
}

public interface IDoor : INetEntity
{
    public bool DoorState { get; set; }

    public void UpdateHealth(NetPeer peer)
    {
        peer.EncodeAndSend(new DoorUpdate(EntityID, DoorState));
    }
}
