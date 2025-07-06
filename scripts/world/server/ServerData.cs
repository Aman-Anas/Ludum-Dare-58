namespace Game.World.Data;

using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
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
    public const string WorldDataFile = "world.dat";
    public const string SectorDirName = "sectors";

    // This is used to save/load data for sectors
    [MemoryPackIgnore]
    public string SaveDirectory { get; set; } = null!;

    // ID 0 is kind of a wildcard, e.g. for entity ownership it means all players are owners
    public ulong PlayerIDCounter { get; set; } = 1;

    // Store username to player ID
    public Dictionary<string, ulong> PlayerIDs { get; set; } = [];

    // Store general player data (playerID -> data)
    public Dictionary<ulong, PlayerData> PlayerData { get; set; } = [];

    // Dynamic store of active (logged in) players
    [MemoryPackIgnore]
    public Dictionary<string, NetPeer> ActivePlayers { get; set; } = [];

    public ulong EntityIDCounter { get; set; }

    public uint SectorIDCounter { get; set; }

    // Store metadata about all sector names etc (key is sector ID)
    public Dictionary<uint, SectorMetadata> SectorMetadata { get; set; } = [];

    // Map entityID -> the sectorID it's located in
    public Dictionary<ulong, uint> EntityDirectory { get; set; } = [];

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
            $"{SaveDirectory}/{SectorDirName}/{sectorID}.dat"
        );
        loadedSector.ServerData = this;
        loadedSector.ReloadArea(Manager.Instance.GameServer.GetNewSectorViewport());
        LoadedSectors[sectorID] = loadedSector;
        return loadedSector;
    }

    public void SaveSector(Sector sector)
    {
        var sectorDir = $"{SaveDirectory}/{SectorDirName}";

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
        DataUtils.SaveData($"{SaveDirectory}/{WorldDataFile}", this);

        // Save all sectors that are currently loaded
        foreach (var sector in LoadedSectors.Values)
        {
            SaveSector(sector);
        }
    }

    public static ServerData LoadServerData(string directory)
    {
        var loadedData = DataUtils.LoadData<ServerData>($"{directory}/{WorldDataFile}");
        loadedData.SaveDirectory = directory;
        return loadedData;
    }

    public bool ValidatePlayer(
        LoginPacket loginData,
        PlayerEntityData playerTemplate,
        out ulong playerID
    )
    {
        if (ActivePlayers.ContainsKey(loginData.Username))
        {
            playerID = default;
            return false;
        }

        if (PlayerIDs.TryGetValue(loginData.Username, out var existingID))
        {
            playerID = existingID;
            return loginData.Password == PlayerData[existingID].Password;
        }
        else
        {
            GD.Print("he no exist");
            // GD.Print(LoginData.Count);
            // GD.Print(LoginData);
            // If the username is not in the registry, then let's add it
            // PlayerData[loginData.Username] = loginData.Password;
            var newEntityData = playerTemplate.CopyFromResource();

            LoadedSectors[0].SpawnNewEntity(Vector3.Zero, Vector3.Zero, newEntityData);

            var newPlayerData = new PlayerData(
                PlayerIDCounter,
                loginData.Username,
                0,
                newEntityData.EntityID,
                loginData.Password
            );

            newEntityData.Owners.Add(newPlayerData.PlayerID);

            PlayerData[newPlayerData.PlayerID] = newPlayerData;
            PlayerIDs[loginData.Username] = newPlayerData.PlayerID;
            playerID = newPlayerData.PlayerID;

            PlayerIDCounter++;

            return true;
        }
    }

    public void PlayerDisconnect(NetPeer peer)
    {
        ActivePlayers.Remove(peer.GetPlayerState().Data.Username);
        peer.GetPlayerState().CurrentSector.PlayerDisconnect(peer);
    }
}

[MemoryPackable]
public partial class SectorMetadata
{
    public uint SectorID { get; set; }

    public string SectorName { get; set; } = null!;
}
