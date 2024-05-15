namespace Utilities.Collections;

using Game;
using Godot;
using MessagePack;
using MessagePackGodot;

public static class DataUtils
{
    /// <summary>
    /// Call at game manager initialization to ensure
    /// MessagePack works correctly with Godot objects
    /// </summary>
    public static void InitMessagePack()
    { // Initialize MessagePack resolvers
        var resolver = MessagePack.Resolvers.CompositeResolver.Create(
            // enable extension packages first (and put any other extensions you use in this section)
            GodotResolver.Instance,
            // finally use standard (default) resolver
            MessagePack.Resolvers.StandardResolver.Instance
        );

        // Get the serializer options
        var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);

        // pass options every time to set as default
        MessagePackSerializer.DefaultOptions = options;
    }

    public static void SaveData<T>(string filename, T data)
    {
        // save the file
        byte[] savedBytes = MessagePackSerializer.Serialize(data);
        using var saveFile = FileAccess.Open(filename, FileAccess.ModeFlags.Write);
        saveFile.StoreBuffer(savedBytes);
        saveFile.Close();
    }

    public static T LoadData<T>(string filename)
    {
        using var saveFile = FileAccess.Open(filename, FileAccess.ModeFlags.Read);
        var data = MessagePackSerializer.Deserialize<T>(
            saveFile.GetBuffer((long)saveFile.GetLength())
        );
        saveFile.Close();

        return data;
    }

    /// <summary>
    /// Load an object from a file, or return null if nonexistent
    /// </summary>
    public static T LoadFromFileOrNull<T>(string filename)
    {
        if (FileAccess.FileExists(filename))
        {
            return LoadData<T>(filename);
        }
        else
        {
            return default;
        }
    }
}
