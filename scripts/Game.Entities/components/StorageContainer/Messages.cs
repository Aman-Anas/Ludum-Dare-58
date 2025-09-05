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
    uint SrcCIndex,
    uint DestCIndex
) : INetMessage
{
    public MessageType MessageType => MessageType.StorageAction;

    public void OnClient(ClientManager client) { }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        if (peer.OwnsEntity(SourceEntityID) && peer.OwnsEntity(DestEntityID)
        // && (peer.GetLocalEntity(SourceEntityID) is INetEntity<IStorageContainer> store1)
        // && (peer.GetLocalEntity(DestEntityID) is INetEntity<IStorageContainer> store2)
        // && store1.Data.StorageState.Length > SourceCIndex
        // && store2.Data.StorageState.Length > DestCIndex
        )
        {
            var store1 = peer.GetLocalEntity(SourceEntityID);
            var store2 = peer.GetLocalEntity(DestEntityID);

            // Get the storage components we want to act on

            if (
                (
                    store1.Data.GetComponent<StorageContainerComponent>(SrcCIndex)
                    is StorageContainerComponent storage1
                )
                && (
                    store2.Data.GetComponent<StorageContainerComponent>(DestCIndex)
                    is StorageContainerComponent storage2
                )
            )
            {
                // Call our storage action method to actually move the stuff
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
            // In case we can't find those components (or they're the wrong type), return.
        }
    }
}

/// <summary>
/// Tell the server we want to drop an item on the ground
/// </summary>
[MemoryPackable]
public readonly partial record struct StorageDrop(
    ulong EntityID,
    uint ComponentIndex,
    short DropIndex
) : IEntityUpdate<EntityData>
{
    public MessageType MessageType => MessageType.StorageDrop;

    public void OnClient(ClientManager client) =>
        this.UpdateClientEntity<StorageDrop, EntityData>(client);

    public void OnServer(NetPeer peer, ServerManager server)
    {
        if (peer.OwnsEntity(EntityID))
        {
            this.UpdateServerEntity<StorageDrop, EntityData>(peer);
        }
    }

    public void UpdateEntity(INetEntity<EntityData> entity)
    {
        var storage = entity.Data.GetComponent<StorageContainerComponent>(ComponentIndex);
        storage?.DropItem(DropIndex);
    }
}
