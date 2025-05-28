namespace Game.Entities;

using System;
using Godot;

public partial class InventoryButton : Button
{
    // TODO: Implement a custom drag n drop system to allow for more complex inventory shortcuts

    const float PreviewScale = 0.75f;

    [Export]
    public Label StackCountLabel { get; set; }

    public short ButtonIndex { get; set; }

    public Action<(short src, short dest, uint count)> TryMoved { get; set; }

    uint _stackNum;
    public uint StackNum
    {
        get => _stackNum;
        set
        {
            _stackNum = value;
            StackCountLabel.Text = _stackNum == 1 ? "" : $"{_stackNum}";
        }
    }

    public override void _GuiInput(InputEvent @event)
    {
        if (Icon == null)
        {
            return;
        }

        if (
            @event is InputEventMouseButton btn
            && btn.Pressed
            && btn.ButtonIndex == MouseButton.Right
        )
        {
            uint amount = Input.IsActionPressed(GameActions.InventorySplit) ? 1 : _stackNum / 2;

            if (((_stackNum - amount) < 0) || (amount == 0))
            {
                return;
            }

            var preview = GetPreview(amount);

            Disabled = true;
            StackCountLabel.Text = (_stackNum - amount) <= 1 ? "" : $"{_stackNum - amount}";

            // src, dest, count
            ForceDrag(new long[] { ButtonIndex, 0, amount }, preview);
        }
    }

    TextureRect GetPreview(uint amount)
    {
        var preview = new TextureRect
        {
            Texture = Icon,
            CustomMinimumSize = this.CustomMinimumSize * PreviewScale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
        };

        var label = new Label()
        {
            Text = (amount == 1) ? "" : $"{amount}",
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        preview.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);

        return preview;
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Icon == null)
        {
            return default;
        }

        uint amount = _stackNum;

        if (((_stackNum - amount) < 0) || (amount == 0))
        {
            return default;
        }

        var preview = GetPreview(amount);

        Disabled = true;
        StackCountLabel.Text = (_stackNum - amount) <= 1 ? "" : $"{_stackNum - amount}";

        SetDragPreview(preview);

        // src, dest, count
        return new long[] { ButtonIndex, 0, amount };
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd)
        {
            Disabled = false;

            if (Icon != null)
                StackCountLabel.Text = _stackNum == 1 ? "" : $"{_stackNum}";
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        var values = (long[])data;

        return ((short)values[0]) != ButtonIndex;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        var values = (long[])data;
        TryMoved.Invoke(((short)values[0], ButtonIndex, (uint)values[2]));
    }
}
