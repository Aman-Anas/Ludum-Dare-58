using System.Collections.Generic;
using Game;
using Godot;
using Utilities.Logic;

public partial class SettingsMenu : MarginContainer
{
    public interface ISettingsSubMenu
    {
        void LoadSettings();
        void ApplySettings();
        Control GetView()
        {
            return (Control)this;
        }
    }

    // I have a feeling this is all over complicated
    // using Dear Imgui for this kinda stuff is just so much nicer

    [Export]
    Button applyButton;

    [Export]
    ItemList settingsSelect;

    [Export]
    Control menuRoot;

    [Export]
    Button resetAllButton;

    [Export]
    Button revertButton;

    // A list of our menu options
    readonly List<ISettingsSubMenu> menus = new();
    readonly SubMenuHelper menuHelper = new();

    Manager manager = Manager.Instance; // Cache the manager

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        settingsSelect.Clear();

        // Define the settings menu order
        foreach (var child in menuRoot.GetChildren())
        {
            menus.Add((ISettingsSubMenu)child);
            settingsSelect.AddItem(child.Name);
        }

        // Set the right menu depending on the selection
        settingsSelect.ItemSelected += (index) =>
            menuHelper.SetSubMenu(menus[(int)index].GetView());

        menuHelper.SetSubMenu(menus[0].GetView());

        resetAllButton.Pressed += ResetAllSettings;
        applyButton.Pressed += ApplyAllSettings;
        revertButton.Pressed += RevertAllSettings;
    }

    void ApplyAllSettings()
    {
        foreach (var menu in menus)
        {
            menu.ApplySettings();
        }
        Manager.Instance.SaveConfig();
    }

    void RevertAllSettings()
    {
        manager.LoadConfig();
        foreach (var menu in menus)
        {
            menu.ApplySettings();
        }
    }

    void ResetAllSettings()
    {
        manager.LoadConfig(true);
        foreach (var menu in menus)
        {
            menu.LoadSettings();
        }
    }
}
