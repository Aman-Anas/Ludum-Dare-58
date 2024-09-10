namespace Game.World.Data;

using System;
using System.Collections.Generic;
using Game.Networking;
using Game.Terrain;
using Godot;
using LiteNetLib;
using MemoryPack;

// ;
// using static Game.Terrain.ChunkData;

[MemoryPackable]
public partial class ServerData
{
    // Store player login data (username -> pword)
    public Dictionary<string, string> LoginData { get; set; } = [];

    // Store general player data (username -> data)
    public Dictionary<string, PlayerData> PlayerData { get; set; } = [];

    // Dynamic store of active (logged in) players
    [MemoryPackIgnore]
    public Dictionary<string, NetPeer> ActivePlayers { get; set; } = [];

    [MemoryPackIgnore]
    public Dictionary<NetPeer, LivePlayerState> CurrentPlayerState { get; set; } = [];

    public uint EntityIDCounter { get; set; } = 0;

    // Store metadata about all sector names etc (key is sector ID)
    public Dictionary<uint, SectorMetadata> SectorMetadata { get; set; } = [];

    // Map entityID -> the sectorID it's located in
    public Dictionary<uint, uint> EntityDirectory { get; set; } = [];

    // Store objects, chunks, etc for each sector. This should be dynamically loaded from file.
    // Ignore serializing to ensure only loaded areas are in this dict
    [MemoryPackIgnore]
    public Dictionary<uint, Sector> SectorWorldData { get; set; } = [];
}

// Data to be stored about a player that is live
public record LivePlayerState(NetPeer Peer, Sector CurrentSector, PlayerData Data);

[MemoryPackable]
public partial class SectorMetadata
{
    public uint SectorID { get; set; }

    public string SectorName { get; set; }
}

[MemoryPackable]
public partial class Sector
{
    // [Key(0)]
    public uint SectorID { get; set; }

    // [Key(1)]
    public bool ContainsTerrain { get; set; }

    // [Key(2)]
    public Dictionary<ChunkID, byte[]> ChunkData { get; set; }

    // [Key(3)]
    public TerrainParameters Parameters { get; set; }

    // [Key(4)]
    public Dictionary<uint, EntityData> EntitiesData { get; set; }

    // [Key(5)]
    public Dictionary<uint, SecretData> EntitySecrets { get; set; }

    [MemoryPackIgnore]
    public Dictionary<uint, INetEntity> Entities { get; set; } = [];

    [MemoryPackIgnore]
    public ServerData WorldDataRef { get; private set; }

    [MemoryPackIgnore]
    public Node3D SectorSceneRoot { get; private set; }

    public void ReloadArea(ServerData worldData, Node3D sectorRoot)
    {
        WorldDataRef = worldData;
        SectorSceneRoot = sectorRoot;

        foreach (var data in EntitiesData.Values)
        {
            // If there is some stored secret data, link it to our entity
            if (EntitySecrets.TryGetValue(data.EntityID, out var secrets))
            {
                data.PutSecrets(secrets);
            }

            InstanceEntity(data);
        }
    }

    public void SpawnNewEntity<T>(Vector3 position, Vector3 rotation, T data)
        where T : EntityData
    {
        data.Position = position;
        data.Rotation = rotation;

        // Generate Entity ID
        data.EntityID = WorldDataRef.EntityIDCounter;
        WorldDataRef.EntityIDCounter++;

        WorldDataRef.EntityDirectory[data.EntityID] = SectorID;
        EntitiesData[data.EntityID] = data;

        // When spawning a new entity, we can use GetSecrets to
        // get the secret data from the entity resource
        var secrets = data.GetSecrets();

        // Usually there won't be any secret data
        if (secrets != null)
        {
            EntitySecrets[data.EntityID] = secrets;
        }

        InstanceEntity(data);
    }

    public void DestroyEntity(uint entityID)
    {
        // Remove the entity (and its secrets data if it exists)
        _ = EntitiesData.Remove(entityID);
        _ = EntitySecrets.Remove(entityID);

        // Remove entity if it's instanced
        if (Entities.Remove(entityID, out var entity))
        {
            var node = entity.GetNode();
            node.QueueFree();
        }
    }

    /// <summary>
    /// Helper method to just instance an entity into the sector's scene
    /// </summary>
    void InstanceEntity(EntityData data)
    {
        var newInstance = data.GetInstance(true);
        SectorSceneRoot.AddChild(newInstance.GetNode());

        Entities[data.EntityID] = newInstance;
    }
}
