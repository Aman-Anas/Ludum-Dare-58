using System;
using System.Collections.Generic;
using Game.World.Data;
using Godot;
using MemoryPack;
using Utilities;
using Utilities.Collections;

namespace Game.Setup;

public static class WorldSaves
{
    public const string WorldSavePath = "user://worlds";
    const string WorldMetaFile = "worldmeta.dat";

    public static List<(string, WorldMetadata)> GetWorldList()
    {
        // Make the worlds folder if it doesnt exist
        if (!DirAccess.DirExistsAbsolute(WorldSavePath))
        {
            DirAccess.MakeDirRecursiveAbsolute(WorldSavePath);
        }

        var newList = new List<(string, WorldMetadata)>();
        foreach (string worldName in DirAccess.GetDirectoriesAt(WorldSavePath))
        {
            var worldDir = DirAccess.Open(GetSaveDir(worldName));
            if (worldDir.FileExists(WorldMetaFile))
            {
                newList.Add(
                    (
                        worldName,
                        DataUtils.LoadData<WorldMetadata>(
                            $"{GetSaveDir(worldName)}/{WorldMetaFile}"
                        )
                    )
                );
            }
        }
        newList.Sort(new WorldDateSorter());
        return newList;
    }

    public static void MakeNewWorld(string name)
    {
        var dirPath = GetSaveDir(name);
        var metaPath = $"{dirPath}/{WorldMetaFile}";
        if (!FileAccess.FileExists(metaPath))
        {
            // If the metadata file doesn't exist, let's make some world metadata
            DirAccess.MakeDirRecursiveAbsolute(dirPath);
            DataUtils.SaveData<WorldMetadata>(metaPath, new(name, DateTime.Now));

            // And instantiate a new world ServerData.
            ServerData newData = new() { SaveDirectory = dirPath };

            // Save the server data to the new file
            newData.SaveServerData();
        }
        else
        {
            throw new ArgumentException("World already exists!");
        }
    }

    // TODO: Add a LastOpened prop to the WorldMetadata and a method to update that prop for a
    // world name.

    static string GetSaveDir(string saveName)
    {
        return $"{WorldSavePath}/{saveName}";
    }

    public static ServerData LoadWorld(string saveName)
    {
        return ServerData.LoadServerData(GetSaveDir(saveName));
    }

    public static void DeleteWorld(string saveName)
    {
        var dir = GetSaveDir(saveName);
        DirAccessExtensions.RemoveDir(dir);
    }
}

[MemoryPackable]
public partial record WorldMetadata(string Nickname, DateTime CreationTime);

public class WorldDateSorter : IComparer<(string, WorldMetadata)>
{
    public int Compare((string, WorldMetadata) x, (string, WorldMetadata) y)
    {
        return (int)(x.Item2.CreationTime - y.Item2.CreationTime).TotalSeconds;
    }
}
