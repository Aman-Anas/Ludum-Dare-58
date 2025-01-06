using MemoryPack;

namespace Game.World.Data;

// ;

[MemoryPackable]
public partial class PlayerData(
    ulong playerID,
    string username,
    uint currentSectorID,
    ulong currentEntityID,
    string password
)
{
    public ulong PlayerID { get; init; } = playerID;
    public string Username { get; init; } = username;

    public uint CurrentSectorID { get; set; } = currentSectorID;
    public ulong CurrentEntityID { get; set; } = currentEntityID;

    // TODO: Make this more secure with salt hash whatever
    public string Password { get; set; } = password;
}
