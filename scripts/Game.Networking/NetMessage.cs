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

public interface INetMessage
{
    public void OnServer(NetPeer peer, ServerManager server);
    public void OnClient(ClientManager client);
    public MessageType MessageType { get; }
}

// Adding a new message type:
// Add a type enum here, and add it to the
// processing switch below.
public enum MessageType : ushort
{
    // Common
    ClientInitializer,
    TransformUpdate,
    DestroyEntity,
    SpawnEntity,

    // Component updates
    ComponentOverwriteUpdate,

    // HealthUpdate,
    // DoorUpdate,
    // BasicAnimUpdate,
    StorageAction,

    // StorageUpdate,
    StorageDrop,

    // Custom transform update for players
    PlayerTransform,
    UsePortal,
}

public static class NetMessageUtil
{
    static readonly ConcurrentBag<BufferedNetDataWriter> writerPool = [];

    // Process packets in a way that prevents any struct boxing
    // To do so, we have to make sure types are known statically.
    // This switch statement is the easiest (and most performant) way. Ideally, we could generate it with
    // source generation but for now manually adding packets is easy enough.
    public static void SwitchPacket(
        NetDataReader reader,
        NetPeer peer,
        ServerManager? server,
        ClientManager? client
    )
    {
        // (also for all the haters, a switch is O(1) and should compile to a jump table)
        // and also we have no way to constant define a dictionary in C# at the moment
        // so this should be fine for now
        switch ((MessageType)reader.GetUShort())
        {
            case MessageType.ClientInitializer:
                ProcessNetMessage<ClientInitializer>(reader, peer, server, client);
                break;
            case MessageType.TransformUpdate:
                ProcessNetMessage<TransformUpdate>(reader, peer, server, client);
                break;
            case MessageType.DestroyEntity:
                ProcessNetMessage<RemoveEntity>(reader, peer, server, client);
                break;
            case MessageType.SpawnEntity:
                ProcessNetMessage<SpawnEntity>(reader, peer, server, client);
                break;

            // case MessageType.HealthUpdate:
            //     ProcessNetMessage<HealthUpdate>(reader, peer, server, client);
            //     break;
            // case MessageType.DoorUpdate:
            //     ProcessNetMessage<ToggleUpdate>(reader, peer, server, client);
            //     break;
            // case MessageType.BasicAnimUpdate:
            //     ProcessNetMessage<BasicAnimUpdate>(reader, peer, server, client);
            //     break;
            case MessageType.StorageAction:
                ProcessNetMessage<StorageAction>(reader, peer, server, client);
                break;
            // case MessageType.StorageUpdate:
            //     ProcessNetMessage<StorageUpdate>(reader, peer, server, client);
            //     break;

            case MessageType.PlayerTransform:
                ProcessNetMessage<PlayerTransform>(reader, peer, server, client);
                break;
            default:
                GD.Print("Failed to process message");
                break;
        }
    }

    // Process a network message with known type (use the where opcode to prevent boxing :D)
    public static void ProcessNetMessage<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T
    >(NetDataReader reader, NetPeer peer, ServerManager? server, ClientManager? client)
        where T : INetMessage
    {
        T data = DecodeData<T>(reader);

        if (server != null)
        {
            data.OnServer(peer, server);
        }
        else
        {
            data.OnClient(client!);
        }
    }

    /// <summary>
    /// Encode a network message to send to a peer. This will encode the message type
    /// first as a ushort.
    /// </summary>
    public static BufferedNetDataWriter EncodeNetMessage<T>(T message)
        where T : INetMessage
    {
        // If we have a struct, we can predict how big the array should be
        int initialCapacity = typeof(T).IsValueType ? sizeof(ushort) + Marshal.SizeOf<T>() : 128;

        if (!writerPool.TryTake(out var writer))
        {
            writer = new(initialCapacity);
        }
        else
        {
            writer.Reset(initialCapacity);
        }
        writer.Put((ushort)message.MessageType);
        MemoryPackSerializer.Serialize(writer, message);
        return writer;
    }

    public static void RecycleWriter(this BufferedNetDataWriter writer)
    {
        writerPool.Add(writer);
    }

    // Extension methods for cleaner message sending and encoding
    public static void EncodeAndSend<T>(
        this NetPeer peer,
        T message,
        DeliveryMethod method = DeliveryMethod.Unreliable
    )
        where T : INetMessage
    {
        var writer = EncodeNetMessage(message);
        peer.Send(writer, method);
        writer.RecycleWriter();
    }

    public static INetEntity GetLocalEntity(this NetPeer peer, ulong entityID)
    {
        var currentSector = peer.GetPlayerState().CurrentSector;
        return currentSector.Entities[entityID];
    }

    public static bool OwnsEntity(this NetPeer peer, ulong entityID)
    {
        var foundEntity = peer.GetLocalEntity(entityID);
        return peer.OwnsEntity(foundEntity);
    }

    public static bool OwnsEntity(this NetPeer peer, INetEntity entity)
    {
        var owners = entity.Data.Owners;
        return owners.Contains(0ul) || owners.Contains(peer.GetPlayerState().PlayerID);
    }
}

public class BufferedNetDataWriter(int initialSize = 64)
    : NetDataWriter(true, initialSize),
        IBufferWriter<byte>
{
    readonly int initialCapacity = initialSize;

    public void Advance(int count)
    {
        _position += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint == 0)
            sizeHint = initialCapacity - _position;
        if (sizeHint <= 0)
            sizeHint = 128;

        ResizeIfNeed(_position + sizeHint);
        return _data.AsMemory(_position, sizeHint);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (sizeHint == 0)
            sizeHint = initialCapacity - _position;
        if (sizeHint <= 0)
            sizeHint = 128;

        ResizeIfNeed(_position + sizeHint);
        return _data.AsSpan(_position, sizeHint);
    }
}



// public interface IEntityUpdate : IEntityUpdate<EntityData>
// {
//     void UpdateEntity(INetEntityM entity);
// }
