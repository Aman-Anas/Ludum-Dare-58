using System;
using Game;
using Godot;

public partial class Farmer : RigidBody3D
{
    [Export]
    RayCast3D floorSensor = null!;

    [Export]
    AnimationPlayer player = null!;

    const float MOVEMENT_FORCE = 40;
    const float MAX_MOVEMENT_SPEED = 12;
    const float GRAPPLE_FORCE = 60;

    Vector3 movementVec = new(0, 0, 0);

    // Jumping
    readonly Vector3 JUMP_IMPULSE = new(0, 5.5f, 0);
    readonly ulong MIN_JUMP_RESET_TIME = 1000; // ms

    bool justJumped;
    ulong timeJumped;

    // Mouselook
    [Export]
    Node3D yawTarget = null!;

    [Export]
    Node3D pitchTarget = null!;

    [Export]
    Node3D mouseLookRotationTarget = null!;

    readonly float MIN_PITCH = Mathf.DegToRad(-90.0f);
    readonly float MAX_PITCH = Mathf.DegToRad(80.0f);

    const float GRAVITY_CORRECTION_SPEED = 4.0f;
    const float ROTATION_SPEED = 7f;

    [Export]
    RayCast3D grappleCast = null!;
    Vector3? currentGrapplePos = null;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Need this to capture the mouse of course
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    void UpdateHeadOrientation()
    {
        yawTarget.Orthonormalize();
        var yawUpDiff = new Quaternion(yawTarget.GlobalBasis.Y, GlobalBasis.Y).Normalized();
        var axis = yawUpDiff.GetAxis();

        // Check to ensure the quaternion is sane and not all zeros
        if (yawUpDiff.LengthSquared() == 0 || axis.LengthSquared() == 0)
            return;

        yawTarget.Rotate(axis.Normalized(), yawUpDiff.GetAngle());

        mouseLookRotationTarget.GlobalRotation = yawTarget.GlobalRotation;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        // Direct mouselook for head itself
        if (@event is InputEventMouseMotion motion)
        {
            var sensitivity = Manager.Instance.Config.MouseSensitivity;

            yawTarget.RotateObjectLocal(Vector3.Up, -motion.Relative.X * sensitivity);

            var pitchRot = pitchTarget.Rotation;
            pitchRot.X = Mathf.Clamp(
                pitchRot.X + (-motion.Relative.Y * sensitivity),
                MIN_PITCH,
                MAX_PITCH
            );
            pitchTarget.Rotation = pitchRot;
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        Orthonormalize();

        if (!Input.IsMouseButtonPressed(MouseButton.Right))
        {
            currentGrapplePos = null;
        }
        if (Input.IsMouseButtonPressed(MouseButton.Right) && currentGrapplePos == null)
        {
            if (grappleCast.IsColliding())
            {
                currentGrapplePos = grappleCast.GetCollisionPoint();
            }
        }

        var inputVec = Input.GetVector(
            GameActions.PlayerStrafeLeft,
            GameActions.PlayerStrafeRight,
            GameActions.PlayerForward,
            GameActions.PlayerBackward
        );

        var touchingFloor = false;

        // Detect whether we're touching the floor (with feet)
        if (floorSensor.IsColliding())
        {
            touchingFloor = true;
        }

        // Movement
        movementVec.X = inputVec.X;
        movementVec.Y = 0;
        movementVec.Z = inputVec.Y;

        // Convert our global linear velocity to local and remove Y
        var actualLocalVelocity = GlobalBasis.Inverse() * state.LinearVelocity;
        actualLocalVelocity.Y = 0;

        // Calculate our intended local velocity
        var intendedLocalVelocity = MAX_MOVEMENT_SPEED * movementVec;

        // Find the difference between them and use it to apply a force
        var diffVelo = intendedLocalVelocity - actualLocalVelocity;

        state.ApplyCentralForce(GlobalBasis * (diffVelo.LimitLength(1) * MOVEMENT_FORCE));

        // Jumping

        // Reset the jump flag if we're in the air or a min time elapsed
        if ((!touchingFloor) || ((Time.GetTicksMsec() - timeJumped) > MIN_JUMP_RESET_TIME))
        {
            justJumped = false;
        }

        // Dev mode jetpack
        if (false && Input.IsActionPressed(GameActions.PlayerJump))
        {
            state.ApplyCentralImpulse(GlobalBasis * Vector3.Up * 0.3f);
        }

        if (Input.IsActionPressed(GameActions.PlayerJump) && touchingFloor && !justJumped)
        {
            state.ApplyCentralImpulse(GlobalBasis * JUMP_IMPULSE);
            justJumped = true;
            timeJumped = Time.GetTicksMsec();
        }

        // Get the current gravity direction and our down direction (both global)
        var currentGravityDir = state.TotalGravity.Normalized();

        var currentDownDir = -GlobalBasis.Y;

        // Find the rotation difference between these two
        var rotationDifference = new Quaternion(currentDownDir, currentGravityDir);

        // Turn it into an euler and multiply by our gravity correction speed
        var gravityCorrectionVelo = rotationDifference.Normalized();

        // Before assigning gravity correction, add mouselook
        var newLocalAngVelo = gravityCorrectionVelo.GetEuler() * GRAVITY_CORRECTION_SPEED;

        // Get the rotation difference for our head
        var mouseLookDiff = new Quaternion(GlobalBasis.Z, mouseLookRotationTarget.GlobalBasis.Z)
            .Normalized()
            .GetEuler();

        // Put into local coordinates
        mouseLookDiff = GlobalBasis.Inverse() * mouseLookDiff;

        // Remove extraneous rotation (only want mouselook to affect Y)
        mouseLookDiff.X = 0;
        mouseLookDiff.Z = 0;

        // Add it to our new velocity (after making it global)
        newLocalAngVelo += GlobalBasis * (mouseLookDiff * ROTATION_SPEED);

        /**
        Get our final angular velocity. It would be more realistic to use torque,
        but velocity is a bit easier to work with. If needed, torque can be used though.
        */
        state.AngularVelocity = newLocalAngVelo;

        if (currentGrapplePos != null)
        {
            var forceDir = (((Vector3)currentGrapplePos) - GlobalPosition).Normalized();
            state.ApplyCentralForce(GRAPPLE_FORCE * forceDir);
        }
    }

    [Export]
    BoneAttachment3D glowyEndPos = null!;

    [Export]
    Node3D glowyThing = null!;

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        UpdateHeadOrientation();

        if (movementVec.LengthSquared() > 0)
        {
            player.Play(idleAnimName);
        }
        else
        {
            player.Play(runAnimName);
        }

        if (currentGrapplePos != null)
        {
            glowyEndPos.GlobalPosition = (Vector3)currentGrapplePos;
            glowyThing.Visible = true;
        }
        else
        {
            glowyThing.Visible = false;
        }
    }

    [Export]
    StringName runAnimName = null!;

    [Export]
    StringName idleAnimName = null!;
}
