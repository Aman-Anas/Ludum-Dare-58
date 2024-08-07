namespace Game.Terrain;

using System;
using System.Runtime.CompilerServices;
using Godot;

public static class TerrainData
{
    // Terrain chunk size
    public const float CHUNK_SIZE = 15;

    // Resolution on each axis per chunk
    public const int VOXELS_PER_AXIS = 16;

    // Median byte value for isolevel
    public const byte CENTER_ISOLEVEL = 128;

    public static readonly Vector3 VoxelAxisLengths =
        new(VOXELS_PER_AXIS, VOXELS_PER_AXIS, VOXELS_PER_AXIS);

    // Number of voxels in each chunk
    public const int VOXELS_PER_CHUNK = VOXELS_PER_AXIS * VOXELS_PER_AXIS * VOXELS_PER_AXIS;

    // The number of points to sample for weight
    public const int SAMPLE_POINTS_PER_AXIS = VOXELS_PER_AXIS + 1;

    // This will make the array bigger at the ends for shared normals
    public const int SAMPLE_ARRAY_PER_AXIS = SAMPLE_POINTS_PER_AXIS + 2;

    public const int SAMPLE_ARRAY_SIZE =
        SAMPLE_ARRAY_PER_AXIS * SAMPLE_ARRAY_PER_AXIS * SAMPLE_ARRAY_PER_AXIS;

    public const int CHUNK_VIEW_DIST = 3; // Distance of viewable chunks (in each 3 dimensions)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Coord3DToIndex(Vector3I coord, int axisLength)
    {
        return (coord.Z * axisLength * axisLength) + (coord.Y * axisLength) + coord.X;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3I IndexToCoord3D(int index, int axisLength)
    {
        int xQuotient = Math.DivRem(index, axisLength, out int x);
        int yQuotient = Math.DivRem(xQuotient, axisLength, out int y);
        int z = yQuotient % axisLength;
        return new Vector3I(x, y, z);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 CoordToChunkSpace(Vector3I coord)
    {
        // -0.5 to 0.5  position relative to chunk
        return (((Vector3)coord) / VoxelAxisLengths) - new Vector3(0.5f, 0.5f, 0.5f);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ChunkToWorldSpace(Vector3 position, Vector3I chunkCoord)
    {
        return (position + chunkCoord) * CHUNK_SIZE;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 ChunkToRelativeWorldSpace(Vector3 position)
    {
        return position * CHUNK_SIZE;
    }
}
