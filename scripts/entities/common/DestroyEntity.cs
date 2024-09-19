namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct DestroyEntity(uint EntityID) : INetMessage
{
    public readonly MessageType GetMessageType() => MessageType.DestroyEntity;

    public readonly void OnClient(ClientManager client)
    {
        var id = EntityID;
        client.EventQueue.Enqueue(() => client.DestroyEntity(id));
    }

    public readonly void OnServer(NetPeer peer, ServerManager server)
    {
        // Only allow updating transform of entities owned by this user
        if (!peer.OwnsEntity(EntityID))
            return;

        var currentSector = peer.GetPlayerState().CurrentSector;

        var id = EntityID;
        server.EventQueue.Enqueue(() => currentSector.DestroyEntity(id));
    }
}
