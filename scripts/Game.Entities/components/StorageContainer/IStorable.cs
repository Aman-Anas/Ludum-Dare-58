namespace Game.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

/// <summary>
/// Interface meaning this object can be stored inside an IStorageContainer
/// </summary>
public interface IStorable : IEntityData
{
    StorableComponent StorableInfo { get; }
}

[MemoryPackable]
public partial class StorableComponent : Component<IStorable>
{
    // Whether or not this storable can stack with anything else
    // If this is set then stack class and MaxStack doesn't matter
    public bool Stackable { get; set; }

    // Unique identifier per stackable type
    // This could be an enum maybe
    public string? StackClass { get; init; }

    // The maximum amount this storable can stack
    public int MaxStack { get; set; }

    // Path to the icon displayed in inventory for this storable
    public string IconPath { get; set; } = @"res://icon.svg";

    public bool StoreItem(StorageContainerComponent storage, uint count)
    {
        // if there's a stack let's add it in
        if (Stackable)
        {
            foreach (var existing in storage.Inventory.Values)
            {
                if (existing.CanStack(data, count))
                {
                    // Update the target stack size
                    existing.StackSize += count;

                    // Remove this item from the world
                    _ = data.CurrentSector?.RemoveEntity(data.EntityID);

                    return true;
                }
            }
        }

        if (storage.Inventory.Count >= storage.MaxSlots)
        {
            // there be no space
            return false;
        }

        // Now that we know there's at least one empty slot, let's put our storable in
        _ = data.CurrentSector?.RemoveEntity(data.EntityID);

        // var data = (EntityData)storable;

        for (short x = 0; x < storage.MaxSlots; x++)
        {
            if (!storage.Inventory.ContainsKey(x))
            {
                storage.Inventory[x] = new InventoryEntry((EntityData)data, count);
                return true;
            }
        }

        // Should never get here
        return false;
    }
}
