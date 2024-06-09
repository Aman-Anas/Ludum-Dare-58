using System;

public static class TerrainParams
{
    // Terrain size and resolution
    public const float CHUNK_SIZE = 10;
    public const int TERRAIN_RESOLUTION = 4;
    const int WORK_GROUP_SIZE = 8;
    public const int NUM_VOXELS_PER_AXIS = WORK_GROUP_SIZE * TERRAIN_RESOLUTION;

    public const int MAX_TRIANGLES_PER_VOXEL = 5;

    public static readonly int MAX_TRIANGLES =
        MAX_TRIANGLES_PER_VOXEL * (int)Math.Pow(NUM_VOXELS_PER_AXIS, 3);

    const int FLOATS_PER_TRI = 6 * 3; // 6 floats per vertex, 3 vertices per tri
    public const int BYTES_PER_TRI = FLOATS_PER_TRI * sizeof(float);

    public static readonly int MAX_MOD_POINTS = (int)Math.Pow(NUM_VOXELS_PER_AXIS + 2, 3);
}
