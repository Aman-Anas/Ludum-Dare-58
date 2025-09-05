namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[GlobalClass]
public partial class Food : StaticBody3D, INetEntity<FoodData>
{
    [Export]
    public FoodData Data { get; set; } = null!;

    [Export]
    MeshInstance3D textMesh = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (FoodData)value;
    }

    public override void _Ready()
    {
        ((TextMesh)textMesh.Mesh).Text = Data.StorableInfo.StackClass;
    }
}
