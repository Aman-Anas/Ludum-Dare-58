namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[GlobalClass]
public partial class Burger : StaticBody3D, INetEntity<BurgerData>
{
    [Export]
    public BurgerData Data { get; set; } = null!;

    [Export]
    MeshInstance3D textMesh = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (BurgerData)value;
    }

    public override void _Ready()
    {
        ((TextMesh)textMesh.Mesh).Text = Data.StackName;
    }
}
