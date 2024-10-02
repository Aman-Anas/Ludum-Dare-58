namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct DoorUpdate(uint EntityID, bool DoorState) : IEntityUpdate
{
    public readonly MessageType GetMessageType() => MessageType.DoorUpdate;

    public readonly void OnClient(ClientManager client) => this.UpdateEntity(client);

    public readonly void OnServer(NetPeer peer, ServerManager server) => this.UpdateEntity(peer);

    public void UpdateEntity(INetEntity entity)
    {
        ((IDoor)entity.Data).DoorState = DoorState;
    }
}
