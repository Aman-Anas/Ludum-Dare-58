namespace Game.World;

using Godot;

public partial class SectorKernel : Node3D
{
    [Export]
    public Node3D DefaultEntityRoot { get; set; } = null!;
}
