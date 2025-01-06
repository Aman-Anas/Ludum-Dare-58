namespace Game.Entities;

using System;
using System.Collections.Generic;
using System.Linq;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface IStorable : IEntityData
{
    public string StackName { get; set; }
    public bool Stackable { get; set; }
    public int MaxStack { get; set; }
}

public static class StorableExt
{
    public static void StoreItem(this IStorable storable, IStorageContainer storage, int count)
    {
        if (storage.CurrentSector.SectorID != storable.CurrentSector.SectorID)
        {
            GD.PrintErr(
                $"Tried to store {storable.EntityID} in {storage.EntityID} but not in the same sector"
            );
        }

        // if there's a stack let's add it in
        if (storable.Stackable)
        {
            foreach (var existing in storage.Inventory.Values)
            {
                var existingStore = existing.StorableInterface;
                if (existingStore.Stackable && (storable.StackName == existingStore.StackName))
                {
                    existing.StackSize += count;
                    return;
                }
            }
        }

        if (storage.Inventory.Count >= storage.MaxSlots)
        {
            // there be no space
            return;
        }

        var data = storable.CurrentSector.RemoveEntity(storable.EntityID);

        for (short x = 0; x < storage.Inventory.Count; x++)
        {
            if (!storage.Inventory.ContainsKey(x))
            {
                storage.Inventory[x] = new(data, count);
                break;
            }
        }
    }
}
