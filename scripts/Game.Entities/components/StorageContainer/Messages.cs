namespace Game.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct StorageAction(
    EntityID SourceEntityID,
    EntityID DestEntityID,
    short PrevIndex,
    short NewIndex,
    uint Count,
    int SourceCIndex,
    int DestCIndex
) : INetMessage
{
    public MessageType MessageType => MessageType.StorageAction;

    public void OnClient(ClientManager client) { }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        if (
            peer.OwnsEntity(SourceEntityID)
            && peer.OwnsEntity(DestEntityID)
            && (peer.GetLocalEntity(SourceEntityID) is INetEntity<IStorageContainer> store1)
            && (peer.GetLocalEntity(DestEntityID) is INetEntity<IStorageContainer> store2)
            && store1.Data.StorageState.Length > SourceCIndex
            && store2.Data.StorageState.Length > DestCIndex
        )
        {
            var storage1 = store1.Data.StorageState[SourceCIndex];
            var storage2 = store2.Data.StorageState[DestCIndex];

            storage1.StorageAct(storage2, PrevIndex, NewIndex, Count);

            if (store1.Data == store2.Data)
            {
                // If this is within the same entity
                storage1.UpdateClientInventory();
            }
            else
            {
                // Otherwise we need to send an update to all owners of each entity
                storage1.UpdateClientInventory();
                storage2.UpdateClientInventory();
            }
        }
    }
}

[MemoryPackable]
public partial record StorageUpdate(
    ulong EntityID,
    int ComponentIndex,
    Dictionary<short, InventoryEntry> Inventory
) : IEntityUpdate<IStorageContainer>
{
    public MessageType MessageType => MessageType.StorageUpdate;

    public void OnClient(ClientManager client)
    {
        this.UpdateClientEntity<StorageUpdate, IStorageContainer>(client);
    }

    public void OnServer(NetPeer peer, ServerManager server) { }

    public void UpdateEntity(INetEntity<IStorageContainer> entity)
    {
        var storage = entity.Data.StorageState[ComponentIndex];
        storage.Inventory = Inventory;
        storage.OnInventoryUpdated?.Invoke();
    }
}

/// <summary>
/// Tell the server we want to drop an item on the ground
/// </summary>
[MemoryPackable]
public readonly partial record struct StorageDrop(
    ulong EntityID,
    int ComponentIndex,
    short DropIndex
) : IEntityUpdate<IStorageContainer>
{
    public MessageType MessageType => MessageType.StorageDrop;

    public void OnClient(ClientManager client) =>
        this.UpdateClientEntity<StorageDrop, IStorageContainer>(client);

    public void OnServer(NetPeer peer, ServerManager server)
    {
        if (peer.OwnsEntity(EntityID))
        {
            this.UpdateServerEntity<StorageDrop, IStorageContainer>(peer);
        }
    }

    public void UpdateEntity(INetEntity<IStorageContainer> entity)
    {
        var storage = entity.Data.StorageState[ComponentIndex];
        storage.DropItem(DropIndex);
    }
}
