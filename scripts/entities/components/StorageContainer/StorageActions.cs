namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct StorageMove(
    ulong SourceEntityID,
    ulong DestEntityID,
    short PrevIndex,
    short NewIndex
) : INetMessage
{
    public MessageType MessageType => MessageType.StorageMove;

    public void OnClient(ClientManager client) { }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        if (
            peer.OwnsEntity(SourceEntityID)
            && peer.OwnsEntity(DestEntityID)
            && (peer.GetLocalEntity(SourceEntityID) is IStorageContainer store1)
            && (peer.GetLocalEntity(DestEntityID) is IStorageContainer store2)
        )
        {
            store1.MoveItem(store2, PrevIndex, NewIndex);
        }
    }
}

[MemoryPackable]
public partial record StorageUpdate(ulong EntityID, Dictionary<short, InventoryItem> Inventory)
    : IEntityUpdate<IStorageContainer>
{
    public MessageType MessageType => MessageType.StorageUpdate;

    public void OnClient(ClientManager client)
    {
        this.UpdateClientEntity<StorageUpdate, IStorageContainer>(client);
    }

    public void OnServer(NetPeer peer, ServerManager server) { }

    public void UpdateEntity(INetEntity<IStorageContainer> entity)
    {
        entity.Data.Inventory = Inventory;
    }
}
