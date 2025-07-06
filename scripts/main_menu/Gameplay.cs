using System;
using Game;
using Godot;

public partial class Gameplay : GridContainer, SettingsMenu.ISettingsSubMenu
{
    Manager gameManager = null!;

    [Export]
    SpinBox mouseSensitivityBox = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        gameManager = Manager.Instance;
        LoadSettings();
    }

    public void LoadSettings()
    {
        mouseSensitivityBox.Value = gameManager.Config.MouseSensitivity;
    }

    public void ApplySettings()
    {
        gameManager.Config.MouseSensitivity = (float)mouseSensitivityBox.Value;
    }
}
