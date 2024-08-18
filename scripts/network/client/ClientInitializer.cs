namespace Game.Networking;

using System.Collections.Generic;
using System.Threading.Tasks;
using Godot;
using LiteNetLib;
using MessagePack;

[MessagePackObject]
public readonly record struct ClientInitializer(
    [property: Key(0)] Vector3 Position,
    [property: Key(1)] Vector3 Orientation,
    [property: Key(2)] bool HasExteriorScene
) : INetMessage
{
    public void OnReceived(NetPeer peer, ServerManager server, ClientManager client)
    {
        GD.Print("hi");
        GD.Print($"Received client initializer {Position} and orient {Orientation}");
    }

    public MessageType GetMessageType() => MessageType.ClientInitializer;
}
