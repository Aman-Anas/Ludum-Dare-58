namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Game.World.Data;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct StorablePickup(ulong EntityID, ulong ItemEntityID)
    : IEntityUpdate<PlayerEntityData>
{
    public MessageType MessageType => MessageType.StorageUpdate;

    public void OnClient(ClientManager client) { }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        if (peer.OwnsEntity(ItemEntityID))
        {
            this.UpdateServerEntity<StorablePickup, PlayerEntityData>(peer);
        }
    }

    public void UpdateEntity(INetEntity<PlayerEntityData> entity) { }
}
