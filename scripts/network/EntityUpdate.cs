namespace Game.Networking;

using System;
using System.Buffers;
using System.Collections.Concurrent;
using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Game.Entities;
using Game.World.Data;
using Godot;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;
using static NetHelper;

/// <summary>
/// <para>Helper interface to define a "entity update" type of message</para>
/// <para>
/// This makes it easy to send small bits of data between entities and their data.
/// For example, an entity data that implements IHealth could use the HealthUpdate message
/// to send a HP update to the server or to a client
/// </para>
/// </summary>

public interface IEntityUpdate<in T> : INetMessage
    where T : IEntityData
{
    public ulong EntityID { get; init; }
    void UpdateEntity(INetEntity<T> entity);
}

public static class EntityUpdateExt
{
    /// <summary>
    /// Helper extension method to call UpdateEntity() for the correct entity on the server
    /// </summary>
    public static void UpdateServerEntity<TMessage, TData>(
        this TMessage message,
        NetPeer peer,
        bool echo = true,
        DeliveryMethod echoMethod = DeliveryMethod.Unreliable
    )
        where TMessage : IEntityUpdate<TData>
        where TData : IEntityData
    {
        if (peer.GetLocalEntity(message.EntityID) is INetEntity<TData> entity)
        {
            message.UpdateEntity(entity);

            if (echo)
                peer.GetPlayerState().CurrentSector.EchoToSector(message, echoMethod, peer);
        }
    }

    public static void UpdateClientEntity<TMessage, TData>(
        this TMessage message,
        ClientManager client
    )
        where TMessage : IEntityUpdate<TData>
        where TData : IEntityData
    {
        if (client.Entities[message.EntityID] is INetEntity<TData> entity)
        {
            message.UpdateEntity(entity);
        }
    }
}
