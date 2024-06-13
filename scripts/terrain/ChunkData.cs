namespace Game.Terrain;

using System.Runtime.InteropServices;

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
    public struct ChunkID
    {
        public int posX;
        public int posY;
        public int posZ;
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
