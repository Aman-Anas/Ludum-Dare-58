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
    ulong SourceEntityID,
    ulong DestEntityID,
    short PrevIndex,
    short NewIndex,
    uint Count
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
        )
        {
            store1.Data.ExecuteStorageAction(store2.Data, PrevIndex, NewIndex, Count);

            if (store1.Data == store2.Data)
            {
                // If this is within the same entity
                store1.Data.UpdateClientInventory();
            }
            else
            {
                // Otherwise we need to send an update to all owners of each entity
                var owners = new HashSet<ulong>();
                owners.UnionWith(store1.Data.Owners);
                owners.UnionWith(store2.Data.Owners);
                store1.Data.CurrentSector.EchoToOwners(
                    owners,
                    new StorageUpdate(SourceEntityID, store1.Data.Inventory),
                    DeliveryMethod.ReliableOrdered
                );
            }
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
        entity.Data.OnInventoryUpdate?.Invoke();
    }
}

[MemoryPackable]
/// <summary>
/// Message to drop an item on the ground
/// </summary>
public readonly partial record struct StorageDrop(ulong EntityID, int DropIndex, Vector3 Rotation)
    : IEntityUpdate<IStorageContainer>
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
        var inventory = entity.Data.Inventory;

        // Inform clients about the change
        entity.Data.UpdateClientInventory();
    }
}
