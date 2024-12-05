namespace Game.Entities;

using Game.Networking;
using Godot;

public partial class PlayerClient : StaticBody3D, INetEntity<PlayerEntityData>
{
    [Export]
    AnimationPlayer player;

    [Export]
    Node3D HeadRef;

    public PlayerEntityData Data { get; set; }

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (PlayerEntityData)value;
    }

    public override void _Ready()
    {
        HeadRef.GlobalRotation = HeadRef.GetParent<Node3D>().GlobalRotation;
    }

    public override void _PhysicsProcess(double delta)
    {
        player.Play(Data.GetAnimation());

        if (Data.HeadRotation == Vector3.Zero)
            return;

        HeadRef.GlobalRotation = Data.HeadRotation;
    }
}
