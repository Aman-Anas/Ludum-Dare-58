namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public partial record SpawnEntity(EntityData Data) : INetMessage
{
    public MessageType GetMessageType() => MessageType.SpawnEntity;

    public void OnClient(ClientManager client)
    {
        client.EventQueue.Enqueue(() => client.SpawnEntity(Data));
    }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        // server should send spawn to client
        return;
    }
}
