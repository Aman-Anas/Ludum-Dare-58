namespace Game;

using System;
using MemoryPack;

[MemoryPackable]
public partial class GameData
{
    // Add game-related save data here
    public int CurrentSheepCount { get; set; } = 0;

    public int CurrentHealth { get; set; } = 200;

    public int FinalKilled { get; set; } = 0;

    [MemoryPackIgnore]
    public Action? FinalThingy { get; set; }
}
