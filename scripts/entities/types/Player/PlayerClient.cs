namespace Game.Entities;

using Game.Networking;
using Godot;

public partial class PlayerClient : RigidBody3D, INetEntity
{
    public uint EntityID { get; set; }

    public EntityData Data
    {
        get => _data;
        set => _data = (PlayerEntityData)value;
    }

    PlayerEntityData _data;
}
