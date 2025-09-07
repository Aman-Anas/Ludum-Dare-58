using System;
using System.Collections.Generic;
using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;
using Utilities.Data;

namespace Game.Entities;

[MemoryPackable]
[MemoryPackUnion(0, typeof(ToggleComponent))]
[MemoryPackUnion(1, typeof(HealthComponent))]
[MemoryPackUnion(2, typeof(StorageContainerComponent))]
[MemoryPackUnion(3, typeof(StorableComponent))]
public partial interface INetComponent
{
    public void Initialize(IEntityData data, uint componentIndex);

    [MemoryPackIgnore]
    public Action? JustUpdated { get; set; }
}

public abstract partial class NetComponent<T> : MemoryPackableResource, INetComponent
    where T : IEntityData
{
    /// <summary>
    /// This is how you would access the entity data inside of a component
    /// </summary>
    [MemoryPackIgnore]
    protected T data = default!;

    /// <summary>
    /// This is the index of the component in the parent entity's component registry.
    /// </summary>
    [MemoryPackIgnore]
    protected uint index;

    /// <summary>
    /// Emitted whenever this component gets overwritten by a ComponentOverwrite update.
    /// </summary>
    [MemoryPackIgnore]
    public Action? JustUpdated { get; set; }

    // Helpers for caching the update/overwrite message for this component
    private ComponentOverwriteUpdate cachedUpdate = null!;
    private readonly BufferedNetDataWriter cachedWriter = new();

    /// <summary>
    /// Initializes the component. This is NOT part of the constructor because we need to
    /// pass in the entity data at runtime. In the editor, the component is a regular resource.
    /// </summary>
    public void Initialize(IEntityData data, uint componentIndex)
    {
        this.data = (T)data;
        this.index = componentIndex;

        UpdateBufferWriter();
        this.cachedUpdate = new(data.EntityID, index)
        {
            ToUpdate = cachedWriter.Data.AsMemory(0, cachedWriter.Length)
        };
    }

    private void UpdateBufferWriter()
    {
        cachedWriter.Reset();
        MemoryPackSerializer.Serialize(cachedWriter, this);
    }

    /// <summary>
    /// Overwrites this component across the network.
    /// </summary>
    public void NetUpdate(
        bool ownersOnly = false,
        DeliveryMethod method = DeliveryMethod.Unreliable
    )
    {
        UpdateBufferWriter();
        cachedUpdate.ToUpdate = cachedWriter.Data.AsMemory(0, cachedWriter.Length);

        if (ownersOnly)
        {
            data.SendToOwners(cachedUpdate, method);
        }
        else
        {
            data.SendMessage(cachedUpdate, method);
        }
    }

    public void NetUpdatePeer(NetPeer peer, DeliveryMethod method = DeliveryMethod.Unreliable)
    {
        UpdateBufferWriter();
        cachedUpdate.ToUpdate = cachedWriter.Data.AsMemory(0, cachedWriter.Length);
        peer.EncodeAndSend(cachedUpdate, method);
    }
}

[MemoryPackable]
public partial record ComponentOverwriteUpdate(ulong EntityID, uint ComponentID)
    : IEntityUpdate<EntityData>
{
    [MemoryPoolFormatter<byte>]
    public required Memory<byte> ToUpdate { get; set; }

    public MessageType MessageType => MessageType.ComponentOverwriteUpdate;

    public void OnClient(ClientManager client) =>
        this.UpdateClientEntity<ComponentOverwriteUpdate, EntityData>(client);

    public void OnServer(NetPeer peer, ServerManager server) =>
        this.UpdateServerEntity<ComponentOverwriteUpdate, EntityData>(peer);

    public void UpdateEntity(INetEntity<EntityData> entity)
    {
        var data = entity.Data;
        if (data.ComponentRegistry == null)
            return;

        var components = data.ComponentRegistry.Components;

        if (ComponentID >= components.Length)
            return;

        var component = components[ComponentID];

        // Deserialize into an existing component
        MemoryPackSerializer.Deserialize(ToUpdate.Span, ref component);

        component?.JustUpdated?.Invoke();
    }
}
