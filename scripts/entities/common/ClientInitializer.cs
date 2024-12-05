namespace Game.Entities;

using System.Collections.Generic;
using Game.Networking;
using Game.Terrain;
using Game.World.Data;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public partial record ClientInitializer(
    uint PlayerEntityID,
    Dictionary<uint, EntityData> EntitiesData,
    SectorParameters TerrainParameters
) : INetMessage
{
    public MessageType GetMessageType() => MessageType.ClientInitializer;

    public void OnClient(ClientManager client)
    {
        client.InitializeScene(this);
    }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        return; // nothing needs to happen
    }
}
