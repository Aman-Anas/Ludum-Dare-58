namespace Game;

using Godot;

public static class GameActions
{
    public static readonly StringName PLAYER_FORWARD = new("player_forward");
    public static readonly StringName PLAYER_BACKWARD = new("player_backward");
    public static readonly StringName PLAYER_STRAFE_RIGHT = new("player_strafe_right");
    public static readonly StringName PLAYER_STRAFE_LEFT = new("player_strafe_left");
    public static readonly StringName PLAYER_ROLL_RIGHT = new("player_roll_right");
    public static readonly StringName PLAYER_ROLL_LEFT = new("player_roll_left");
    public static readonly StringName PLAYER_JUMP = new("player_jump");

    public static readonly StringName PAUSE_GAME = new("pause_game");
}
