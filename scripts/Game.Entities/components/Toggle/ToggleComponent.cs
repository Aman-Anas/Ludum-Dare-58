namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface IDoor : IEntityData
{
    public ToggleComponent DoorState { get; }
}

[GlobalClass]
[MemoryPackable]
public partial class ToggleComponent : NetComponent<EntityData>
{
    [Export]
    public bool State { get; set; }

    public event Action? ToggleStateChanged;

    public void Toggle()
    {
        State = !State;
        this.NetUpdate();

        ToggleStateChanged?.Invoke();
    }
}
