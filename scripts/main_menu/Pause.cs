using System;
using Game;
using Godot;
using Utilities.Logic;

public partial class Pause : Control
{
    SubMenuHelper menu;

    [Export]
    Button resumeButton;

    [Export]
    Button settingsButton;

    [Export]
    Button quitButton;

    [Export]
    Button backButton;

    [Export]
    Control pauseMenuRoot;

    [Export]
    Control settingsMenuRoot;

    Input.MouseModeEnum prevMouseMode = Input.MouseModeEnum.Captured;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        menu = new SubMenuHelper(backButton, pauseMenuRoot);
        resumeButton.Pressed += Resume;
        settingsButton.Pressed += () => menu.SetSubMenu(settingsMenuRoot);
        quitButton.Pressed += () => GetTree().Quit();
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
        if (Input.IsActionJustPressed(GameActions.PAUSE_GAME))
        {
            if (GetTree().Paused)
            {
                Resume();
            }
            else
            {
                GetTree().Paused = true;
                Show();
                prevMouseMode = Input.MouseMode;
                Input.MouseMode = Input.MouseModeEnum.Visible;
            }
        }
    }
}
