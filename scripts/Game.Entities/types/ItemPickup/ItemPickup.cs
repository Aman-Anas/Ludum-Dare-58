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

    /// <summary>
    /// The visual element/representation to animate and spin
    /// </summary>
    [Export]
    Node3D visualElement = null!;

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
        if (body is INetEntity<IAutoCollector> entity && entity.Data.AutoPickup)
        {
            // Only pickup stuff on the server side. (for now)
            if (Data.CurrentSector != null)
            {
                foreach (var entry in Data.Items)
                {
                    bool storeSuccess = ((IStorable)entry.Item).StorableInfo.StoreItem(
                        entity.Data.MainInventory,
                        entry.Count
                    );

                    if (!storeSuccess)
                    {
                        entity.Data.MainInventory.UpdateClientInventory();
                        return;
                    }
                }

                entity.Data.MainInventory.UpdateClientInventory();
                Data.DestroyEntity();
            }
        }
    }

    public override void _Process(double delta)
    {
        visualElement.Rotation = new(0, visualElement.Rotation.Y + (float)(delta * 3), 0);
        visualElement.Position = new(0, MathF.PI + MathF.Sin(Time.GetTicksMsec() / 1000f), 0);
    }
}
