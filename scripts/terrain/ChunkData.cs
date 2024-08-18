namespace Game.Terrain;

using System.Runtime.InteropServices;
using Godot;
using MessagePack;

// If needed, refactor to non-public struct fields, only
// set up that way for simplicity + testing
public static class ChunkData
{
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct Vertex(Vector3 Position, Vector3 Normal);

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct TriangleIndex(int A, int B, int C);

    [MessagePackObject]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public readonly record struct ChunkID
    {
        [Key(0)]
        public readonly int X;

        [Key(1)]
        public readonly int Y;

        [Key(2)]
        public readonly int Z;

        public ChunkID(int X, int Y, int Z)
        {
            this.X = X;
            this.Y = Y;
            this.Z = Z;
        }

        public ChunkID(Vector3I coord)
        {
            X = coord.X;
            Y = coord.Y;
            Z = coord.Z;
        }

        public static ChunkID GetNearestID(Vector3 position)
        {
            // Put this global position into chunk space
            position /= TerrainData.CHUNK_SIZE;

            // Snap it to the nearest chunk
            return new(
                Mathf.RoundToInt(position.X),
                Mathf.RoundToInt(position.Y),
                Mathf.RoundToInt(position.Z)
            );
        }

        public Vector3 GetSampleVector()
        {
            return new Vector3(X, Y, Z);
        }

        public Vector3I GetSampleVector3I()
        {
            return new Vector3I(X, Y, Z);
        }
    }

    [MessagePackObject]
    public record struct TerrainParameters(
        [property: Key(0)] float NoiseScale,
        [property: Key(1)] Vector3 NoiseOffset
    );
}
