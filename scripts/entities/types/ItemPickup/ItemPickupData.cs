namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
public partial class ItemPickupData : EntityData
{
    // Data, count
    public List<(EntityData, int)> Items { get; set; }
}
