using Godot;

[GlobalClass]
public partial class PhysicsSmoother : Node3D
{
    [Export]
    Node3D target;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready() { }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        var frac = Engine.GetPhysicsInterpolationFraction();

        Transform = Transform.InterpolateWith(target.GlobalTransform, (float)frac);
    }
}
