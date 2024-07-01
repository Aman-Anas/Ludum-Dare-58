namespace Game.Terrain;

using System;

public static class TerrainParams
{
    // Terrain size and resolution
    public const float CHUNK_SIZE = 10;
    public const int TERRAIN_RESOLUTION = 4;
    const int WORK_GROUP_SIZE = 4;
    public const int NUM_VOXELS_PER_AXIS = WORK_GROUP_SIZE * TERRAIN_RESOLUTION;

    public const int NUM_SAMPLE_POINTS_PER_AXIS = NUM_VOXELS_PER_AXIS + 1;
    public const int LENGTH_MOD_ARRAY_PER_AXIS = NUM_SAMPLE_POINTS_PER_AXIS + 2;

    public const int MAX_TRIANGLES_PER_VOXEL = 5;

    public const int MAX_TRIANGLES =
        MAX_TRIANGLES_PER_VOXEL * NUM_VOXELS_PER_AXIS * NUM_VOXELS_PER_AXIS * NUM_VOXELS_PER_AXIS; // 3 dimensions

    const int FLOATS_PER_TRI = 6 * 3; // 6 floats per vertex, 3 vertices per tri
    public const int BYTES_PER_TRI = FLOATS_PER_TRI * sizeof(float);

    // Bytes to allocate for triangles
    public const uint MAX_TRIANGLE_BYTES = BYTES_PER_TRI * MAX_TRIANGLES;

    const int MAX_MOD_POINTS =
        LENGTH_MOD_ARRAY_PER_AXIS * LENGTH_MOD_ARRAY_PER_AXIS * LENGTH_MOD_ARRAY_PER_AXIS;

    // TODO: potential optimization - Consider refactoring to use a single byte per mod point
    // Will have to pack and unpack in GLSL (i don't think it has "byte"), but it would greatly reduce array size.
    public const int MAX_MOD_BYTES = MAX_MOD_POINTS * sizeof(float);

    public const int CHUNK_VIEW_DIST = 2; // Distance of viewable chunks (in 3 dimensions)
}
