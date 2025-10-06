using System;
using Game;
using Godot;

public partial class Title : Node3D
{
    [Export]
    StaticBody3D PlayButton = null!;
    Node3D PlayRoot = null!;

    [Export]
    StaticBody3D HelpButton = null!;
    Node3D HelpRoot = null!;

    [Export]
    StaticBody3D QuitButton = null!;
    Node3D QuitRoot = null!;

    [Export(PropertyHint.FilePath)]
    string firstScenePath = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        PlayButton.InputEvent += (_, @event, _, _, _) =>
        {
            if (
                @event is InputEventMouseButton click
                && (click.IsPressed())
                && (click.ButtonIndex == MouseButton.Left)
            )
            {
                GD.Print("Play");
                GetTree().ChangeSceneToFile(firstScenePath);
            }
        };

        HelpButton.InputEvent += (_, @event, _, _, _) =>
        {
            if (
                @event is InputEventMouseButton click
                && (click.IsPressed())
                && (click.ButtonIndex == MouseButton.Left)
            )
            {
                GD.Print("Help");
                // GetTree().ChangeSceneToFile()
            }
        };
        QuitButton.InputEvent += (_, @event, _, _, _) =>
        {
            if (
                @event is InputEventMouseButton click
                && (click.IsPressed())
                && (click.ButtonIndex == MouseButton.Left)
            )
            {
                GD.Print("Quit");
                Manager.Instance.QuitGame();
            }
        };
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
