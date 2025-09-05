namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using MemoryPack;
using Utilities.Data;

[MemoryPackable]
[GlobalClass]
public partial class ItemPickupEntry(EntityData Item, uint Count) : MemoryPackableResource
{
    public EntityData Item { get; set; } = Item;
    public uint Count { get; set; } = Count;
}

[MemoryPackable]
[GlobalClass]
public partial class ItemPickupData : EntityData
{
    [Export]
    public ItemPickupEntry[] Items { get; set; } = [];

    // // Data, count
    // public Queue<(EntityData, uint)> Items { get; set; } = [];

    // [Export]
    // [MemoryPackIgnore]
    // public Godot.Collections.Array<uint> Counts { get; set; } = null!;

    // [Export]
    // [MemoryPackIgnore]
    // public Godot.Collections.Array<EntityData> Datalist { get; set; } = null!;

    // public override void OnResourceCopy()
    // {
    //     for (int x = 0; x < Datalist.Count; x++)
    //     {
    //         Items.Enqueue((Datalist[x], Counts[x]));
    //     }
    // }

    // public ItemPickupData() { }
}
