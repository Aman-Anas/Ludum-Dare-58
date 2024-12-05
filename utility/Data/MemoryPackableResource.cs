namespace Utilities.Data;

using Godot;
using MemoryPack;

[GlobalClass]
public abstract partial class MemoryPackableResource : Resource
{
    // These are to ensure MemoryPack doesn't serialize these unneeded fields
    [MemoryPackIgnore]
    public new nint NativeInstance
    {
        get => base.NativeInstance;
    }

    [MemoryPackIgnore]
    public new bool ResourceLocalToScene
    {
        get => base.ResourceLocalToScene;
        set => base.ResourceLocalToScene = value;
    }

    [MemoryPackIgnore]
    public new string ResourcePath
    {
        get => base.ResourcePath;
        set => base.ResourcePath = value;
    }

    [MemoryPackIgnore]
    public new string ResourceSceneUniqueId
    {
        get => base.ResourceSceneUniqueId;
        set => base.ResourceSceneUniqueId = value;
    }

    [MemoryPackIgnore]
    public new string ResourceName
    {
        get => base.ResourceName;
        set => base.ResourceName = value;
    }
}
