namespace Game.Entities;

using System;
using Game.Networking;
using Game.World.Data;
using LiteNetLib;
using MemoryPack;

// [MemoryPackable]
// public readonly partial record struct BasicAnimUpdate(ulong EntityID, byte CurrentAnim)
//     : IEntityUpdate<IBasicAnim>
// {
//     public MessageType MessageType => MessageType.BasicAnimUpdate;

//     public void OnClient(ClientManager client) =>
//         this.UpdateClientEntity<BasicAnimUpdate, IBasicAnim>(client);

//     public void OnServer(NetPeer peer, ServerManager server)
//     {
//         if (!peer.OwnsEntity(EntityID))
//             return;

//         this.UpdateServerEntity<BasicAnimUpdate, IBasicAnim>(peer);
//     }

//     public void UpdateEntity(INetEntity<IBasicAnim> entity)
//     {
//         entity.Data.CurrentAnim = CurrentAnim;
//     }
// }
