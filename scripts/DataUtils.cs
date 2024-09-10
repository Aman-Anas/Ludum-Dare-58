namespace Utilities.Collections;

using System.Diagnostics.CodeAnalysis;
using Game;
using Godot;
using MemoryPack;

public static class DataUtils
{
    /// <summary>
    /// Call at game manager initialization to ensure
    /// MessagePack works correctly with Godot objects
    /// </summary>
    public static void InitMessagePack()
    { // Initialize MessagePack resolvers
        // var resolver = MessagePack.Resolvers.CompositeResolver.Create(
        //     // enable extension packages first (and put any other extensions you use in this section)

        //     // Auto-generated static resolver for AOT compilation (so no JIT needed)
        //     // MessagePack.Resolvers.GeneratedResolver.Instance,
        //     // Godot-specific structs etc resolver
        //     GodotResolver.Instance,
        //     // finally use standard (default) resolver
        //     MessagePack.Resolvers.StandardResolver.Instance
        // );

        // // Get the serializer options
        // var options = MessagePackSerializerOptions.Standard.WithResolver(resolver);

        // // pass options every time to set as default
        // MessagePackSerializer.DefaultOptions = options;
    }

    public static void SaveData<T>(string filename, T data)
    {
        // save the file
        byte[] savedBytes = MemoryPackSerializer.Serialize(data);
        using var saveFile = FileAccess.Open(filename, FileAccess.ModeFlags.Write);
        saveFile.StoreBuffer(savedBytes);
        saveFile.Close();
    }

    public static T LoadData<[DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T>(
        string filename
    )
    {
        using var saveFile = FileAccess.Open(filename, FileAccess.ModeFlags.Read);
        var data = MemoryPackSerializer.Deserialize<T>(
            saveFile.GetBuffer((long)saveFile.GetLength())
        );
        saveFile.Close();

        return data;
    }

    /// <summary>
    /// Load an object from a file, or return null if nonexistent
    /// </summary>
    public static T LoadFromFileOrNull<
        [DynamicallyAccessedMembers(DynamicallyAccessedMemberTypes.All)] T
    >(string filename)
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
