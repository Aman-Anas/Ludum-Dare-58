namespace Game.Networking;

using System;
using System.Buffers;
using System.Diagnostics.CodeAnalysis;
using Game.Entities;
using Game.World.Data;
using LiteNetLib;
using LiteNetLib.Utils;
using MemoryPack;
using static NetHelper;

public interface INetMessage
{
    public void OnServer(NetPeer peer, ServerManager server);
    public void OnClient(ClientManager client);
    public MessageType GetMessageType();
}

// Adding a new message type:
// Add a type enum here, and add it to the
// processing switch below.
public enum MessageType : uint
{
    ClientInitializer,
    TransformUpdate,
    DestroyEntity,
    SpawnEntity,

    // Component updates
    HealthUpdate,
    DoorUpdate
}

public static class NetMessageUtil
{
    // Process packets in a way that prevents any struct boxing
    // To do so, we have to make sure types are known statically.
    // This switch statement is the easiest (and most performant) way. Ideally, we could generate it with
    // source generation but for now manually adding packets is easy enough.
    public static void SwitchPacket(
        NetDataReader reader,
        NetPeer peer,
        ServerManager server,
        ClientManager client
    )
    {
        // (also for all the haters, a switch is O(1) and should compile to a jump table)
        // and also we have no way to constant define a dictionary in C# at the moment
        // so this should be the fastest & most non-allocating way
        switch ((MessageType)reader.GetUInt())
        {
            case MessageType.ClientInitializer:
                ProcessNetMessage<ClientInitializer>(reader, peer, server, client);
                break;
            case MessageType.TransformUpdate:
                ProcessNetMessage<TransformUpdate>(reader, peer, server, client);
                break;
            case MessageType.DestroyEntity:
                ProcessNetMessage<DestroyEntity>(reader, peer, server, client);
                break;
            case MessageType.SpawnEntity:
                ProcessNetMessage<SpawnEntity>(reader, peer, server, client);
                break;
            case MessageType.HealthUpdate:
                ProcessNetMessage<HealthUpdate>(reader, peer, server, client);
                break;
            case MessageType.DoorUpdate:
                ProcessNetMessage<DoorUpdate>(reader, peer, server, client);
                break;
        }
    }

    // Process a network message with known type (use the where opcode to prevent boxing :D)
    // TODO: Investigate using MemoryPack instead of MessagePack for network data.
    public static void ProcessNetMessage<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T
    >(NetDataReader reader, NetPeer peer, ServerManager server, ClientManager client)
        where T : INetMessage
    {
        T data = DecodeData<T>(reader);

        if (server != null)
        {
            data.OnServer(peer, server);
        }
        else
        {
            data.OnClient(client);
        }
    }

    // Encode a network message to send to a peer. This will encode the message type
    // first as a uint.
    public static NetDataWriter EncodeNetMessage<T>(T message)
        where T : INetMessage
    {
        BufferedNetDataWriter writer = new();
        writer.Put((uint)message.GetMessageType());
        MemoryPackSerializer.Serialize(writer, message);
        return writer;
    }

    // Extension methods for cleaner message sending and encoding

    public static void EncodeAndSend<T>(
        this NetPeer peer,
        T message,
        DeliveryMethod method = DeliveryMethod.Unreliable
    )
        where T : INetMessage
    {
        peer.Send(EncodeNetMessage(message), method);
    }

    /// <summary>
    /// Helper extension method to call UpdateEntity() for the correct entity on the server
    /// </summary>
    public static void UpdateEntity<T>(this T message, NetPeer peer)
        where T : IEntityUpdate
    {
        message.UpdateEntity(peer.GetLocalEntity(message.EntityID));
    }

    /// <summary>
    /// Helper extension method to call UpdateEntity() for the correct entity on the client
    /// </summary>
    public static void UpdateEntity<T>(this T message, ClientManager client)
        where T : IEntityUpdate
    {
        message.UpdateEntity(client.Entities[message.EntityID]);
    }

    /// <summary>
    /// Helper extension method to get some entity's data on the server
    /// </summary>
    public static INetEntity GetLocalEntity(this NetPeer peer, uint entityID)
    {
        var currentSector = peer.GetPlayerState().CurrentSector;
        return currentSector.Entities[entityID];
    }

    public static bool OwnsEntity(this NetPeer peer, uint entityID)
    {
        var foundEntity = peer.GetLocalEntity(entityID);
        return foundEntity.Data.Owners.Contains(peer.GetPlayerState().Username);
    }
}

public class BufferedNetDataWriter : NetDataWriter, IBufferWriter<byte>
{
    const int MIN_BUFFER_SIZE = 56;

    public void Advance(int count)
    {
        _position += count;
    }

    public Memory<byte> GetMemory(int sizeHint = 0)
    {
        if (sizeHint == 0)
            sizeHint = MIN_BUFFER_SIZE;

        ResizeIfNeed(_position + sizeHint);
        return _data.AsMemory(_position, sizeHint);
    }

    public Span<byte> GetSpan(int sizeHint = 0)
    {
        if (sizeHint == 0)
            sizeHint = MIN_BUFFER_SIZE;

        ResizeIfNeed(_position + sizeHint);
        return _data.AsSpan(_position, sizeHint);
    }
}

/// <summary>
/// <para>Helper class to define a "entity update" type of message</para>
/// <para>
/// This makes it easy to send small bits of data between entities and their data.
/// For example, an entity data that implements IHealth could use the HealthUpdate message
/// to send a HP update to the server or to a client
/// </para>
/// </summary>
public interface IEntityUpdate : INetMessage
{
    public uint EntityID { get; init; }
    void UpdateEntity(INetEntity entity);
}
