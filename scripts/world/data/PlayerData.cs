using MemoryPack;

namespace Game.World.Data;

// ;

[MemoryPackable]
public partial class PlayerData
{
    public uint CurrentSectorID { get; set; }

    public uint CurrentEntityID { get; set; }
}
