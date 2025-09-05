namespace Game.Entities;

using System;
using Game.Networking;
using LiteNetLib;
using MemoryPack;

// [MemoryPackable]
// public readonly partial record struct HealthUpdate(ulong EntityID, int NewHealth)
//     : IEntityUpdate<IHealth>
// {
//     public MessageType MessageType => MessageType.HealthUpdate;

//     public void OnClient(ClientManager client) =>
//         this.UpdateClientEntity<HealthUpdate, IHealth>(client);

//     public void OnServer(NetPeer peer, ServerManager server) { }

//     public void UpdateEntity(INetEntity<IHealth> entity)
//     {
//         entity.Data.HealthState.InternalHealth = NewHealth;
//     }
// }
