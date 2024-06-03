using System.Linq;
using Game;
using Godot;

public partial class SimpleRigidPlayer : RigidBody3D
{
    public bool MovementEnabled { get; set; } = true;

    [Export]
    Node3D floorSensors; // This should have a bunch of (or one) RayCast3D children

    // Movement
    const float MOVEMENT_FORCE = 30;
    const float MAX_MOVEMENT_SPEED = 5;
    Vector3 movementVec = new(0, 0, 0);
    const float AIR_MOVEMENT_MULTIPLIER = 0.1f;
    const float GRAVITY_CORRECTION_SPEED = 4.0f;

    // Jumping
    readonly Vector3 JUMP_IMPULSE = new(0, 4.5f, 0);
    bool justJumped;
    ulong timeJumped;
    readonly ulong MIN_JUMP_RESET_TIME = 1000; // ms

    // Mouselook
    [Export]
    Node3D yawTarget;

    [Export]
    Node3D pitchTarget;

    [Export]
    Node3D headMesh;

    [Export]
    Node3D mouseLookRotationTarget;

    readonly float MIN_PITCH = Mathf.DegToRad(-90.0f);
    readonly float MAX_PITCH = Mathf.DegToRad(80.0f);

    const float ROTATION_SPEED = 5f;

    // Player Animation
    [Export]
    AnimationPlayer animPlayer;

    readonly StringName DEFAULT_ANIM = new("Idle"); // This should be an idle
    readonly StringName RUNNING_ANIM = new("Run");

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Need this to capture the mouse of course
        Input.MouseMode = Input.MouseModeEnum.Captured;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        RunAnimations();
    }

    void UpdateHeadOrientation()
    {
        yawTarget.Orthonormalize();
        var yawUpDiff = new Quaternion(yawTarget.GlobalBasis.Y, GlobalBasis.Y).Normalized();
        yawTarget.Rotate(yawUpDiff.GetAxis().Normalized(), yawUpDiff.GetAngle());

        mouseLookRotationTarget.GlobalRotation = yawTarget.GlobalRotation;
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (!MovementEnabled)
            return;

        // Direct mouselook for head itself
        if (@event is InputEventMouseMotion motion)
        {
            var sensitivity = Manager.Instance.Config.MouseSensitivity;

            yawTarget.RotateObjectLocal(Vector3.Up, -motion.Relative.X * sensitivity);

            var pitchRot = pitchTarget.Rotation;
            pitchRot.X = Mathf.Clamp(
                pitchRot.X + (motion.Relative.Y * sensitivity),
                MIN_PITCH,
                MAX_PITCH
            );
            pitchTarget.Rotation = pitchRot;
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (!MovementEnabled)
            return;

        Orthonormalize();

        var inputVec = Input.GetVector(
            GameActions.PLAYER_STRAFE_RIGHT,
            GameActions.PLAYER_STRAFE_LEFT,
            GameActions.PLAYER_BACKWARD,
            GameActions.PLAYER_FORWARD
        );

        var touchingFloor = false;

        // Detect whether we're touching the floor (with feet)
        foreach (RayCast3D sensor in floorSensors.GetChildren().Cast<RayCast3D>())
        {
            if (sensor.IsColliding())
            {
                touchingFloor = true;
                break;
            }
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

        if (!touchingFloor)
        {
            diffVelo *= AIR_MOVEMENT_MULTIPLIER;
        }

        state.ApplyCentralForce(GlobalBasis * (diffVelo.LimitLength(1) * MOVEMENT_FORCE));

        // Jumping

        // Reset the jump flag if we're in the air or a min time elapsed
        if ((!touchingFloor) || ((Time.GetTicksMsec() - timeJumped) > MIN_JUMP_RESET_TIME))
        {
            justJumped = false;
        }

        if (Input.IsActionPressed(GameActions.PLAYER_JUMP) && touchingFloor && !justJumped)
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

        // Before assigning gravity correction, make it a local vector and add mouselook
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

        UpdateHeadOrientation();
    }

    void RunAnimations()
    {
        var horizontalSpeed = GlobalBasis.Inverse() * LinearVelocity;
        horizontalSpeed.Y = 0;
        if (movementVec.Length() > 0.1 && (horizontalSpeed.Length() > 0.2))
        {
            animPlayer.Play(RUNNING_ANIM);
        }
        else
        {
            animPlayer.Play(DEFAULT_ANIM);
        }
    }
}
