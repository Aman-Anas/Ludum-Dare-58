namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

// [MemoryPackable]
// public readonly partial record struct ToggleUpdate(ulong EntityID, bool State, int Index)
//     : IEntityUpdate<IToggleable>
// {
//     public MessageType MessageType => MessageType.DoorUpdate;

//     public void OnClient(ClientManager client) =>
//         this.UpdateClientEntity<ToggleUpdate, IToggleable>(client);

//     public void OnServer(NetPeer peer, ServerManager server) =>
//         this.UpdateServerEntity<ToggleUpdate, IToggleable>(peer);

//     public void UpdateEntity(INetEntity<IToggleable> entity)
//     {
//         entity.Data.Toggles[Index].State = State;
//     }
// }
