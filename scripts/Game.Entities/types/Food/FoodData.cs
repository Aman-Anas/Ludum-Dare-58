namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using MemoryPack;

public enum FoodType
{
    Standard,
}

[MemoryPackable]
[GlobalClass]
public partial class FoodData : EntityData, IStorable
{
    [Export]
    public string FoodName { get; set; } = "something unknown";

    [Export]
    public FoodType FoodType { get; set; } = FoodType.Standard;

    [Export]
    public StorableComponent StorableInfo { get; set; } =
        new()
        {
            StackClass = nameof(FoodData),
            Stackable = true,
            MaxStack = 20,

            // Use @ for 'verbatim' file paths, it disables escape sequences etc
            IconPath = @"res://addons/kenney_prototype_textures/red/texture_11.png",
        };

    public FoodData()
    {
        ComponentRegistry = new(
            this,
            [
                StorableInfo,
                // something else
            ]
        );
    }
}
