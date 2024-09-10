// namespace Game.Entities;

// using Game.Networking;
// using Godot;
// using LiteNetLib;
// ;

// [MessagePackObject]
// public record struct EntityTransformUpdate(
//     [property: Key(0)] uint EntityID,
//     [property: Key(1)] Vector3 Position,
//     [property: Key(2)] Vector3 Orientation
// ) : INetMessage
// {
//     public readonly MessageType GetMessageType() => MessageType.EntityTransformUpdate;

//     public void OnReceived(NetPeer peer, ServerManager server, ClientManager client) { }
// }
