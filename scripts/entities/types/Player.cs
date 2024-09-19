// namespace Game.Entities;

// using System;
// using Game.Networking;
// using Godot;
// using MemoryPack;
// ;

// [MessagePackObject]
// public class PlayerEntityData : EntityData, IHealth
// {
//     [Key(5)]
//     [Export]
//     public uint Health { get; set; }

//     // Why save this? because it's funny
//     [Key(6)]
//     public float TorsoPivotAngle { get; set; }

//     [Key(7)]
//     public Color PlayerColor { get; set; }

//     public override void SpawnClientEntity(
//         Vector3 position,
//         Vector3 orientation,
//         ClientManager client
//     ) => client.SpawnEntity<NetPlayer>(this);

//     public override void SpawnServerEntity(
//         Vector3 position,
//         Vector3 orientation,
//         ServerManager server
//     ) => server.RespawnEntity<ServerPlayer>(this);
// }

// public partial class ServerPlayer : StaticBody3D, INetEntity
// {
//     public EntityData Data { get; set; }
// }

// // [MemoryPackable]
// public partial class NetPlayer : StaticBody3D, INetEntity // : StaticBody3D, INetEntity, IHealthComponent
// {
//     [Export]
//     Node3D playerTorsoPivot;

//     PlayerEntityData _data;
//     public EntityData Data
//     {
//         get => _data;
//         set => _data = (PlayerEntityData)value;
//     }

//     public override void _Ready()
//     {
//         var rot = playerTorsoPivot.Rotation;
//         rot.X = _data.TorsoPivotAngle;
//         playerTorsoPivot.Rotation = rot;
//     }
// }
namespace Game.Entities;

using System;
using Game.Networking;
using Godot;
using MemoryPack;

[MemoryPackable]
public partial class PlayerEntityData : EntityData, IHealth
{
    [Export]
    public int Health { get; set; }

    [MemoryPackIgnore]
    public Action HealthDepleted { get; set; }
}

public partial class PlayerServer : StaticBody3D, INetEntity
{
    public uint EntityID { get; set; }

    public EntityData Data
    {
        get => _data;
        set => _data = (PlayerEntityData)value;
    }

    PlayerEntityData _data;

    public override void _Ready()
    {
        _data.HealthDepleted += _data.DestroyEntity;
    }
}

public partial class PlayerClient : RigidBody3D, INetEntity
{
    public uint EntityID { get; set; }

    public EntityData Data
    {
        get => _data;
        set => _data = (PlayerEntityData)value;
    }

    PlayerEntityData _data;

    public override void _Ready()
    {
        _data.HealthDepleted += _data.DestroyEntity;
    }
}
