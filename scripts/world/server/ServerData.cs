namespace Game.World.Data;

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Game.Entities;
using Game.Networking;
using Game.Setup;
using Game.Terrain;
using Godot;
using LiteNetLib;
using MemoryPack;
using Utilities.Collections;

[MemoryPackable]
public partial class ServerData
{
    public const string WORLD_DATA_FILE = "world.dat";
    public const string SECTOR_DIR_NAME = "sectors";

    // This is used to save/load data for sectors
    [MemoryPackIgnore]
    public string SaveDirectory { get; set; }

    // Store player login data (username -> pword)
    public Dictionary<string, string> LoginData { get; set; } = [];

    // Store general player data (username -> data)
    public Dictionary<string, PlayerData> PlayerData { get; set; } = [];

    // Dynamic store of active (logged in) players
    [MemoryPackIgnore]
    public Dictionary<string, NetPeer> ActivePlayers { get; set; } = [];

    public uint EntityIDCounter { get; set; } = 0;

    public uint SectorIDCounter { get; set; } = 0;

    // Store metadata about all sector names etc (key is sector ID)
    public Dictionary<uint, SectorMetadata> SectorMetadata { get; set; } = [];

    // Map entityID -> the sectorID it's located in
    public Dictionary<uint, uint> EntityDirectory { get; set; } = [];

    // Store objects, chunks, etc for each sector. This should be dynamically loaded from file.
    // Ignore serializing to ensure only loaded areas are in this dict
    [MemoryPackIgnore]
    public Dictionary<uint, Sector> LoadedSectors { get; set; } = [];

    // Check if a sector exists
    public bool SectorExists(uint sectorID) => SectorMetadata.ContainsKey(sectorID);

    // Add a new sector
    public Sector AddNewSector(string sectorName, SectorParameters sectorParams)
    {
        var newSectorId = SectorIDCounter;
        SectorIDCounter++;

        SectorMetadata[newSectorId] = new SectorMetadata
        {
            SectorID = newSectorId,
            SectorName = sectorName
        };

        var newSector = new Sector(
            newSectorId,
            sectorParams,
            Manager.Instance.GameServer.GetNewSectorViewport(),
            this
        );
        LoadedSectors[newSectorId] = newSector;

        return newSector;
    }

    public Sector LoadSector(uint sectorID)
    {
        var loadedSector = DataUtils.LoadData<Sector>(
            $"{SaveDirectory}/{SECTOR_DIR_NAME}/{sectorID}.dat"
        );
        loadedSector.WorldData = this;
        loadedSector.ReloadArea(Manager.Instance.GameServer.GetNewSectorViewport());
        LoadedSectors[sectorID] = loadedSector;
        return loadedSector;
    }

    public void SaveSector(Sector sector)
    {
        var sectorDir = $"{SaveDirectory}/{SECTOR_DIR_NAME}";

        if (!DirAccess.DirExistsAbsolute(sectorDir))
        {
            DirAccess.MakeDirRecursiveAbsolute(sectorDir);
        }

        DataUtils.SaveData($"{sectorDir}/{sector.SectorID}.dat", sector);
    }

    public void UnloadAllSectors()
    {
        foreach (var sectorID in LoadedSectors.Keys)
        {
            UnloadSector(sectorID);
        }
    }

    public void UnloadSector(uint sectorID)
    {
        if (LoadedSectors.TryGetValue(sectorID, out var sector))
        {
            LoadedSectors.Remove(sectorID);
            sector.Unload();
            SaveSector(sector);
        }
    }

    public void SaveServerData()
    {
        DataUtils.SaveData($"{SaveDirectory}/{WORLD_DATA_FILE}", this);

        // Save all sectors that are currently loaded
        foreach (var sector in LoadedSectors.Values)
        {
            SaveSector(sector);
        }
    }

    public static ServerData LoadServerData(string directory)
    {
        var loadedData = DataUtils.LoadData<ServerData>($"{directory}/{WORLD_DATA_FILE}");
        loadedData.SaveDirectory = directory;
        return loadedData;
    }

    public bool ValidatePlayer(LoginPacket loginData, PlayerEntityData playerTemplate)
    {
        if (ActivePlayers.ContainsKey(loginData.Username))
        {
            return false;
        }

        if (LoginData.TryGetValue(loginData.Username, out string actualPword))
        {
            return loginData.Password == actualPword;
        }
        else
        {
            GD.Print("he no exist");
            GD.Print(LoginData.Count);
            GD.Print(LoginData);
            // If the username is not in the registry, then let's add it
            LoginData[loginData.Username] = loginData.Password;
            var newPlayerData = playerTemplate.CopyFromResource();
            newPlayerData.Owners.Add(loginData.Username);

            LoadedSectors[0].SpawnNewEntity(Vector3.Zero, Vector3.Zero, newPlayerData);

            PlayerData[loginData.Username] = new(0, newPlayerData.EntityID);
            return true;
        }
    }

    public void PlayerDisconnect(NetPeer peer)
    {
        ActivePlayers.Remove(peer.GetPlayerState().Username);
        peer.GetPlayerState().CurrentSector.PlayerDisconnect(peer);
    }
}

[MemoryPackable]
public partial class SectorMetadata
{
    public uint SectorID { get; set; }

    public string SectorName { get; set; }
}
