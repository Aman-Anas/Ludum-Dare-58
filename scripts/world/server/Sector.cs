namespace Game.World.Data;

using System.Collections.Generic;
using Game.Entities;
using Game.Networking;
using Game.Terrain;
using Godot;
using LiteNetLib;
using MemoryPack;

[MemoryPackable]
public partial record struct SectorParameters(
    bool HasTerrain,
    float NoiseScale,
    Vector3 NoiseOffset
);

[MemoryPackable]
public partial class Sector
{
    /// <summary>
    /// Used for loading a sector from file
    /// </summary>
    [MemoryPackConstructor]
    public Sector() { }

    /// <summary>
    /// Used for generating a new Sector
    /// </summary>
    public Sector(uint id, SectorParameters sectorParameters, SubViewport root, ServerData data)
    {
        this.SectorID = id;
        this.Parameters = sectorParameters;
        this.SectorSceneRoot = root;
        this.WorldData = data;
    }

    /// <summary>
    /// ID of sector in server data
    /// </summary>
    public uint SectorID { get; set; }

    // Terrain and world gen data
    public bool ContainsTerrain { get; set; }

    public Dictionary<ChunkID, byte[]> ChunkData { get; set; } = [];

    public SectorParameters Parameters { get; set; } = new();

    // Entity data
    public Dictionary<uint, EntityData> EntitiesData { get; set; } = [];

    public Dictionary<uint, SecretData> EntitySecrets { get; set; } = [];

    [MemoryPackIgnore]
    public Dictionary<uint, INetEntity> Entities { get; set; } = [];

    // List of players in area (to broadcast messages to)
    [MemoryPackIgnore]
    public List<NetPeer> Players { get; set; } = [];

    // Reference to world data, so we can update "global" world things (entity counter, directory)
    [MemoryPackIgnore]
    public ServerData WorldData { get; set; }

    [MemoryPackIgnore]
    public SubViewport SectorSceneRoot { get; private set; }

    public void EchoToSector<T>(
        T message,
        DeliveryMethod method = DeliveryMethod.Unreliable,
        NetPeer ignorePeer = null
    )
        where T : INetMessage
    {
        var writer = NetMessageUtil.EncodeNetMessage(message);
        foreach (var player in Players)
        {
            if (player != ignorePeer)
                player.Send(writer, method);
        }
    }

    public void ReloadArea(SubViewport sectorRoot)
    {
        SectorSceneRoot = sectorRoot;

        foreach (var data in EntitiesData.Values)
        {
            data.CurrentSector = this;

            // If there is some stored secret data, link it to our entity
            if (EntitySecrets.TryGetValue(data.EntityID, out var secrets))
            {
                data.Secrets = secrets;
            }

            InstanceEntity(data);
        }
    }

    public void SpawnNewEntity<T>(Vector3 position, Vector3 rotation, T data)
        where T : EntityData
    {
        data.Position = position;
        data.Rotation = rotation;
        data.CurrentSector = this;

        // Generate Entity ID
        data.EntityID = WorldData.EntityIDCounter;
        WorldData.EntityIDCounter++;

        WorldData.EntityDirectory[data.EntityID] = SectorID;
        EntitiesData[data.EntityID] = data;

        // When spawning a new entity, we can use GetSecrets to
        // get the secret data from the entity resource
        var secrets = data.Secrets;

        // Usually there won't be any secret data, if there is, save it
        if (secrets != null)
        {
            EntitySecrets[data.EntityID] = secrets;
        }

        InstanceEntity(data);

        EchoToSector(new SpawnEntity(data), DeliveryMethod.ReliableUnordered);
    }

    public void DestroyEntity(uint entityID)
    {
        // Remove the entity (and its secrets data if it exists)
        _ = EntitiesData.Remove(entityID);
        _ = EntitySecrets.Remove(entityID);

        // Remove entity if it's instanced
        if (Entities.Remove(entityID, out var entity))
        {
            EchoToSector(new DestroyEntity(entityID), DeliveryMethod.ReliableUnordered);
            var node = entity.GetNode();
            node.QueueFree();
        }
    }

    /// <summary>
    /// Helper method to just instance an entity into the sector's scene
    /// </summary>
    void InstanceEntity(EntityData data)
    {
        var newInstance = data.SpawnInstance(true);
        SectorSceneRoot.AddChild(newInstance.GetNode());

        Entities[data.EntityID] = newInstance;
    }

    public void Unload()
    {
        foreach (var entity in Entities.Values)
        {
            entity.Data.Position = entity.Position;
            entity.Data.Rotation = entity.Rotation;
            entity.Data.CurrentSector = null;
            entity.Data = null;
            entity.GetNode().QueueFree();
        }
        SectorSceneRoot.QueueFree();
        SectorSceneRoot = null;
    }
}
