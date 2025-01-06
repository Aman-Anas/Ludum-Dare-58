namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public partial class InventoryItem(EntityData storable, int stackSize)
{
    public EntityData Storable { get; set; } = storable;

    public int StackSize { get; set; } = stackSize;

    [MemoryPackIgnore]
    public IStorable StorableInterface => (IStorable)Storable;
};

public interface IStorageContainer : IEntityData
{
    public Dictionary<short, InventoryItem> Inventory { get; set; }

    /// <summary>
    /// Whether or not this entity automatically picks up dropped items
    /// </summary>
    public bool AutoPickup { get; set; }

    public short MaxSlots { get; set; }

    public (short, short) VisualGridSize { get; set; }
}

public static class StorageExt
{
    public static bool ValidateIndex(this IStorageContainer storage, short index)
    {
        return index >= 0 && index < storage.MaxSlots;
    }

    public static void MoveItem(
        this IStorageContainer storage,
        IStorageContainer next,
        short prevIndex,
        short newIndex
    )
    {
        if (!(storage.ValidateIndex(prevIndex) && storage.ValidateIndex(newIndex)))
        {
            return;
        }

        var nextStore = next.Inventory;
        if (nextStore.ContainsKey(newIndex))
        {
            return;
        }

        var current = storage.Inventory;
        if (!current.Remove(prevIndex, out var data))
        {
            return;
        }

        nextStore[newIndex] = data;

        storage.UpdateInventory();
    }

    public static void UpdateInventory(this IStorageContainer container)
    {
        container.CurrentSector?.EchoToOwners(
            container.Owners,
            new StorageUpdate(container.EntityID, container.Inventory)
        );
    }
}
