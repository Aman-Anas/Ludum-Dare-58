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

    public override void _Ready()
    {
        _data.HealthDepleted += _data.DestroyEntity;
    }

    public override void _PhysicsProcess(double delta)
    {
        // Tell the server our new transform at the fixed physics tick
        this.UpdateTransform();
    }
}
