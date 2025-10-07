using System;
using Game;
using Godot;

public partial class Killzone : Area3D
{
    int initialHp;
    int initialSheep;

    public override void _Ready()
    {
        BodyEntered += DoSomething;
        initialHp = Manager.Instance.Data.CurrentHealth;
        initialSheep = Manager.Instance.Data.CurrentSheepCount;
    }

    private void DoSomething(Node3D body)
    {
        if (body is Farmer)
        {
            Manager.Instance.Data.CurrentHealth = initialHp;
            Manager.Instance.Data.CurrentSheepCount = initialSheep;
            GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.

    public override void _PhysicsProcess(double delta)
    {
        if (Manager.Instance.Data.CurrentHealth <= 0)
        {
            Manager.Instance.Data.CurrentHealth = initialHp;
            Manager.Instance.Data.CurrentSheepCount = initialSheep;
            GetTree().CallDeferred(SceneTree.MethodName.ReloadCurrentScene);
        }
    }
}
