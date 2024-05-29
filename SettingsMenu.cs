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

    [Export]
    Controls controlsMenu;

    [Export]
    Button resetAllButton;

    [Export]
    Button revertButton;

    // A list of our menu options
    List<Control> menus;

    readonly SubMenuHelper menuHelper = new();

    Manager manager = Manager.Instance; // Cache the manager

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Define the settings menu order here
        menus = new List<Control> { graphicsMenu, controlsMenu };

        settingsSelect.Clear();
        for (int x = 0; x < menus.Count; x++)
        {
            settingsSelect.AddItem(menus[x].Name);
        }

        settingsSelect.ItemSelected += (index) => menuHelper.SetSubMenu(menus[(int)index]);
        menuHelper.SetSubMenu(menus[0]);

        resetAllButton.Pressed += ResetAllSettings;
        applyButton.Pressed += ApplyAllSettings;
        revertButton.Pressed += RevertAllSettings;
    }

    void ApplyAllSettings()
    {
        graphicsMenu.ApplySettings();
        Manager.Instance.SaveConfig();
    }

    void RevertAllSettings()
    {
        manager.LoadConfig();
        graphicsMenu.PopulateGeneralSettings();
        controlsMenu.UpdateActionList();
    }

    void ResetAllSettings()
    {
        manager.LoadConfig(true);
        graphicsMenu.PopulateGeneralSettings();
        controlsMenu.UpdateActionList();
    }
}
