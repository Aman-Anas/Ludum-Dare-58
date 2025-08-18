namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public readonly partial record struct TransformUpdate(
    ulong EntityID,
    Vector3 Position,
    Vector3 Rotation
) : IEntityUpdate<EntityData>
{
    public MessageType MessageType => MessageType.TransformUpdate;

    public void OnClient(ClientManager client) =>
        this.UpdateClientEntity<TransformUpdate, EntityData>(client);

    public void OnServer(NetPeer peer, ServerManager server)
    {
        // Only allow updating transform of entities owned by this user
        if (!peer.OwnsEntity(EntityID))
            return;

        this.UpdateServerEntity<TransformUpdate, EntityData>(peer);
    }

    public void UpdateEntity(INetEntity<EntityData> entity)
    {
        // We don't need to update the pos and rot in the data atm,
        // since we can just update it in the data for all
        // entities when we save/unload the sector
        entity.Position = Position;
        entity.Rotation = Rotation;
    }
}
