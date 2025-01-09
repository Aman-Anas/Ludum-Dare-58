namespace Game.Entities;

using System;
using System.Globalization;
using Godot;

public partial class InventoryButton : Button
{
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
        if (
            @event is InputEventMouseButton btn
            && btn.Pressed
            && btn.ButtonIndex == MouseButton.Right
        )
        {
            if (Icon == null)
            {
                return;
            }

            if ((_stackNum / 2) < 1)
            {
                return;
            }

            var preview = new TextureRect
            {
                Texture = Icon,
                CustomMinimumSize = this.CustomMinimumSize * PreviewScale,
                ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
            };

            var label = new Label()
            {
                Text = $"{_stackNum / 2}",
                HorizontalAlignment = HorizontalAlignment.Right,
            };

            preview.AddChild(label);
            label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);

            Disabled = true;
            StackCountLabel.Text = $"{_stackNum - (_stackNum / 2)}";

            StackCountLabel.Visible = true;

            ForceDrag(new long[] { ButtonIndex, 0, _stackNum / 2 }, preview);
        }
    }

    public override Variant _GetDragData(Vector2 atPosition)
    {
        if (Icon == null)
        {
            return default;
        }

        var preview = new TextureRect
        {
            Texture = Icon,
            CustomMinimumSize = this.CustomMinimumSize * PreviewScale,
            ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize
        };

        var label = new Label()
        {
            Text = StackCountLabel.Text,
            HorizontalAlignment = HorizontalAlignment.Right,
        };

        preview.AddChild(label);
        label.SetAnchorsAndOffsetsPreset(LayoutPreset.BottomRight);

        Disabled = true;
        StackCountLabel.Visible = false;

        SetDragPreview(preview);

        // src, dest, count
        return new long[] { ButtonIndex, 0, _stackNum };
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd)
        {
            Disabled = false;
            StackCountLabel.Visible = true;

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
