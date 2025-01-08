namespace Game.Entities;

using System;
using Godot;

public partial class InventoryButton : Button
{
    const float PreviewScale = 0.75f;

    [Export]
    public Label StackCountLabel { get; set; }

    public short ButtonIndex { get; set; }
    public Action<short, short> TryMoved { get; set; }

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

        return ButtonIndex;
    }

    public override void _Notification(int what)
    {
        if (what == NotificationDragEnd)
        {
            Disabled = false;
            StackCountLabel.Visible = true;
        }
    }

    public override bool _CanDropData(Vector2 atPosition, Variant data)
    {
        return ((short)data) != ButtonIndex;
    }

    public override void _DropData(Vector2 atPosition, Variant data)
    {
        TryMoved.Invoke((short)data, ButtonIndex);
    }
}
