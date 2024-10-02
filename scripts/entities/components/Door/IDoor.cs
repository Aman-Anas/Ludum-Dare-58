namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface IDoor : IEntityData
{
    public bool DoorState { get; set; }

    public virtual void Toggle()
    {
        this.SendMessage(new DoorUpdate(EntityID, DoorState));
    }
}
