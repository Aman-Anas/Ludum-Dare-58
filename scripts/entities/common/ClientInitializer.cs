namespace Game.Entities;

using System.Collections.Generic;
using Game.Networking;
using Game.Terrain;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public partial record ClientInitializer(
    Dictionary<uint, EntityData> EntitiesData,
    TerrainParameters TerrainParameters
) : INetMessage
{
    public MessageType GetMessageType() => MessageType.ClientInitializer;

    public void OnClient(ClientManager client)
    {
        // Clear out all old entities.
        // TODO: Maybe call a method on the client manager to do this and cleanup terrain stuff

        foreach (var entityData in client.EntitiesData.Values)
        {
            entityData.DestroyEntity();
        }

        foreach (var data in EntitiesData.Values)
        {
            client.SpawnEntity(data);
        }
    }

    public void OnServer(NetPeer peer, ServerManager server)
    {
        return; // nothing needs to happen
    }
}
