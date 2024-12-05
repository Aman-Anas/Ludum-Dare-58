namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

/// <summary>
/// Custom entity update for players every physics frame
/// </summary>
[MemoryPackable]
public readonly partial record struct PlayerTransform(
    uint EntityID,
    Vector3 Position,
    Vector3 Rotation,
    Vector3 GlobalHeadRotation
) : IEntityUpdate
{
    public readonly MessageType GetMessageType() => MessageType.PlayerTransform;

    public readonly void OnClient(ClientManager client) => this.UpdateEntity(client);

    public readonly void OnServer(NetPeer peer, ServerManager server)
    {
        // Only allow updating transform of entities owned by this user
        if (!peer.OwnsEntity(EntityID))
            return;

        this.UpdateEntity(peer);
    }

    public readonly void UpdateEntity(INetEntity entity)
    {
        // We don't need to update the pos and rot in the data atm,
        // since we can just update it in the data for all
        // entities when we save/unload the sector
        entity.Position = Position;
        entity.Rotation = Rotation;
        ((PlayerEntityData)entity.Data).HeadRotation = GlobalHeadRotation;
    }
}
