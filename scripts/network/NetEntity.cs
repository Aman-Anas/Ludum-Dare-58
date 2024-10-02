namespace Game.Networking;

using System;
using System.Collections.Generic;
using Game.Entities;
using Game.World.Data;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface IEntityData
{
    public uint EntityID { get; set; }
    public Sector CurrentSector { get; set; }
    public ClientManager Client { get; set; }
}

[MemoryPackUnion(0, typeof(DestructiblePropData))]
[MemoryPackUnion(1, typeof(PhysicsProjectileData))]
[MemoryPackable]
public abstract partial class EntityData : Resource, IEntityData
{
    /// <summary>
    /// Entity ID for this entity's data. NOTE: This is ONLY "set"-enabled for
    /// /// de-serialization purposes. Should only be set once at entity initialization.
    /// </summary>
    public uint EntityID { get; set; }

    public Vector3 Position { get; set; }

    public Vector3 Rotation { get; set; }

    /// <summary>
    /// The godot scene for this entity on the server.
    /// This currently gets serialized to client as well, but it's
    /// not a big deal
    /// </summary>
    [Export(PropertyHint.File)]
    public string ServerScene { get; set; }

    /// <summary>
    /// The godot scene for this entity on the client
    /// </summary>
    [Export(PropertyHint.File)]
    public string ClientScene { get; set; }

    [MemoryPackIgnore]
    public Sector CurrentSector { get; set; } // Only accessible on server

    [MemoryPackIgnore]
    public ClientManager Client { get; set; } // Only accessible on client

    /// <summary>
    /// Set of all usernames allowed to mess with this entity. Used for client packet validation
    /// (so you can't just move around whatever entity you want)
    /// </summary>
    public HashSet<string> Owners { get; set; }

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
    public INetEntity SpawnInstance(bool onServer)
    {
        string scene = onServer ? ServerScene : ClientScene;

        var newEntity = (INetEntity)NetHelper.InstanceFromScene<Node3D>(scene);
        newEntity.Data = this;
        newEntity.Position = Position;
        newEntity.Rotation = Rotation;

        return newEntity;
    }

    public void DestroyEntity()
    {
        if (CurrentSector == null)
        {
            Client.RemoveEntity(EntityID);
        }
        else
        {
            CurrentSector.DestroyEntity(EntityID);
        }
    }
}

public interface INetEntity
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public EntityData Data { get; set; }

    public Node3D GetNode() => (Node3D)this;
}

public static class EntityExtensions
{
    /// <summary>
    /// Update this entity's transform to everyone else in the sector (and update the server)
    /// This will only affect client -> server entity if the player 'owns' that entity
    /// </summary>
    /// <param name="entity">The entity to update the transform of</param>
    /// <typeparam name="T">Entity type</typeparam>
    public static void UpdateTransform<T>(this T entity)
        where T : INetEntity
    {
        var transformUpdate = new TransformUpdate(
            entity.Data.EntityID,
            entity.Position,
            entity.Rotation
        );

        if (entity.Data.CurrentSector == null)
        {
            entity.Data.Client.ServerLink.EncodeAndSend(transformUpdate);
        }
        else
        {
            entity.Data.CurrentSector.EchoToSector(transformUpdate);
        }
    }

    public static void SendMessage<TData, TMessage>(this TData Data, TMessage message)
        where TData : IEntityData
        where TMessage : INetMessage
    {
        if (Data.CurrentSector == null)
        {
            Data.Client.ServerLink.EncodeAndSend(message);
        }
        else
        {
            Data.CurrentSector.EchoToSector(message);
        }
    }
}

/// <summary>
/// SecretData is stored separately from entity data when serialized
/// to keep "secret" entity server-side data from being sent to the client.
/// </summary>
// [MemoryPackUnion(0, typeof(DestructiblePropData))]
// [MemoryPackable]
public abstract partial class SecretData : Resource;
