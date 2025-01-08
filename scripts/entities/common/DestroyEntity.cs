namespace Game.Entities;

using Game.Networking;
using Game.World.Data;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct RemoveEntity(ulong EntityID) : INetMessage
{
    public MessageType MessageType => MessageType.DestroyEntity;

    public void OnClient(ClientManager client)
    {
        client.RemoveEntity(EntityID);
    }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        // Only allow destruction of entities owned by this user
        if (!peer.OwnsEntity(EntityID))
            return;

        var currentSector = peer.GetPlayerState().CurrentSector;

        currentSector.RemoveEntity(EntityID);
    }
}
