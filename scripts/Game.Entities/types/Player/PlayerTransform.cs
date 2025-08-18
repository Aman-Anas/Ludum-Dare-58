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
    ulong EntityID,
    Vector3 Position,
    Vector3 Rotation,
    Vector3 GlobalHeadRotation
) : IEntityUpdate<PlayerEntityData>
{
    public MessageType MessageType => MessageType.PlayerTransform;

    public void OnClient(ClientManager client) =>
        this.UpdateClientEntity<PlayerTransform, PlayerEntityData>(client);

    public void OnServer(NetPeer peer, ServerManager server)
    {
        // Only allow updating transform of entities owned by this user
        if (!peer.OwnsEntity(EntityID))
            return;

        this.UpdateServerEntity<PlayerTransform, PlayerEntityData>(peer);
    }

    public void UpdateEntity(INetEntity<PlayerEntityData> entity)
    {
        // We don't need to update the pos and rot in the data atm,
        // since we can just update it in the data for all
        // entities when we save/unload the sector
        entity.Position = Position;
        entity.Rotation = Rotation;
        entity.Data.HeadRotation = GlobalHeadRotation;
    }
}
