namespace Game.World;

using Game.Terrain;

// This class provides references to the main parts of a world
public class WorldComponents
{
    // Chunk manager for world generation (might want to integrate it
    // into a worldGenerator class or something later)
    public ChunkManager ChunkManager { get; set; } = null;
}
