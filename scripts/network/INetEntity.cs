namespace Game.Networking;

using System;
using System.Collections.Generic;
using Game.Entities;
using Game.World.Data;
using Godot;
using LiteNetLib;
using MemoryPack;

public interface INetEntity
{
    public Vector3 Position { get; set; }
    public Vector3 Rotation { get; set; }
    public EntityData Data { get; set; }
    public Node3D GetNode() => (Node3D)this;
}

public interface INetEntity<T> : INetEntity
    where T : EntityData
{
    public new T Data { get; set; }
}

public static class EntityExtensions
{
    /// <summary>
    /// Helper to update this entity's transform to the sector (and update the server)
    /// This will only affect client -> server entity if the player 'owns' that entity
    /// </summary>
    /// <param name="entity">The entity to update the transform of</param>
    /// <typeparam name="T">Entity type</typeparam>
    public static void UpdateTransform<T>(this INetEntity<T> entity)
        where T : EntityData
    {
        var transformUpdate = new TransformUpdate(
            entity.Data.EntityID,
            entity.Position,
            entity.Rotation
        );

        entity.Data.SendMessage(transformUpdate);
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

    /// <summary>
    /// I hope I don't need this
    /// </summary>
    public static INetEntity<EntityData> GetGenericEntity<T>(this INetEntity<T> entity)
        where T : EntityData
    {
        return (INetEntity<EntityData>)entity;
    }
}
