namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable(GenerateType.VersionTolerant)]
public partial class DestructiblePropData : EntityData
{
    [Export]
    public uint Health { get; set; }
}

public partial class DestructibleProp : StaticBody3D, INetEntity
{
    public uint EntityID { get; set; }
    public uint Health { get; set; }
    public EntityData Data
    {
        set => _data = (DestructiblePropData)value;
    }

    DestructiblePropData _data;

    public override void _Ready()
    {
        // ((IHealth)_data).UpdateHealth();
    }
}
