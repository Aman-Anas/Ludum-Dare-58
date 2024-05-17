using System;
using System.Text;
using Godot;
using NathanHoad;

public partial class KeymapLine : HBoxContainer
{
    [Export]
    public Label ActionLabel { get; set; }

    [Export]
    public Button AddButton { get; set; }

    [Export]
    Button ClearButton { get; set; }

    [Export]
    public Label BindedActions { get; set; }

    StringName actionName;

    public event Action RebindTriggered;

    public event Action RebindComplete;

    bool rebinding;

    public override void _Ready()
    {
        AddButton.Pressed += () => RebindTriggered?.Invoke();
        AddButton.Pressed += () => rebinding = true;
    }

    public void AssignAction(StringName actionName)
    {
        this.actionName = actionName;
        ActionLabel.Text = actionName;
        UpdateBindedActions();
    }

    public void UpdateBindedActions()
    {
        var binded = InputMap.ActionGetEvents(actionName);
        StringBuilder actions = new();
        foreach (var bindedEvent in binded)
        {
            actions.Append(InputHelper.GetLabelForInput(bindedEvent));
            actions.Append(", ");
        }
        BindedActions.Text = actions.ToString().TrimSuffix(", ");
    }

    public override void _UnhandledInput(InputEvent @event)
    {
        if (rebinding)
        {
            InputHelper.SetKeyboardOrJoypadInputForAction(actionName, @event, false);
            UpdateBindedActions();
            rebinding = false;
            AcceptEvent();
            RebindComplete?.Invoke();
        }
    }
}
