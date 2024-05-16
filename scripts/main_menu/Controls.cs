using Godot;
using NathanHoad;

public partial class Controls : GridContainer
{
    [Export]
    PackedScene keymapScene;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        KeymapLine keymapLine;
        foreach (var input in InputMap.GetActions())
        {
            keymapLine = keymapScene.Instantiate<KeymapLine>();
            this.AddChild(keymapLine);
            keymapLine.AssignAction(input);
        }
    }

    public void UpdateActionList()
    {
        foreach (var node in GetChildren())
        {
            if (node is KeymapLine keymapLine)
            {
                keymapLine.UpdateBindedActions();
            }
        }
    }
}
