namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface IToggleable : IEntityData
{
    public ToggleComponent[] Toggles { get; }
}

[MemoryPackable]
public partial class ToggleComponent : Component<IToggleable>
{
    public bool State { get; set; }

    public event Action? ToggleStateChanged;

    public void Toggle()
    {
        State = !State;
        data.SendMessage(new ToggleUpdate(data.EntityID, State, index));

        ToggleStateChanged?.Invoke();
    }
}
