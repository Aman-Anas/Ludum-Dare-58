namespace Utilities.Node;

using Godot;

[GlobalClass]
// Simple helper node to do physics interpolation
public partial class PhysicsSmoother : Node3D
{
    [Export]
    Node3D target;

    Transform3D lastFrameTransform;
    Transform3D targetTransform;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        TopLevel = true;
        lastFrameTransform = targetTransform = target.GlobalTransform;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        var frac = (float)Engine.GetPhysicsInterpolationFraction();

        // Currently doing simple linear interpolation
        Transform = new Transform3D
        {
            // Update our position to be the last one plus the fraction * difference between frames
            Origin =
                lastFrameTransform.Origin
                + ((targetTransform.Origin - lastFrameTransform.Origin) * frac),

            // Set the rotation and scale to be a slerp between the frames
            Basis = lastFrameTransform.Basis.Slerp(targetTransform.Basis, frac)
        };
    }

    public override void _PhysicsProcess(double delta)
    {
        lastFrameTransform = targetTransform;
        targetTransform = target.GlobalTransform;
    }

    public override void _Notification(int what)
    {
        // Only do interpolation if this node is actually visible
        if (what == NotificationVisibilityChanged)
        {
            SetProcess(IsVisibleInTree());
            SetPhysicsProcess(IsVisibleInTree());
        }
    }
}
