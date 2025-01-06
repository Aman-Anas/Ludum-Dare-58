namespace Game.Entities;

using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public partial record SpawnEntity(EntityData Data) : INetMessage
{
    public MessageType MessageType => MessageType.SpawnEntity;

    public void OnClient(ClientManager client)
    {
        client.SpawnEntity(Data);
    }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        // Server should send spawn to client
        return;
    }
}
