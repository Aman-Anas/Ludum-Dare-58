namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct UsePortal(ulong EntityID, byte PortalID)
    : IEntityUpdate<IPortalHolder>
{
    public MessageType MessageType => MessageType.UsePortal;

    public void OnClient(ClientManager client) { }

    public void OnServer(NetPeer peer, ServerManager server) =>
        this.UpdateServerEntity<UsePortal, IPortalHolder>(peer);

    public void UpdateEntity(INetEntity<IPortalHolder> entity)
    {
        // Set health directly (the update* methods are for emitting this message, so we
        // don't want to call them)
        // ((IPortalHolder)entity.Data).PortalInfo.
    }
}
