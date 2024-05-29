using Godot;
using Utilities.Logic;

public partial class MainMenu : Control
{
    // Buttons used in interface
    [Export]
    Button PlayButton;

    [Export]
    Button HelpButton;

    [Export]
    Button QuitButton;

    [Export]
    Button SettingsButton;

    [Export]
    Button HomeButton;

    // The main menu screens
    [Export]
    Control MainMenuRoot;

    [Export]
    Control HelpMenu;

    [Export]
    Control SettingsMenu;

    [Export]
    PackedScene mainGameScene;

    // Helper to manage side menus
    SubMenuHelper mainHelper;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        mainHelper = new(HomeButton, MainMenuRoot);

        PlayButton.Pressed += StartGame;
        HelpButton.Pressed += () => mainHelper.SetSubMenu(HelpMenu);
        SettingsButton.Pressed += () => mainHelper.SetSubMenu(SettingsMenu);
        QuitButton.Pressed += () => GetTree().Quit();
    }

    void StartGame()
    {
        //start the game
        GetTree().ChangeSceneToPacked(mainGameScene);
    }
}
