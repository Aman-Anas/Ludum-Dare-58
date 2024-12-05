namespace Game.Entities;

using System;
using Game.Networking;
using Game.World.Data;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct BasicAnimUpdate(uint EntityID, byte CurrentAnim)
    : IEntityUpdate
{
    public readonly MessageType GetMessageType() => MessageType.HealthUpdate;

    public readonly void OnClient(ClientManager client) => this.UpdateEntity(client);

    public readonly void OnServer(NetPeer peer, ServerManager server)
    {
        if (!peer.OwnsEntity(EntityID))
            return;

        peer.GetPlayerState().CurrentSector?.EchoToSector(this, ignorePeer: peer);
    }

    public readonly void UpdateEntity(INetEntity entity)
    {
        ((IBasicAnim)entity.Data).CurrentAnim = CurrentAnim;
    }
}
