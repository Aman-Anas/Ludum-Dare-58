namespace Game;

using Godot;

public static class GameActions
{
    public static readonly StringName PlayerForward = new("player_forward");
    public static readonly StringName PlayerBackward = new("player_backward");
    public static readonly StringName PlayerStrafeRight = new("player_strafe_right");
    public static readonly StringName PlayerStrafeLeft = new("player_strafe_left");
    public static readonly StringName PlayerRollRight = new("player_roll_right");
    public static readonly StringName PlayerRollLeft = new("player_roll_left");
    public static readonly StringName PlayerJump = new("player_jump");

    public static readonly StringName InventoryToggle = new("inventory_toggle");
    public static readonly StringName InventorySingleSelect = new("inventory_single_select");

    public static readonly StringName PlayerPrimaryUse = new("player_primary_use");
    public static readonly StringName PlayerSecondaryUse = new("player_secondary_use");

    public static readonly StringName DropItem = new("player_drop_item");

    public static readonly StringName PauseGame = new("pause_game");
}
