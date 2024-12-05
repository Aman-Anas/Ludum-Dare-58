namespace Game.Networking;

using Godot;
using MemoryPack;
using Utilities.Data;

/// <summary>
/// SecretData is stored separately from entity data when serialized
/// to keep "secret" entity server-side data from being sent to the client.
/// </summary>
// [MemoryPackUnion(0, typeof(DestructiblePropData))]
// [MemoryPackable]
public abstract partial class SecretData : MemoryPackableResource;
