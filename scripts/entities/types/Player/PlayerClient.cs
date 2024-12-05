namespace Game.Entities;

using Game.Networking;
using Godot;

public partial class PlayerClient : StaticBody3D, INetEntity<PlayerEntityData>
{
    public uint EntityID { get; set; }

    public PlayerEntityData Data { get; set; }

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (PlayerEntityData)value;
    }
}
