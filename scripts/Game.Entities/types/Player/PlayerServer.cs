namespace Game.Entities;

using Game.Networking;
using Godot;

public partial class PlayerServer : Area3D, INetEntity<PlayerEntityData>
{
    public PlayerEntityData Data { get; set; } = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (PlayerEntityData)value;
    }

    // Player Animation
    [Export]
    AnimationPlayer animPlayer = null!;

    public override void _Ready()
    {
        Data.AnimHelper.JustUpdated += () => animPlayer.Play(Data.GetAnimation());

        // We shouldn't do this and instead move the player entity somewhere else
        // when they die (so they respawn)
        // _data.HealthDepleted += _data.DestroyEntity;
    }
}
