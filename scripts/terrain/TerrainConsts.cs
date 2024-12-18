namespace Game.Terrain;

using System;
using System.Runtime.CompilerServices;
using Godot;

public static class TerrainConsts
{
    // Terrain chunk size
    public const float ChunkScale = 64;

    // Resolution on each axis per chunk
    public const int VoxelsPerAxis = 32;

    public const int VoxelsPerAxisMinusOne = VoxelsPerAxis - 1;
    public const int VoxelsPerAxisMinusTwo = VoxelsPerAxis - 2;

    // // Median byte value for isolevel
    // public const byte CenterLevel = 128;

    public static readonly Vector3 VoxelAxisLengths =
        new(VoxelsPerAxis, VoxelsPerAxis, VoxelsPerAxis);

    public static readonly Vector3 VoxelAxisLengthsMinusOne =
        new(VoxelsPerAxisMinusOne, VoxelsPerAxisMinusOne, VoxelsPerAxisMinusOne);

    public static readonly Vector3 VoxelAxisLengthsMinusTwo =
        new(VoxelsPerAxisMinusTwo, VoxelsPerAxisMinusTwo, VoxelsPerAxisMinusTwo);

    public static readonly Vector3 HalfVoxelAxisLengths = VoxelAxisLengths / 2f;

    // Number of voxels in each chunk
    public const int VoxelsPerChunk = VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis;

    // Length of our input sample array
    public const int VoxelArrayLength = VoxelsPerAxis * VoxelsPerAxis * VoxelsPerAxis;

    public const int ChunkViewDistance = 2; // Distance of viewable chunks (in each 3 dimensions)

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Coord3DToIndex(Vector3I coord, int axisLength)
    {
        return (coord.X * axisLength * axisLength) + (coord.Y * axisLength) + coord.Z;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3I IndexToCoord3D(int index, int axisLength)
    {
        int zQuotient = Math.DivRem(index, axisLength, out int z);
        int yQuotient = Math.DivRem(zQuotient, axisLength, out int y);
        int x = yQuotient % axisLength;
        return new Vector3I(x, y, z);
    }

    public static Vector3I GetNearestChunkID(Vector3 position)
    {
        // Put this global position into chunk space
        position /= TerrainConsts.ChunkScale;

        // Snap it to the nearest chunk
        return new(
            Mathf.RoundToInt(position.X),
            Mathf.RoundToInt(position.Y),
            Mathf.RoundToInt(position.Z)
        );
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector3 CoordToChunkSpace(Vector3I coord)
    {
        // -0.5 to 0.5  position relative to chunk
        return (((Vector3)coord) / VoxelAxisLengthsMinusTwo) - new Vector3(0.5f, 0.5f, 0.5f);
    }

    // [MethodImpl(MethodImplOptions.AggressiveInlining)]
    // public static Vector3 ChunkToWorldSpace(Vector3 position, Vector3I chunkCoord)
    // {
    //     return (position + chunkCoord) * ChunkScale;
    // }
}
