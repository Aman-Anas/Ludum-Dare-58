using System;
using System.Collections.Generic;
using System.Linq;
using Game.World.Data;
using Godot;
using MessagePack;
using Utilities.Collections;

namespace Game.Setup;

public static class WorldSaves
{
    public const string WORLD_SAVE_PATH = "user://worlds/";
    const string WORLD_META_FILE = "worldmeta.dat";
    const string WORLD_DATA_FILE = "world.dat";

    public static List<(string, WorldMetadata)> GetWorldList()
    {
        var newList = new List<(string, WorldMetadata)>();
        foreach (string worldName in DirAccess.GetDirectoriesAt(WORLD_SAVE_PATH))
        {
            var worldDir = DirAccess.Open(GetSaveDir(worldName));
            if (worldDir.FileExists(WORLD_META_FILE))
            {
                newList.Add(
                    (
                        worldName,
                        DataUtils.LoadData<WorldMetadata>(
                            $"{GetSaveDir(worldName)}/{WORLD_META_FILE}"
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
        var metaPath = $"{dirPath}/{WORLD_META_FILE}";
        var worldPath = $"{dirPath}/{WORLD_DATA_FILE}";
        if (!FileAccess.FileExists(metaPath))
        {
            DirAccess.MakeDirRecursiveAbsolute(dirPath);
            DataUtils.SaveData<WorldMetadata>(metaPath, new(name, DateTime.Now));
            DataUtils.SaveData<ServerData>(worldPath, new());
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
        return $"{WORLD_SAVE_PATH}/{saveName}";
    }

    public static ServerData GetWorldData(string saveName)
    {
        return DataUtils.LoadData<ServerData>($"{GetSaveDir(saveName)}/{WORLD_DATA_FILE}");
    }
}

[MessagePackObject]
public record WorldMetadata(
    [property: Key(0)] string Nickname,
    [property: Key(1)] DateTime CreationTime
);

public class WorldDateSorter : IComparer<(string, WorldMetadata)>
{
    public int Compare((string, WorldMetadata) x, (string, WorldMetadata) y)
    {
        return (int)(x.Item2.CreationTime - y.Item2.CreationTime).TotalSeconds;
    }
}
