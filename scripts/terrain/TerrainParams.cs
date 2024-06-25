namespace Game.Terrain;

using System;

public static class TerrainParams
{
    // Terrain size and resolution
    public const float CHUNK_SIZE = 30;
    public const int TERRAIN_RESOLUTION = 4;
    const int WORK_GROUP_SIZE = 4;
    public const int NUM_VOXELS_PER_AXIS = WORK_GROUP_SIZE * TERRAIN_RESOLUTION;

    public const int MAX_TRIANGLES_PER_VOXEL = 5;

    public const int MAX_TRIANGLES =
        MAX_TRIANGLES_PER_VOXEL * NUM_VOXELS_PER_AXIS * NUM_VOXELS_PER_AXIS * NUM_VOXELS_PER_AXIS; // 3 dimensions

    const int FLOATS_PER_TRI = 6 * 3; // 6 floats per vertex, 3 vertices per tri
    public const int BYTES_PER_TRI = FLOATS_PER_TRI * sizeof(float);

    const int MAX_MOD_POINTS =
        (NUM_VOXELS_PER_AXIS + 2) * (NUM_VOXELS_PER_AXIS + 2) * (NUM_VOXELS_PER_AXIS + 2);

    public const int MAX_MOD_BYTES = MAX_MOD_POINTS * sizeof(float);

    public const int CHUNK_VIEW_DIST = 2; // Distance of viewable chunks (in 3 dimensions)
}
