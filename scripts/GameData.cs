namespace Game;

using MemoryPack;

[MemoryPackable]
public partial class GameData
{
    // Add game-related save data here
    public int CurrentSheepCount { get; set; } = 0;

    public int CurrentHealth { get; set; } = 200;
}
