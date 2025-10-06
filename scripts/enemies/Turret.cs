using System;
using Godot;

public partial class Turret : Node3D
{
    [Export]
    Node3D turretLauncher = null!;

    [Export]
    Area3D area = null!;

    [Export]
    PackedScene pew = null!;

    [Export]
    Node3D launchPoint = null!;

    [Export]
    Timer launchPewTime = null!;

    [Export]
    Area3D hitArea = null!;

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
    }

    bool locked = false;

    int health = 2;

    [Export]
    AnimationPlayer animPlayer = null!;
    StringName hurt = new("hurt");
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
                turretLauncher.LookAt(player.TargetPosition.GlobalPosition);
            }
        }
        locked = found;

        foreach (var body in hitArea.GetOverlappingBodies())
        {
            if (body is SheepProjectile && ((Time.GetTicksMsec() - lastHurt) >= 100))
            {
                health--;
                animPlayer.Play(hurt);
                lastHurt = Time.GetTicksMsec();
            }
        }

        if (health <= 0)
        {
            animPlayer.Stop();
            QueueFree();
        }
    }
}
