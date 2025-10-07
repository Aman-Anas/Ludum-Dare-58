using System;
using Godot;

public partial class RollyTurret : RigidBody3D
{
    [Export]
    Area3D area = null!;

    [Export]
    PackedScene pew = null!;

    [Export]
    Node3D launchPoint = null!;

    [Export]
    Timer launchPewTime = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        launchPewTime.Timeout += () =>
        {
            if (locked)
            {
                var newPew = pew.Instantiate<Potato>();
                AddChild(newPew);
                newPew.GlobalTransform = launchPoint.GlobalTransform;
                newPew.LinearVelocity = newPew.GlobalBasis * new Vector3(0, 0, -50);
            }
        };

        BodyEntered += PotatHandler;
    }

    private void PotatHandler(Node body)
    {
        if (body is SheepProjectile && ((Time.GetTicksMsec() - lastHurt) >= 100))
        {
            health--;
            animPlayer.Play(hurt);
            lastHurt = Time.GetTicksMsec();
        }
        if (health <= 0)
        {
            animPlayer.Stop();
            QueueFree();
        }
    }

    bool locked = false;

    int health = 4;

    [Export]
    AnimationPlayer animPlayer = null!;
    StringName hurt = new("hurt");
    StringName roll = new("roll");
    ulong lastHurt = 0;

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        var found = false;
        foreach (var body in area.GetOverlappingBodies())
        {
            if (body is Farmer player)
            {
                found = true;
                // LookAt(player.TargetPosition.GlobalPosition);
                var dir = (player.TargetPosition.GlobalPosition - GlobalPosition).Normalized();
                AngularVelocity = new(0, (-GlobalBasis.X).Dot(dir) * 10, 0);
                ApplyCentralForce(GlobalBasis * new Vector3(0, 0, (float)(-500 * delta)));
            }
        }
        locked = found;

        animPlayer.Play(roll);
    }
}
