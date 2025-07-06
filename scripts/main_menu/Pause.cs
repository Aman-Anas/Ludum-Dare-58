using System;
using Game;
using Godot;
using Utilities.Logic;

public partial class Pause : Control
{
    SubMenuHelper menu = null!;

    [Export]
    Button resumeButton = null!;

    [Export]
    Button settingsButton = null!;

    [Export]
    Button quitButton = null!;

    [Export]
    Button backButton = null!;

    [Export]
    Button returnToTitle = null!;

    [Export]
    Control pauseMenuRoot = null!;

    [Export]
    Control settingsMenuRoot = null!;

    Input.MouseModeEnum prevMouseMode = Input.MouseModeEnum.Captured;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        menu = new SubMenuHelper(backButton, pauseMenuRoot);
        resumeButton.Pressed += Resume;
        settingsButton.Pressed += () => menu.SetSubMenu(settingsMenuRoot);
        returnToTitle.Pressed += Manager.Instance.ExitToTitle;

        quitButton.Pressed += Manager.Instance.QuitGame;
    }

    void Resume()
    {
        menu.CloseSubMenu();
        GetTree().Paused = false;
        Hide();
        Input.MouseMode = prevMouseMode;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (Input.IsActionJustPressed(GameActions.PauseGame))
        {
            if (Visible)
            {
                Resume();
            }
            else
            {
                // TODO: Make this actually pause only when in singleplayer mode
                GetTree().Paused = true;
                Show();
                prevMouseMode = Input.MouseMode;
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
        }
    }
}
