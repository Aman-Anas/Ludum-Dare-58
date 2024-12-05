namespace Game.Entities;

using Game.Networking;
using Godot;

public partial class StaticProp : StaticBody3D, INetEntity<StaticPropData>
{
    public uint EntityID { get; set; }

    public StaticPropData Data { get; set; }

    EntityData INetEntity.Data
    {
        get => Data;
        set => Data = (StaticPropData)value;
    }

    // literally does nothing
}
