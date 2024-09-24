namespace Game.Entities;

using Game.Networking;
using Godot;

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
        // We shouldn't do this and instead move the player entity somewhere else
        // when they die (so they respawn)
        // _data.HealthDepleted += _data.DestroyEntity;
    }
}
