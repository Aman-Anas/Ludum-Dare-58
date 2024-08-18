namespace Game.Networking;

using System;
using System.Buffers;
using LiteNetLib;
using LiteNetLib.Utils;
using MessagePack;
using static NetUtils;

public interface INetMessage
{
    public void OnReceived(NetPeer peer, ServerManager server, ClientManager client);
    public MessageType GetMessageType();
}

// Adding a new message type:
// Add a type enum here, and add it to the
// processing switch below.
public enum MessageType
{
    ClientInitializer
}

public static class NetMessageProcessor
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
        }
    }

    // Process a network message with known type (use the where opcode to prevent boxing :D)
    // TODO: Investigate using MemoryPack instead of MessagePack for network data.
    public static void ProcessNetMessage<T>(
        NetDataReader reader,
        NetPeer peer,
        ServerManager server,
        ClientManager client
    )
        where T : INetMessage
    {
        DecodeData<T>(reader).OnReceived(peer, server, client);
    }

    // Encode a network message to send to a peer. This will encode the message type
    // first as a uint.
    public static NetDataWriter EncodeNetMessage<T>(T message)
        where T : INetMessage
    {
        BufferedNetDataWriter writer = new();
        writer.Put((uint)message.GetMessageType());
        MessagePackSerializer.Serialize(writer, message);
        return writer;
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
