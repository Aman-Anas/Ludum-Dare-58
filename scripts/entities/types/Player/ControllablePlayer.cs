namespace Game.Entities;

using System;
using System.Linq;
using System.Text;
using Game;
using Game.Networking;
using Godot;

[GlobalClass]
public partial class ControllablePlayer : RigidBody3D, INetEntity<PlayerEntityData>
{
    // Must get set externally
    public PlayerEntityData Data { get; set; } = null!;

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (PlayerEntityData)value;
    }

    public bool MovementEnabled { get; set; } = true;

    [Export]
    Node3D floorSensors = null!; // This should have a bunch of (or one) RayCast3D children

    [Export]
    Node3D dropLocation = null!;

    // Movement
    const float MOVEMENT_FORCE = 30;
    const float MAX_MOVEMENT_SPEED = 5;
    const float AIR_MOVEMENT_MULTIPLIER = 0.1f;
    const float GRAVITY_CORRECTION_SPEED = 4.0f;
    const bool DEV_MODE = true;

    Vector3 movementVec = new(0, 0, 0);

    // Jumping
    readonly Vector3 JUMP_IMPULSE = new(0, 4.5f, 0);
    readonly ulong MIN_JUMP_RESET_TIME = 1000; // ms

    bool justJumped;
    ulong timeJumped;

    // Mouselook
    [Export]
    Node3D yawTarget = null!;

    [Export]
    Node3D pitchTarget = null!;

    [Export]
    Node3D headMesh = null!;

    [Export]
    Node3D mouseLookRotationTarget = null!;

    readonly float MIN_PITCH = Mathf.DegToRad(-90.0f);
    readonly float MAX_PITCH = Mathf.DegToRad(80.0f);

    const float ROTATION_SPEED = 5f;

    // Player Animation
    [Export]
    AnimationPlayer animPlayer = null!;

    [Export]
    InventoryUI inventoryUI = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Need this to capture the mouse of course
        Input.MouseMode = Input.MouseModeEnum.Captured;

        inventoryUI.Hide();
        inventoryUI.UpdateInventorySlots(Data.Inventory);

        inventoryUI.TryInventoryMove += ((short src, short dest, uint count) moveCmd) =>
        {
            Data.ExecuteStorageAction(Data, moveCmd.src, moveCmd.dest, moveCmd.count);
        };

        Data.OnInventoryUpdate += () => inventoryUI.UpdateInventorySlots(Data.Inventory);

        animPlayer.CurrentAnimationChanged += (_) => Data.UpdateAnim();
    }

    public override void _PhysicsProcess(double delta)
    {
        // Update our transform to the server
        // this.UpdateTransform();

        // Use our custom player transform message
        Data.SendMessage(
            new PlayerTransform(Data.EntityID, Position, Rotation, headMesh.GlobalRotation)
        );
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        RunAnimations();
        UpdateHeadOrientation();
    }

    Vector2 mouseMovement; // Mouse movement since last frame

    void UpdateHeadOrientation()
    {
        // Apply our mouse look using the accumulated mouse movement
        var sensitivity = Manager.Instance.Config.MouseSensitivity;

        yawTarget.RotateObjectLocal(Vector3.Up, -mouseMovement.X * sensitivity);

        var pitchRot = pitchTarget.Rotation;
        pitchRot.X = Mathf.Clamp(
            pitchRot.X + (mouseMovement.Y * sensitivity),
            MIN_PITCH,
            MAX_PITCH
        );
        pitchTarget.Rotation = pitchRot;

        // Reset the mouse movement accumulator
        mouseMovement = Vector2.Zero;

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
        if (!MovementEnabled)
            return;

        // Direct mouselook for head itself
        if (@event is InputEventMouseMotion motion)
        {
            // Accumulate mouse movement
            // https://yosoyfreeman.github.io/article/godot/tutorial/achieving-better-mouse-input-in-godot-4-the-perfect-camera-controller/
            // Idk why it has to be this complicated
            // update: this actually isn't necessary as long as accumulated
            // inputs is enabled (which it is at time of writing)
            var viewportTransform = GetTree().Root.GetFinalTransform();
            mouseMovement += ((InputEventMouseMotion)motion.XformedBy(viewportTransform)).Relative;
        }

        // If we are carrying something in the first slot
        if (Data.Inventory.TryGetValue(0, out var firstSlotItem))
        {
            firstSlotItem.Storable.Position = dropLocation.GlobalPosition;
            Data.Client.SpawnEntity(firstSlotItem.Storable);
        }
    }

    public override void _IntegrateForces(PhysicsDirectBodyState3D state)
    {
        if (!MovementEnabled)
            return;

        Orthonormalize();

        var inputVec = Input.GetVector(
            GameActions.PlayerStrafeRight,
            GameActions.PlayerStrafeLeft,
            GameActions.PlayerBackward,
            GameActions.PlayerForward
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

        // Dev mode jetpack
        if (DEV_MODE && Input.IsActionPressed(GameActions.PlayerJump))
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
    }

    void RunAnimations()
    {
        var horizontalSpeed = GlobalBasis.Inverse() * LinearVelocity;
        horizontalSpeed.Y = 0;
        if (movementVec.Length() > 0.1 && (horizontalSpeed.Length() > 0.2))
        {
            Data.CurrentAnim = (byte)PlayerAnims.Run;
        }
        else
        {
            Data.CurrentAnim = (byte)PlayerAnims.Idle;
        }

        animPlayer.Play(Data.GetAnimation(), customBlend: 1);
    }
}
