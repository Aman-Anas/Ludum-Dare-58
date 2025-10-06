using System;
using Game;
using Godot;

public partial class Launcher : Node3D
{
    [Export]
    PackedScene projectile = null!;

    const ulong cooldownTimeMs = 120;

    ulong lastShot = 0;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (
            Manager.Instance.Data.CurrentSheepCount > 0
            && Input.IsMouseButtonPressed(MouseButton.Left)
        )
        {
            // Check the cooldown
            if ((Time.GetTicksMsec() - lastShot) >= cooldownTimeMs)
            {
                Manager.Instance.Data.CurrentSheepCount--;
                // spawn a thing
                var newProjectile = projectile.Instantiate<SheepProjectile>();
                GetTree().CurrentScene.AddChild(newProjectile);
                newProjectile.GlobalPosition = GlobalPosition;
                newProjectile.GlobalRotation = GlobalRotation;
                newProjectile.LinearVelocity = GlobalBasis * new Vector3(0, 0, -200);

                lastShot = Time.GetTicksMsec();
            }
        }
    }
}
