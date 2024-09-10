namespace Game.Networking;

using System;
using Game.Entities;
using Game.World.Data;
using Godot;
using LiteNetLib;
using MemoryPack;

public static class NetEntityUtil
{
    public static EntityData GetEntityData(uint id, ServerManager server, ClientManager client)
    {
        // if (client != null)
        // {
        return client.EntitiesData[id];
        // }
    }

    // Use to spawn an entity into the world
}

// [Union(0, typeof(PropData))]
[MemoryPackUnion(1, typeof(DestructiblePropData))]
[MemoryPackable(SerializeLayout.Explicit)]
public abstract partial class EntityData : Resource //, IEntityData
{
    /// <summary>
    /// Entity ID for this entity's data. NOTE: This is ONLY "set"-enabled for
    /// /// de-serialization purposes. Should only be set once at entity initialization.
    /// </summary>
    // [Key(0)]
    public uint EntityID { get; set; }

    // [Key(1)]
    public Vector3 Position { get; set; }

    // [Key(2)]
    public Vector3 Rotation { get; set; }

    // [Key(3)]
    [Export(PropertyHint.File)]
    public string ServerScene { get; set; }

    // [Key(4)]
    [Export(PropertyHint.File)]
    public string ClientScene { get; set; }

    // For secrets, use these two flags to avoid unneeded serialization
    // and allow resource storage
    // [IgnoreMember]
    // [Export]
    public virtual SecretData GetSecrets() => null;

    /// <summary>
    /// Re-assign the secrets (after serialization)
    /// </summary>
    public virtual void PutSecrets(SecretData secrets) { }

    /// <summary>
    /// Get an instantiated entity
    /// </summary>
    public INetEntity GetInstance(bool onServer)
    {
        string scene = onServer ? ServerScene : ClientScene;

        var newEntity = (INetEntity)NetHelper.InstanceFromScene<Node3D>(scene);
        newEntity.Data = this;
        newEntity.Position = Position;
        newEntity.Rotation = Rotation;

        return newEntity;
    }
}

public interface INetEntity
{
    public uint EntityID { get; set; }
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public EntityData Data { set; }

    public Node3D GetNode() => (Node3D)this;
}

/// <summary>
/// SecretData is stored separately from entity data when serialized
/// to keep "secret" entity server-side data from being sent to the client.
/// </summary>
// [MemoryPackUnion(0, typeof(DestructiblePropData))]
// [MemoryPackable]
public abstract partial class SecretData : Resource;
