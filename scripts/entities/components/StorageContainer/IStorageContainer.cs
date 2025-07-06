namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public partial class InventoryItem(EntityData storable, uint stackSize)
{
    public EntityData Storable { get; set; } = storable;

    public uint StackSize { get; set; } = stackSize;

    [MemoryPackIgnore]
    public IStorable StorableInterface => (IStorable)Storable;

    public bool CanStore(IStorable other, uint count)
    {
        return StorableInterface.Stackable
            && ((StackSize + count) <= StorableInterface.MaxStack)
            && (other.StackName == StorableInterface.StackName);
    }
};

public interface IStorageContainer : IEntityData
{
    public Dictionary<short, InventoryItem> Inventory { get; set; }

    /// <summary>
    /// Whether or not this entity automatically picks up dropped items
    /// </summary>
    public bool AutoPickup { get; set; }

    public short MaxSlots { get; set; }

    public Action OnInventoryUpdate { get; set; }
}

public static class StorageExt
{
    public static bool ValidateIndex(this IStorageContainer storage, short index)
    {
        return index >= 0 && index < storage.MaxSlots;
    }

    public static void UpdateClientInventory(this IStorageContainer storage)
    {
        storage.SendToOwners(
            new StorageUpdate(storage.EntityID, storage.Inventory),
            DeliveryMethod.ReliableOrdered
        );
    }

    public static void ExecuteStorageAction(
        this IStorageContainer storage,
        IStorageContainer next,
        short prevIndex,
        short newIndex,
        uint count
    )
    {
        if (!(storage.ValidateIndex(prevIndex) && next.ValidateIndex(newIndex)))
        {
            return;
        }

        var nextStore = next.Inventory;
        var current = storage.Inventory;

        if (!current.TryGetValue(prevIndex, out var srcData))
        {
            return;
        }
        if ((count > srcData.StackSize) || (count <= 0))
        {
            return;
        }

        // Check if there's anything at the target location
        var nextData = nextStore.GetValueOrDefault(newIndex);

        // If there is, check if these items can stack
        if ((nextData != null) && nextData.CanStore(srcData.StorableInterface, count))
        {
            if (count == srcData.StackSize)
            {
                // If we are moving the whole stack then del the old one
                _ = current.Remove(prevIndex);
            }
            else
            {
                // Otherwise add the amt
                current[prevIndex].StackSize -= count;
            }
            nextData.StackSize += count;
        }
        else // Either nothing at target dest, or it's non-stackable
        {
            // If we're moving all items then swap the data (if anything in next)
            if (count == srcData.StackSize)
            {
                // Put our data in the target dest
                nextStore[newIndex] = srcData;

                if (nextData != null)
                {
                    // Move target into our old slot
                    current[prevIndex] = nextData;
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
                if (nextData != null)
                    return;

                current[prevIndex].StackSize -= count;
                nextStore[newIndex] = new(srcData.Storable.CopyFromResource(), count);
            }
        }

        // Tell all storages about this inventory change
        storage.OnInventoryUpdate?.Invoke();

        if (storage != next)
            next.OnInventoryUpdate?.Invoke();

        // If this is a client, send a message to the server
        storage.Client?.ServerLink.EncodeAndSend(
            new StorageAction(storage.EntityID, next.EntityID, prevIndex, newIndex, count),
            DeliveryMethod.ReliableOrdered
        );
    }
}
