// namespace Game.Entities;

// using System;
// using System.Collections.Generic;
// using Game.Networking;
// using Godot;
// using LiteNetLib;
// ;

// [MessagePackObject]
// public record EntityInitializer(
//     [property: Key(0)] uint EntityID,
//     [property: Key(1)] string ClientScene,
//     [property: Key(2)] EntityClientData EntityData
// ) : INetMessage
// {
//     public void OnReceived(NetPeer peer, ServerManager server, ClientManager client)
//     {
//         if (client != null) { }
//         // GD.Print("hi");
//         // GD.Print(
//         //     $"Received client initializer {PlayerEntityData.Position} and orient {PlayerEntityData.Rotation}"
//         // );
//         // GD.Print($"spawnScene {PlayerEntityData.ClientScene}");
//     }

//     public MessageType GetMessageType() => MessageType.ClientInitializer;
// }
// // [property: Key(0)] PlayerEntityData PlayerEntityData,
// // [property: Key(1)] bool HasExteriorScene,
// // [property: Key(2)] Dictionary<uint, EntityData> SectorEntityData
