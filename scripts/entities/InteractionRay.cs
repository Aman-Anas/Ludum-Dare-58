using System;
using Game;
using Game.Terrain;
using Godot;

public partial class InteractionRay : RayCast3D
{
    ulong lastTerraform = Time.GetTicksMsec();
    const ulong TERRAFORM_INTERVAL = 100; //ms

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
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
            Input.IsActionPressed(GameActions.PLAYER_PRIMARY_USE)
            && ((Time.GetTicksMsec() - lastTerraform) >= TERRAFORM_INTERVAL)
            && collisionObj.GetParent<Node3D>() is Chunk
        )
        {
            Manager.Instance.MainWorld.chunkManager?.TerraformPoint(GetCollisionPoint(), 0.1f);
            // GD.Print("hi", GetCollisionPoint(), GetCollisionNormal());

            lastTerraform = Time.GetTicksMsec();
        }
    }
}
