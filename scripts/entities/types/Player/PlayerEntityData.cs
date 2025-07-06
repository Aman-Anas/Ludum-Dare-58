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
public partial class PlayerEntityData : EntityData, IHealth, IBasicAnim, IStorageContainer
{
    [Export]
    public int Health { get; set; }

    [Export]
    // Why save this? because it's funny
    public float HeadPivotAngle { get; set; }

    [Export]
    // For now, use a simple color to differentiate players
    public Color PlayerColor { get; set; }

    [MemoryPackIgnore]
    public Vector3 HeadRotation { get; set; } = Vector3.Zero;

    [MemoryPackIgnore]
    public byte CurrentAnim { get; set; }

    [MemoryPackIgnore]
    public Action HealthDepleted { get; set; } = null!;

    /////// Inventory ///////
    [MemoryPackIgnore]
    public Dictionary<short, InventoryItem> Inventory { get; set; } = [];

    [MemoryPackOnSerialized]
    static void Saving<TBufferWriter>(
        ref MemoryPackWriter<TBufferWriter> writer,
        ref PlayerEntityData value
    )
        where TBufferWriter : IBufferWriter<byte>
    {
        if (value.InSaveState)
        {
            writer.WriteUnmanaged(value.Health);
            writer.WriteValue(value.Inventory);
        }
    }

    [MemoryPackOnDeserialized]
    static void Loading(ref MemoryPackReader reader, ref PlayerEntityData value)
    {
        if (value.InSaveState)
        {
            value.Health = reader.ReadUnmanaged<int>();
            value.Inventory = reader.ReadValue<Dictionary<short, InventoryItem>>()!;
        }
    }

    public override void OnPlayerJoin(NetPeer peer)
    {
        if (peer.OwnsEntity(EntityID))
        {
            peer.EncodeAndSend(new HealthUpdate(EntityID, Health));

            var update = new StorageUpdate(EntityID, Inventory);
            peer.EncodeAndSend(update, DeliveryMethod.ReliableOrdered);
        }
    }

    public short MaxSlots { get; set; } = InventoryUI.NumSlots;
    public bool AutoPickup { get; set; } = true;

    [MemoryPackIgnore]
    public Action OnInventoryUpdate { get; set; } = null!;

    static readonly StringName IdleName = new("GAME_Breathe");
    static readonly StringName RunningName = new("GAME_Run");

    public StringName GetAnimation()
    {
        return (PlayerAnims)CurrentAnim switch
        {
            PlayerAnims.Idle => IdleName,
            PlayerAnims.Run => RunningName,
            _ => IdleName
        };
    }
}

public enum PlayerAnims : byte
{
    Idle,
    Run
}
