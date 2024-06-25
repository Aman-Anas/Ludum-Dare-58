namespace Game.Terrain;

using System.Runtime.InteropServices;
using Godot;

// If needed, refactor to non-public struct fields, only
// set up that way for simplicity + testing
public static class ChunkData
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Vertex
    {
        public float posX;
        public float posY;
        public float posZ;

        public float normX;
        public float normY;
        public float normZ;
    }

    // Csharp structure to use for chunks
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct Triangle
    {
        public Vertex vertexA;
        public Vertex vertexB;
        public Vertex vertexC;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct ChunkID(int x, int y, int z)
    {
        public readonly int posX = x;
        public readonly int posY = y;
        public readonly int posZ = z;

        // For debug
        public override readonly string ToString()
        {
            return posX + " " + posY + " " + posZ;
        }

        public static ChunkID GetNearestID(Vector3 position)
        {
            // Put this global position into chunk space
            position /= TerrainParams.CHUNK_SIZE;

            // Snap it to the nearest chunk
            return new(
                Mathf.RoundToInt(position.X),
                Mathf.RoundToInt(position.Y),
                Mathf.RoundToInt(position.Z)
            );
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct ChunkParameters
    {
        public float noiseScale;
        public float isoLevel;
        public int numVoxelsPerAxis;
        public float chunkScale;
        public float chunkX;
        public float chunkY;
        public float chunkZ;
        public float noiseOffsetX;
        public float noiseOffsetY;
        public float noiseOffsetZ;
        public int useMods; // negative if don't positive if do
    }
}
