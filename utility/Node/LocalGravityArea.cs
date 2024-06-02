using Godot;

[GlobalClass]
public partial class LocalGravityArea : Area3D
{
    [Export]
    Vector3 localGravityValue;

    void UpdateLocalGravity()
    {
        GravityDirection = GlobalBasis * localGravityValue;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        UpdateLocalGravity();
    }

    public override void _Notification(int what)
    {
        if (what == NotificationTransformChanged)
            UpdateLocalGravity();
    }
}
