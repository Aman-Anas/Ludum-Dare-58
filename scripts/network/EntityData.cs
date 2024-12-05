namespace Game.Networking;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using Game.Entities;
using Game.World.Data;
using Godot;
using LiteNetLib;
using MemoryPack;
using MessagePack;
using Utilities.Data;

public interface IEntityData
{
    public uint EntityID { get; set; }
    public Sector CurrentSector { get; set; }
    public ClientManager Client { get; set; }
    public SecretData Secrets { get; set; }
}

[GlobalClass]
[MemoryPackUnion(0, typeof(DestructiblePropData))]
[MemoryPackUnion(1, typeof(PhysicsProjectileData))]
[MemoryPackUnion(2, typeof(StaticPropData))]
[MemoryPackUnion(3, typeof(PlayerEntityData))]
[MemoryPackable]
public abstract partial class EntityData : MemoryPackableResource, IEntityData
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
    public HashSet<string> Owners { get; set; } = [];

    // For secrets, use these two flags to avoid unneeded serialization
    // and allow resource storage
    // [IgnoreMember]
    // [Export]
    [MemoryPackIgnore]
    public virtual SecretData Secrets
    {
        get { return null; }
        set { }
    }

    // Method called when an entity is first copied from a resource template.
    // Useful for converting between Godot [Export] collections/classes and
    // MemoryPack serializable C# classes
    public virtual void OnResourceCopy() { }

    public INetEntity SpawnInstance(bool onServer)
    {
        string scene = onServer ? ServerScene : ClientScene;
        GD.Print(scene);
        var newEntity = NetHelper.InstanceFromScene<INetEntity>(scene);
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

public static class EntityDataExtensions
{
    /// <summary>
    /// Get an instantiated entity
    /// /// </summary>
    public static T CopyFromResource<T>(this T data)
        where T : EntityData
    {
        T newData = (T)data.Duplicate(true);
        if (data.Secrets != null)
            newData.Secrets = (SecretData)data.Secrets.Duplicate(true);

        newData.OnResourceCopy();
        return newData;
    }
}
