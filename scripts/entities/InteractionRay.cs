using System;
using Game;
using Game.Terrain;
using Godot;

public partial class InteractionRay : RayCast3D
{
    const float MIN_DISTANCE_SQ = 2.1f * 2.1f;
    ulong lastTerraform = Time.GetTicksMsec();
    const ulong TERRAFORM_INTERVAL = 30; //ms

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every physics frame. 'delta' is the elapsed time since the previous frame.
    public override void _PhysicsProcess(double delta)
    {
        // return;
        // Should get the currently held item and call its use/run method. (interface)
        // just testing for now though, so let's assume terraforming

        if (!IsColliding())
        {
            return;
        }

        // GD.Print(collisionObj);
        // CSGBoxes are detected, but aren't actually CollisionObject3Ds
        if (GetCollider() is not CollisionObject3D)
        {
            return;
        }

        if ((Time.GetTicksMsec() - lastTerraform) >= TERRAFORM_INTERVAL
        //&& collisionObj.GetParent<Node3D>() is Chunk
        )
        {
            bool add;
            if (Input.IsActionPressed(GameActions.PLAYER_PRIMARY_USE))
            {
                if ((GetCollisionPoint() - GlobalPosition).LengthSquared() < MIN_DISTANCE_SQ)
                {
                    return;
                }
                add = true;
            }
            else if (Input.IsActionPressed(GameActions.PLAYER_SECONDARY_USE))
            {
                add = false;
            }
            else
            {
                return;
            }

            // var pos = (GlobalBasis.Z * 2) + GlobalPosition;
            // if (IsColliding()) // && (GetCollisionPoint() - GlobalPosition).Length() <= 2)
            // {
            var pos = GetCollisionPoint();
            // }
            // Manager.Instance.MainWorld.ChunkManager?.TerraformPoint(pos, 0.1f, add);
            // GD.Print("hi", GetCollisionPoint(), GetCollisionNormal());

            lastTerraform = Time.GetTicksMsec();
        }
    }
}
