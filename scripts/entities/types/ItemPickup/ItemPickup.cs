namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

public partial class ItemPickup : Area3D, INetEntity<ItemPickupData>
{
    public ItemPickupData Data { get; set; }

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (ItemPickupData)value;
    }

    public override void _Ready()
    {
        BodyEntered += TriggerPickup;
    }

    private void TriggerPickup(Node3D body)
    {
        if (body is INetEntity<IStorageContainer> entity && entity.Data.AutoPickup)
        {
            // On server
            if (Data.CurrentSector != null)
            {
                foreach (var (item, count) in Data.Items)
                {
                    ((IStorable)item).StoreItem(entity.Data, count);
                }

                entity.Data.UpdateInventory();
            }

            Data.DestroyEntity();
        }
    }
}
