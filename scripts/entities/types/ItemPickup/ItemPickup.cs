namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[GlobalClass]
public partial class ItemPickup : Area3D, INetEntity<ItemPickupData>
{
    [Export]
    public ItemPickupData Data { get; set; } = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (ItemPickupData)value;
    }

    public override void _Ready()
    {
        AreaEntered += TriggerPickup;
    }

    private void TriggerPickup(Node3D body)
    {
        if (body is INetEntity<IStorageContainer> entity && entity.Data.AutoPickup)
        {
            // On server
            if (Data.CurrentSector != null)
            {
                while (Data.Items.TryDequeue(out (EntityData item, uint count) val))
                {
                    if (!((IStorable)val.item).StoreItem(entity.Data, val.count))
                    {
                        // early exit since we failed to store an item
                        entity.Data.UpdateClientInventory();
                        return;
                    }
                }

                entity.Data.UpdateClientInventory();
                Data.DestroyEntity();
            }
        }
    }

    public override void _Process(double delta)
    {
        Rotation = new(0, Rotation.Y + (float)(delta * 3), 0);
    }
}
