using MemoryPack;

namespace Game.World.Data;

// ;

[MemoryPackable]
public partial record PlayerData(uint CurrentSectorID, uint CurrentEntityID)
{
    // public uint CurrentSectorID { get; set; }

    // public uint CurrentEntityID { get; set; }
}
