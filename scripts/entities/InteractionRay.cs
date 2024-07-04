using System;
using Game;
using Game.Terrain;
using Godot;

public partial class InteractionRay : RayCast3D
{
    const float MIN_DISTANCE_SQ = 2.1f;
    ulong lastTerraform = Time.GetTicksMsec();
    const ulong TERRAFORM_INTERVAL = 10; //ms

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every physics frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {
        // Should get the currently held item and call its use/run method. (interface)
        // just testing for now though, so let's assume terraforming

        if (!IsColliding())
        {
            return;
        }

        // GD.Print(collisionObj);
        // CSGBoxes are detected, but aren't actually CollisionObject3Ds
        if (GetCollider() is not CollisionObject3D collisionObj)
        {
            return;
        }

        if (
            ((Time.GetTicksMsec() - lastTerraform) >= TERRAFORM_INTERVAL)
            && collisionObj.GetParent<Node3D>() is Chunk
        )
        {
            float strength;
            if (Input.IsActionPressed(GameActions.PLAYER_PRIMARY_USE))
            {
                if ((GetCollisionPoint() - GlobalPosition).LengthSquared() < MIN_DISTANCE_SQ)
                {
                    return;
                }
                strength = 0.1f;
            }
            else if (Input.IsActionPressed(GameActions.PLAYER_SECONDARY_USE))
            {
                strength = -0.1f;
            }
            else
            {
                return;
            }
            Manager.Instance.MainWorld.chunkManager?.TerraformPoint(GetCollisionPoint(), strength);
            // GD.Print("hi", GetCollisionPoint(), GetCollisionNormal());

            lastTerraform = Time.GetTicksMsec();
        }
    }
}
