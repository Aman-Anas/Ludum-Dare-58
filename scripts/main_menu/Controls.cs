using Godot;
using NathanHoad;

public partial class Controls : GridContainer, SettingsMenu.ISettingsSubMenu
{
    [Export]
    PackedScene keymapScene;

    [Export]
    Control keyPressOverlay;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        KeymapLine keymapLine;
        foreach (var input in InputMap.GetActions())
        {
            keymapLine = keymapScene.Instantiate<KeymapLine>();
            this.AddChild(keymapLine);
            keymapLine.AssignAction(input);
            keymapLine.RebindTriggered += () => keyPressOverlay.Show();
            keymapLine.RebindComplete += () => keyPressOverlay.Hide();
        }
        keyPressOverlay.Hide();
    }

    public void LoadSettings()
    {
        foreach (var node in GetChildren())
        {
            if (node is KeymapLine keymapLine)
            {
                keymapLine.UpdateBindedActions();
            }
        }
    }

    public void ApplySettings() { }
}
