// namespace Game.Entities;

// using System;
// using Game.Networking;
// using Godot;
// ;

// [MessagePackObject]
// public class PropData : EntityData
// {
//     public override void SpawnClientEntity(
//         Vector3 position,
//         Vector3 orientation,
//         ClientManager client
//     ) => client.SpawnEntity<PropServerClient>(this);

//     public override void SpawnServerEntity(
//         Vector3 position,
//         Vector3 orientation,
//         ServerManager server
//     ) => server.RespawnEntity<PropServerClient>(this);
// }

// /// <summary>
// /// Since prop behavior is same on both server and client (there is none lol), we can just
// /// use the same class.
// /// </summary>
// public partial class PropServerClient : StaticBody3D, INetEntity
// {
//     PropData _data;
//     public EntityData Data
//     {
//         get => _data;
//         set => _data = (PropData)value;
//     }
// }
