namespace Game.Entities;

using System;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
[GlobalClass]
public partial class BurgerData : EntityData, IStorable
{
    [Export]
    public string StackName { get; set; } = "burger_standard";

    public bool Stackable { get; set; } = true;
    public int MaxStack { get; set; } = 20;

    [Export]
    public string IconPath { get; set; } =
        @"res://addons/kenney_prototype_textures/red/texture_11.png";
}
