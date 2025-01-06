namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct DoorUpdate(ulong EntityID, bool DoorState)
    : IEntityUpdate<IDoor>
{
    public MessageType MessageType => MessageType.DoorUpdate;

    public void OnClient(ClientManager client) =>
        this.UpdateClientEntity<DoorUpdate, IDoor>(client);

    public void OnServer(NetPeer peer, ServerManager server) =>
        this.UpdateServerEntity<DoorUpdate, IDoor>(peer);

    public void UpdateEntity(INetEntity<IDoor> entity)
    {
        entity.Data.DoorState = DoorState;
    }
}
