using System;
using Game.Entities;
using Game.Networking;
using Godot;

public partial class DoorEntity : AnimatableBody3D, INetEntity<DestructibleDoorData>
{
    [Export]
    public DestructibleDoorData Data { get; set; } = null!;

    [Export]
    AnimationPlayer player = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (DestructibleDoorData)value;
    }

    ulong lastChanged = 0;
    bool state = false;

    // Called when the node enters the scene tree for the first time.

    public override void _Ready() { }

    static readonly StringName OpenAnim = new("OpenDoor");
    static readonly StringName CloseAnim = new("CloseDoor");

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if ((Time.GetTicksMsec() - lastChanged) > 2000)
        {
            if (state)
            {
                player.Play(CloseAnim);
            }
            else
            {
                player.Play(OpenAnim);
            }
            state = !state;
            lastChanged = Time.GetTicksMsec();
            // Data.DoorState.Toggle();
        }
    }
}
