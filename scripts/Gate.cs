using System;
using Godot;

public partial class Gate : Area3D
{
    [Export(PropertyHint.File)]
    string nextScene = null!;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        BodyEntered += Entered;
    }

    private void Entered(Node3D body)
    {
        if (body is Farmer)
        {
            GetTree().ChangeSceneToFile(nextScene);
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.

    public override void _Process(double delta) { }
}
