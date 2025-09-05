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
    public ulong EntityID { get; set; }
    public Sector CurrentSector { get; set; }
    public ClientManager Client { get; set; }
    public HashSet<ulong> Owners { get; set; }
}

// Event bus shared by all entities and components to make things easier.
public partial class EntityEvents;

[GlobalClass, Icon("res://icon.svg")]
[MemoryPackUnion(0, typeof(DestructibleDoorData))]
[MemoryPackUnion(1, typeof(PhysicsProjectileData))]
[MemoryPackUnion(2, typeof(StaticPropData))]
[MemoryPackUnion(3, typeof(PlayerEntityData))]
[MemoryPackUnion(4, typeof(ItemPickupData))]
[MemoryPackUnion(5, typeof(FoodData))]
[MemoryPackable]
public abstract partial class EntityData : MemoryPackableResource, IEntityData
{
    /// <summary>
    /// Entity ID for this entity's data. NOTE: This is ONLY "set"-enabled for
    /// de-serialization purposes. Should only be set once at entity initialization.
    /// </summary>
    public ulong EntityID { get; set; }

    public Vector3 Position { get; set; }

    public Vector3 Rotation { get; set; }

    /// <summary>
    /// The godot scene for this entity on the server.
    /// This currently gets serialized to client as well, but it's
    /// not a big deal
    /// </summary>
    [Export(PropertyHint.File)]
    public string ServerScene { get; set; } = null!;

    /// <summary>
    /// The godot scene for this entity on the client
    /// </summary>
    [Export(PropertyHint.File)]
    public string ClientScene { get; set; } = null!;

    [MemoryPackIgnore]
    public Sector CurrentSector { get; set; } = null!; // Only accessible on server

    [MemoryPackIgnore]
    public ClientManager Client { get; set; } = null!; // Only accessible on client

    /// <summary>
    /// Flag set to true whenever we're saving/loading to a save file (should be false other times)
    /// </summary>
    /// <value></value>
    public bool InSaveState { get; set; }

    /// <summary>
    /// Set of all usernames allowed to mess with this entity. Used for client packet validation
    /// (so you can't just move around whatever entity you want)
    /// </summary>
    public HashSet<ulong> Owners { get; set; } = [];

    /// <summary>
    /// Method called when an entity is first copied from a resource template.
    /// Useful for converting between Godot [Export] collections/classes and
    ///MemoryPack serializable C# classes
    /// </summary>
    public virtual void OnResourceCopy() { }

    /// <summary>
    /// Called whenever a player joins the area of this entity,
    /// or this entity joins an area containing players.
    /// This method is useful to send setup packets for nonserialized fields and other secret data
    /// </summary>
    ///
    /// <param name="peer">The peer which we just met.</param>
    public virtual void OnMeetPlayer(NetPeer peer) { }

    public INetEntity SpawnInstance(bool onServer)
    {
        string scene = onServer ? ServerScene : ClientScene;
        // GD.Print(scene);
        var newEntity = NetHelper.InstanceFromScene<INetEntity>(scene);
        newEntity.Data = this;
        newEntity.Position = Position;
        newEntity.Rotation = Rotation;

        return newEntity;
    }

    public void DestroyEntity()
    {
        OnDestroy();

        if (CurrentSector == null)
        {
            Client.RemoveEntity(EntityID);
        }
        else
        {
            CurrentSector.RemoveEntity(EntityID);
        }
    }

    public virtual void OnDestroy() { }

    [MemoryPackIgnore]
    public virtual NetComponentRegistry? ComponentRegistry { get; init; }

    public T? GetComponent<T>(uint index)
        where T : class, INetComponent
    {
        if (ComponentRegistry == null)
        {
            return null;
        }
        var components = ComponentRegistry.Components;

        if (index >= components.Length)
        {
            return null;
        }

        return (components[index]) as T;
    }
}

/// <summary>
/// Fancy name for an array of components
/// Needed so that we can make sure they are initialized correctly
/// </summary>
public class NetComponentRegistry
{
    public INetComponent[] Components { get; }

    public NetComponentRegistry(EntityData data, INetComponent[] components)
    {
        this.Components = components;

        for (uint x = 0; x < Components.Length; x++)
        {
            Components[x].Initialize(data, x);
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

        newData.OnResourceCopy();
        return newData;
    }
}
