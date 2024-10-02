namespace Game.Entities;

using Game.Networking;
using Godot;

public partial class StaticProp : StaticBody3D, INetEntity
{
    public uint EntityID { get; set; }

    public EntityData Data
    {
        get => _data;
        set => _data = (StaticPropData)value;
    }

    StaticPropData _data;

    // literally does nothing
}
