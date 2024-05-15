using System.Collections.Generic;
using Game;
using Godot;
using Utilities.Logic;

public partial class SettingsMenu : MarginContainer
{
    // I have a feeling this is all over complicated
    // using Dear Imgui for this kinda stuff is just so much nicer

    [Export]
    Button applyButton;

    [Export]
    ItemList settingsSelect;

    [Export]
    Graphics graphicsMenu;

    // A list of our menu options
    List<Control> menus;

    readonly SubMenuHelper menuHelper = new();

    Manager manager = Manager.Instance; // Cache the manager

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        settingsSelect.Clear();

        // Define the settings menu order here
        menus = new List<Control> { graphicsMenu, };

        for (int x = 0; x < menus.Count; x++)
        {
            settingsSelect.AddItem(menus[x].Name);
        }

        settingsSelect.ItemSelected += SelectSetting;

        applyButton.Pressed += ApplyAllSettings;

        GD.Print(DisplayServer.WindowGetVsyncMode());
    }

    void SelectSetting(long index)
    {
        menuHelper.SetSubMenu(menus[(int)index]);
    }

    void ApplyAllSettings()
    {
        graphicsMenu.ApplySettings();
    }
}
