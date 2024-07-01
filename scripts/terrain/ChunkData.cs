namespace Game.Terrain;

using System.Runtime.InteropServices;
using Godot;

// If needed, refactor to non-public struct fields, only
// set up that way for simplicity + testing
public static class ChunkData
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly struct Vertex
    {
        public readonly float posX;
        public readonly float posY;
        public readonly float posZ;

        public readonly float normX;
        public readonly float normY;
        public readonly float normZ;
    }

    // Csharp structure to use for chunks
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct Triangle
    {
        public readonly Vertex vertexA;
        public readonly Vertex vertexB;
        public readonly Vertex vertexC;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct ChunkID
    {
        public readonly int posX;
        public readonly int posY;
        public readonly int posZ;

        public ChunkID(int X, int Y, int Z)
        {
            posX = X;
            posY = Y;
            posZ = Z;
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

        public Vector3 GetSampleVector()
        {
            return new Vector3(posX, posY, posZ);
        }
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public record struct ChunkParameters
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
        public int useMods; // 0 if don't 1 if do
    }
}
