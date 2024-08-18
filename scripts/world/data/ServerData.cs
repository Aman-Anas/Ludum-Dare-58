namespace Game.World.Data;

using System.Collections.Generic;
using LiteNetLib;
using MessagePack;
using static Game.Terrain.ChunkData;

[MessagePackObject(keyAsPropertyName: true)]
public class ServerData
{
    // Store player login data (username -> pword)
    public Dictionary<string, string> LoginData { get; set; } = [];

    // Store general player data (username -> data)
    public Dictionary<string, PlayerData> PlayerData { get; set; } = [];

    // Dynamic store of active (logged in) players
    [IgnoreMember]
    public Dictionary<string, NetPeer> ActivePlayers { get; set; } = [];

    // Store metadata about all sector names etc
    public Dictionary<int, SectorMetadata> SectorMetadata { get; set; } = [];

    // Store objects, chunks, etc for each sector. This should be dynamically loaded from file.
    // Ignore serializing to ensure only loaded areas are in this dict
    [IgnoreMember]
    public Dictionary<int, SectorWorldData> SectorWorldData { get; set; } = [];
}

[MessagePackObject]
public class SectorMetadata
{
    [Key(0)]
    public int SectorID { get; set; }

    [Key(1)]
    public string SectorName { get; set; }
}

[MessagePackObject]
public class SectorWorldData
{
    [Key(0)]
    public bool ContainsTerrain { get; set; }

    [Key(1)]
    public Dictionary<ChunkID, byte[]> ChunkData { get; set; }

    [Key(2)]
    public TerrainParameters Parameters { get; set; }
}

[MessagePackObject]
public class PlayerData
{
    [Key(0)]
    public int CurrentSectorID { get; set; }
}
