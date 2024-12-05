namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface IDoor : IEntityData
{
    public bool DoorState { get; set; }
}

public static class DoorExt
{
    public static void ToggleDoor(this IDoor door)
    {
        door.DoorState = !door.DoorState;
        door.SendMessage(new DoorUpdate(door.EntityID, door.DoorState));
    }
}
