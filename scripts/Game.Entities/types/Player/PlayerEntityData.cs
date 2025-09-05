namespace Game.Entities;

using System;
using System.Buffers;
using System.Collections.Generic;
using Game.Networking;
using Godot;
using LiteNetLib;
using MemoryPack;

[GlobalClass]
[MemoryPackable]
public partial class PlayerEntityData : EntityData, IAutoCollector, IHealth
{
    /// <summary>
    /// Health status of this player. Hidden
    /// </summary>
    [Export]
    [MemoryPackIgnore]
    public HealthComponent HealthState { get; set; } = new();

    /// <summary>
    /// Property to test differentiation between players
    /// </summary>
    [Export]
    public Color PlayerColor { get; set; } = Color.FromHsv(Random.Shared.NextSingle(), 1, 1);

    /// <summary>
    /// Current rotation state of the player's head
    /// </summary>
    public Vector3 HeadRotation { get; set; } = Vector3.Zero;

    /// <summary>
    /// Main player inventory component. Hidden
    /// </summary>
    [MemoryPackIgnore]
    public StorageContainerComponent MainInventory { get; set; } =
        new() { MaxSlots = InventoryUI.NumSlots };

    // Save and load hidden components
    [MemoryPackOnSerialized]
    static void Saving<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        ref PlayerEntityData value
    )
        where TBufferWriter : IBufferWriter<byte>
    {
        if (value.InSaveState)
        {
            writer.WriteValue(value.HealthState);
            writer.WriteValue(value.MainInventory);
        }
    }

    [MemoryPackOnDeserialized]
    static void Loading(ref MemoryPackReader reader, ref PlayerEntityData value)
    {
        if (value.InSaveState)
        {
            value.HealthState = reader.ReadValue<HealthComponent>()!;
            value.MainInventory = reader.ReadValue<StorageContainerComponent>()!;
        }
    }

    public override void OnMeetPlayer(NetPeer peer)
    {
        if (peer.OwnsEntity(EntityID))
        {
            HealthState.NetUpdatePeer(peer);
            MainInventory.NetUpdatePeer(peer, DeliveryMethod.ReliableOrdered);
        }
    }

    static readonly StringName IdleName = new("GAME_Breathe");
    static readonly StringName RunningName = new("GAME_Run");

    public StringName GetAnimation()
    {
        return (PlayerAnims)AnimHelper.CurrentAnimation switch
        {
            PlayerAnims.Idle => IdleName,
            PlayerAnims.Run => RunningName,
            _ => IdleName
        };
    }

    /// <summary>
    /// Animation component.
    /// This is not saved because animation state depends on player input anyways
    /// </summary>
    [MemoryPackIgnore]
    public BasicAnimComponent AnimHelper { get; } = new();

    public bool AutoPickup { get; } = true;

    public PlayerEntityData()
    {
        ComponentRegistry = new(this, [HealthState, MainInventory, AnimHelper]);
    }
}

public enum PlayerAnims : byte
{
    Idle,
    Run
}
