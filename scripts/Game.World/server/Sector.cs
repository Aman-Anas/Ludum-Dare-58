namespace Game.World.Data;

using System;
using System.Collections.Generic;
using Game.Entities;
using Game.Networking;
using Game.Terrain;
using Godot;
using LiteNetLib;
using MemoryPack;
using Utilities;

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
    /// Parameterless constructor used for loading a sector from file
    /// </summary>
    [MemoryPackConstructor]
    public Sector() { }

    /// <summary>
    /// Used for generating a new Sector
    /// </summary>
    public Sector(
        uint id,
        SectorParameters sectorParameters,
        SubViewport root,
        ServerData data,
        string? kernelScene = null //TODO:
    )
    {
        this.SectorID = id;
        this.Parameters = sectorParameters;
        this.SectorRoot = root;
        this.ServerData = data;
        this.KernelScene = kernelScene;

        if (kernelScene != null)
        {
            this.ImportFromScene(kernelScene, ignoreEntities: false);
        }
    }

    /// <summary>
    /// Scene to use as a template for this sector.
    /// </summary>
    public string? KernelScene { get; set; }

    /// <summary>
    /// ID of sector in server data
    /// </summary>
    public uint SectorID { get; set; }

    // Terrain and world gen data
    public bool ContainsTerrain { get; set; }

    public Dictionary<Vector3I, byte[]> ChunkData { get; set; } = [];

    public SectorParameters Parameters { get; set; }

    // Entity data TODO: Periodically compress entity IDs
    public Dictionary<ulong, EntityData> EntitiesData { get; set; } = [];

    // Dynamic store of all entities in sector
    [MemoryPackIgnore]
    public Dictionary<ulong, INetEntity> Entities { get; set; } = [];

    // Dynamic mapping of players (playerID -> peer) in area (to broadcast messages to)
    [MemoryPackIgnore]
    public Dictionary<ulong, NetPeer> Players { get; set; } = [];

    // Reference to universal data, so we can update "global" world things (entity counter, directory)
    [MemoryPackIgnore]
    public ServerData ServerData { get; set; } = null!;

    // The root node of this sector, must be assigned externally!
    [MemoryPackIgnore]
    public SubViewport SectorRoot { get; private set; } = null!;

    public void EchoToSector<T>(
        T message,
        DeliveryMethod method = DeliveryMethod.Unreliable,
        NetPeer? ignorePeer = null
    )
        where T : INetMessage
    {
        var writer = NetMessageUtil.EncodeNetMessage(message);
        foreach (var player in Players.Values)
        {
            if (player != ignorePeer)
                player.Send(writer, method);
        }
        writer.RecycleWriter();
    }

    public void EchoToOwners<TMessage>(
        HashSet<ulong> owners,
        TMessage message,
        DeliveryMethod method = DeliveryMethod.Unreliable
    )
        where TMessage : INetMessage
    {
        if (owners.Contains(0L))
        {
            EchoToSector(message, method);
            return;
        }

        var writer = NetMessageUtil.EncodeNetMessage(message);
        foreach (var playerID in owners)
        {
            if (Players.TryGetValue(playerID, out var player))
            {
                player.Send(writer, method);
            }
        }

        writer.RecycleWriter();
    }

    public void ReloadArea(SubViewport sectorRoot)
    {
        SectorRoot = sectorRoot;

        if (KernelScene != null)
        {
            ImportFromScene(KernelScene, ignoreEntities: true);
        }

        foreach (var data in EntitiesData.Values)
        {
            data.CurrentSector = this;

            InstanceEntity(data);
        }
    }

    public void ImportFromScene(
        string scenePath,
        bool ignoreEntities = false,
        Vector3 position = default,
        Vector3 rotation = default
    )
    {
        GD.Print("Trying to load scene", scenePath);
        var scene = GD.Load<PackedScene>(scenePath);
        var instance = scene.Instantiate();
        foreach (var child in instance.GetChildren())
        {
            GD.Print("Child from scene", child);
            if (child is INetEntity entity)
            {
                // Only spawn in entities if we set this flag.
                if (!ignoreEntities)
                {
                    GD.Print("Child is entity, spawning", child);
                    SpawnNewEntity(
                        position + entity.Position,
                        rotation + entity.Rotation,
                        entity.Data.CopyFromResource()
                    );
                }
            }
            else
            {
                GD.Print("Child is not entity, adding under root");
                instance.RemoveChild(child);
                child.Owner = null;
                SectorRoot.AddChild(child);
                child.Owner = SectorRoot;
            }
        }

        UpdateOwners.UpdateOwnerRecursive(SectorRoot, SectorRoot);

        instance.QueueFree();
    }

    public void SpawnNewEntity<T>(Vector3 position, Vector3 rotation, T data)
        where T : EntityData
    {
        data.Position = position;
        data.Rotation = rotation;
        data.CurrentSector = this;

        // Generate Entity ID
        data.EntityID = ServerData.EntityIDCounter;
        ServerData.EntityIDCounter++;

        ServerData.EntityDirectory[data.EntityID] = SectorID;
        EntitiesData[data.EntityID] = data;

        // When spawning a new entity, we can use GetSecrets to
        // get the secret data from the entity resource
        // var secrets = data.Secrets;

        // Usually there won't be any secret data, if there is, save it
        // if (secrets != null)
        // {
        //     EntitySecrets[data.EntityID] = secrets;
        // }

        InstanceEntity(data);

        EchoToSector(new SpawnEntity(data), DeliveryMethod.ReliableUnordered);
    }

    public EntityData? RemoveEntity(ulong entityID)
    {
        // Remove the entity (and its secrets data if it exists)
        _ = EntitiesData.Remove(entityID, out var data);
        // _ = EntitySecrets.Remove(entityID, out var secrets);

        // Remove entity if it's instanced
        if (Entities.Remove(entityID, out var entity))
        {
            EchoToSector(new RemoveEntity(entityID), DeliveryMethod.ReliableUnordered);
            var node = entity.GetNode();
            node.QueueFree();
        }

        return data;
    }

    /// <summary>
    /// Helper method to just instance an entity into the sector's scene
    /// </summary>
    void InstanceEntity(EntityData data)
    {
        data.InSaveState = false;

        var newInstance = data.SpawnInstance(true);
        SectorRoot.AddChild(newInstance.GetNode());

        Entities[data.EntityID] = newInstance;

        foreach (var playerPeer in Players.Values)
        {
            data.OnMeetPlayer(playerPeer);
        }
    }

    public void Unload()
    {
        foreach (var entity in Entities.Values)
        {
            entity.Data.InSaveState = true;
            entity.Data.Position = entity.Position;
            entity.Data.Rotation = entity.Rotation;
            entity.Data.CurrentSector = null!;
            entity.Data = null!;
            entity.GetNode().QueueFree();
        }
        SectorRoot.QueueFree();
        SectorRoot = null!;
        ServerData = null!;
    }

    public void PlayerConnect(NetPeer peer)
    {
        var playerData = peer.GetPlayerState().Data;

        peer.EncodeAndSend(
            new ClientInitializer(playerData.CurrentEntityID, EntitiesData, Parameters),
            DeliveryMethod.ReliableUnordered
        );
        foreach (var data in EntitiesData.Values)
        {
            data.OnMeetPlayer(peer);
        }
    }

    public void PlayerDisconnect(NetPeer peer)
    {
        // Ensure the player's transform gets updated in the server when
        // they disconnect
        var state = peer.GetPlayerState();
        var entity = Entities[state.Data.CurrentEntityID];
        entity.Data.Position = entity.Position;
        entity.Data.Rotation = entity.Rotation;
        Players.Remove(state.PlayerID);
    }
}
