namespace Game.Entities;

using System;
using System.Collections.Generic;
using Godot;

// Player inventory GUI
public partial class InventoryUI : Control
{
    public const short NumSlots = (6 + 1) * 9;

    public const short NumHotbarSlots = 9;

    public const short XDim = 9;
    public const short YDim = 7;

    InventoryButton[] buttons = new InventoryButton[NumSlots];

    [Export]
    PackedScene buttonTemplate;

    [Export]
    GridContainer hotbarRoot;

    [Export]
    GridContainer mainGridRoot;

    public Action<short, short> TryInventoryMove { get; set; }

    public override void _Ready()
    {
        // clear existing nodes we use for testing
        foreach (var node in hotbarRoot.GetChildren())
        {
            node.QueueFree();
        }
        foreach (var node in mainGridRoot.GetChildren())
        {
            node.QueueFree();
        }

        for (short x = 0; x < NumSlots; x++)
        {
            var newSquare = buttonTemplate.Instantiate<InventoryButton>();
            newSquare.ButtonIndex = x;
            newSquare.Icon = null;
            // GD.Load<Texture2D>(
            //     @"res://addons/kenney_prototype_textures/green/texture_10.png"
            // );
            newSquare.StackCountLabel.Text = "";

            buttons[x] = newSquare;
            newSquare.TryMoved += (src, dest) =>
            {
                TryInventoryMove?.Invoke(src, dest);
                GD.Print($"tried to move {src} to {dest}");
            };

            if (x < NumHotbarSlots)
            {
                hotbarRoot.AddChild(newSquare);
            }
            else
            {
                mainGridRoot.AddChild(newSquare);
            }
        }
    }

    public void UpdateInventorySlots(Dictionary<short, InventoryItem> items)
    {
        for (short x = 0; x < NumSlots; x++)
        {
            var square = buttons[x];

            if (items.TryGetValue(x, out var item))
            {
                square.Icon = GD.Load<Texture2D>(item.StorableInterface.IconPath);
                square.StackCountLabel.Text = item.StackSize == 1 ? "" : $"{item.StackSize}";
            }
            else
            {
                square.Icon = null;
                square.StackCountLabel.Text = "";
            }
        }
    }
}
