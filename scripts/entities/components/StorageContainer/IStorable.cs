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

    public string IconPath { get; set; }
}

public static class StorableExt
{
    public static bool StoreItem(this IStorable storable, IStorageContainer storage, uint count)
    {
        // if there's a stack let's add it in
        if (storable.Stackable)
        {
            foreach (var existing in storage.Inventory.Values)
            {
                if (existing.CanStore(storable, count))
                {
                    existing.StackSize += count;
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
        _ = storable.CurrentSector?.RemoveEntity(storable.EntityID);

        var data = (EntityData)storable;

        for (short x = 0; x < storage.MaxSlots; x++)
        {
            if (!storage.Inventory.ContainsKey(x))
            {
                storage.Inventory[x] = new(data, count);
                return true;
            }
        }

        // Should never get here
        return false;
    }
}
