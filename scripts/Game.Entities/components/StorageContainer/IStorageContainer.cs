namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

/// <summary>
/// Interface meaning this object can contain IStorable entities
/// </summary>
public interface IStorageContainer : IEntityData
{
    public StorageContainerComponent[] StorageState { get; }
}

[MemoryPackable]
public partial class InventoryEntry(EntityData storable, uint stackSize)
{
    public EntityData Storable { get; } = storable;
    public uint StackSize { get; set; } = stackSize;

    [MemoryPackIgnore]
    public IStorable StorableInterface => (IStorable)Storable;

    /// <summary>
    /// Check whether you can put "count" number of entities into this stack
    /// </summary>
    public bool CanStack(IStorable incoming, uint count)
    {
        var myInfo = StorableInterface.StorableInfo;
        var incomingInfo = incoming.StorableInfo;

        return myInfo.Stackable
            && ((StackSize + count) <= myInfo.MaxStack)
            && (incomingInfo.StackClass == myInfo.StackClass);
    }
}

[MemoryPackable]
public partial class StorageContainerComponent : Component<IStorageContainer>
{
    // The actual data of the inventory
    public Dictionary<short, InventoryEntry> Inventory { get; set; } = [];

    // Total number of inventory slots in this container
    public short MaxSlots { get; set; }

    [MemoryPackIgnore]
    // This is triggered whenever the inventory gets updated (connect to UI etc)
    public Action? OnInventoryUpdated { get; set; }

    [MemoryPackIgnore]
    // Optional item drop location for whenever you call DropItem();
    public Func<Vector3>? DropLocation { get; set; }

    /// <summary>
    /// Checks whether a given slot is within bounds
    /// </summary>
    public bool ValidateIndex(short index)
    {
        return index >= 0 && index < MaxSlots;
    }

    /// <summary>
    /// Tell all the clients about the current complete inventory state
    /// </summary>
    public void UpdateClientInventory()
    {
        data.SendToOwners(
            new StorageUpdate(data.EntityID, index, Inventory),
            DeliveryMethod.ReliableOrdered
        );
    }

    /// <summary>
    /// Main method to move "count" objects from one stack to another. Also works across containers
    /// </summary>
    public bool StorageAct(
        StorageContainerComponent next,
        short prevIndex,
        short newIndex,
        uint count
    )
    {
        if (!(ValidateIndex(prevIndex) && next.ValidateIndex(newIndex)))
        {
            return false;
        }

        var nextStore = next.Inventory;
        var current = Inventory;

        if (!current.TryGetValue(prevIndex, out var srcData))
        {
            return false;
        }
        if ((count > srcData.StackSize) || (count <= 0))
        {
            return false;
        }

        // Check if there's anything at the target location
        var nextSlotItem = nextStore.GetValueOrDefault(newIndex);

        // There's something there. Check if we can stack
        if ((nextSlotItem != null) && nextSlotItem.CanStack(srcData.StorableInterface, count))
        {
            if (count == srcData.StackSize)
            {
                // If we are moving the whole stack then del the old one
                _ = current.Remove(prevIndex);
            }
            else
            {
                // Otherwise remove what we need to from the old stack
                current[prevIndex].StackSize -= count;
            }
            nextSlotItem.StackSize += count;
        }
        else // Either nothing at target dest, or it's non-stackable
        {
            // If we're moving all items then swap the data (if anything in next)
            if (count == srcData.StackSize)
            {
                // Put our data in the target dest
                nextStore[newIndex] = srcData;

                if (nextSlotItem != null)
                {
                    // Move target into our old slot
                    current[prevIndex] = nextSlotItem;
                }
                else
                {
                    // Or if there's nothing there, then clear our old slot
                    _ = current.Remove(prevIndex);
                }
            }
            else
            {
                // If we're splitting a stack, we have to make sure there's
                // nothing at the target destination
                if (nextSlotItem != null)
                    return false;

                current[prevIndex].StackSize -= count;
                nextStore[newIndex] = new(srcData.Storable.CopyFromResource(), count);
            }
        }

        // Tell all storages about this inventory change
        OnInventoryUpdated?.Invoke();

        if (data != next)
            next.OnInventoryUpdated?.Invoke();

        // If this is a client, send a message to the server
        data.Client?.ServerLink.EncodeAndSend(
            new StorageAction(
                data.EntityID,
                next.data.EntityID,
                prevIndex,
                newIndex,
                count,
                index,
                next.index
            ),
            DeliveryMethod.ReliableOrdered
        );

        return true;
    }

    public void DropItem(short dropIndex)
    {
        const string DroppedItemScene = @"res://scenes/entities/test_items/item_pickup.tscn";

        if (!ValidateIndex(dropIndex))
            return;

        if (!Inventory.Remove(dropIndex, out var item))
        {
            return;
        }

        // If we are on the client send a message
        if (data.CurrentSector == null)
        {
            data.Client.ServerLink.EncodeAndSend(
                new StorageDrop(data.EntityID, this.index, dropIndex),
                DeliveryMethod.ReliableUnordered
            );
        }
        else
        {
            var pickup = new ItemPickupData();
            pickup.Items.Enqueue((item.Storable, item.StackSize));

            pickup.ClientScene = DroppedItemScene;
            pickup.ServerScene = DroppedItemScene;

            Vector3 droplocation;
            if (DropLocation != null)
            {
                droplocation = DropLocation();
            }
            else
            {
                droplocation = data.CurrentSector.Entities[data.EntityID].GetNode().GlobalPosition;
            }

            data.CurrentSector?.SpawnNewEntity(droplocation, Vector3.Zero, pickup);
        }
    }
}
