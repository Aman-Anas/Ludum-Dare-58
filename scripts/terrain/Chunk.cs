namespace Game.Terrain;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Diagnostics;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;
using static Game.Terrain.ChunkData;

public partial class Chunk : MeshInstance3D
{
    [Export]
    StaticBody3D physicsBody;

    [Export]
    CollisionShape3D collider;

    [Export]
    Material chunkMaterial;

    StringName finalizeName = new(nameof(FinalizeInScene));

    // Mesh data stuff
    ArrayMesh chunkMesh = new();
    ConcavePolygonShape3D chunkShape = new();

    Godot.Collections.Array meshData = [];
    Godot.Collections.Dictionary collisionData = [];

    // Maps vertex IDs to their index in the surface array
    readonly Dictionary<(int, int), int> existingVertexIDs = [];

    // For the Godot surface array
    readonly List<Vector3> verts = [];
    readonly List<Vector3> normals = [];
    readonly List<TriangleIndex> indices = [];

    readonly List<Vector3> collisionVertices = [];
    Vector3[] collisionVertexArray;

    const int INDICES_PER_TRI = 3;

    Rid chunkShapeRid;
    Rid chunkMeshRid;
    Rid chunkMaterialRid;

    ChunkID currentChunkID;
    Vector3I chunkSampleCoord;

    byte[] terrainData;

    public unsafe void ProcessChunk(ChunkID cID, byte[] terrainData) //, Stopwatch s)
    {
        Stopwatch s = Stopwatch.StartNew();
        currentChunkID = cID;
        chunkSampleCoord = currentChunkID.GetSampleVector3I();

        // s.Restart();
        existingVertexIDs.Clear();
        verts.Clear();
        normals.Clear();
        indices.Clear();
        collisionVertices.Clear();

        // s.Stop();
        // GD.Print("clear lists ", s.Elapsed.TotalMicroseconds);
        // s.Restart();
        this.terrainData = terrainData;

        Parallel.For(0, TerrainData.VOXELS_PER_CHUNK, ProcessVoxel);
        // for (int x = 0; x < TerrainData.VOXELS_PER_CHUNK; x++)
        // {
        //     ProcessVoxel(x, terrainData);
        // }
        // s.Stop();
        // GD.Print("process voxels ", s.Elapsed.TotalMicroseconds);
        // s.Restart();

        CreateMesh();

        // s.Stop();
        // GD.Print("create mesh ", s.Elapsed.TotalMicroseconds);
        // s.Restart();

        // Now that the mesh has been generated, let's assign the mesh
        // FinalizeInScene();
        CallDeferred(finalizeName);

        s.Stop();
        // runningSum += s.ElapsedMilliseconds;
        // countChunks++;
        // GD.Print($"{runningSum / countChunks} avg");
        GD.Print($"time {s.Elapsed.TotalMilliseconds}");
        // s.Restart();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static unsafe int GetSampleIndex(Vector3I coord)
    {
        coord += Vector3I.One;
        return TerrainData.Coord3DToIndex(coord, TerrainData.SAMPLE_ARRAY_PER_AXIS);
    }

    unsafe float GetFloatAtCoord(Vector3I coord)
    {
        return Mathf.Remap(
            terrainData[GetSampleIndex(coord)],
            byte.MinValue,
            byte.MaxValue,
            -1.0f,
            1.0f
        );
    }

    unsafe void ProcessVoxel(int voxelIndex)
    {
        // this voxel coord from 0-15
        var voxelCoord = TerrainData.IndexToCoord3D(voxelIndex, TerrainData.VOXELS_PER_AXIS);
        const byte isoLevel = TerrainData.CENTER_ISOLEVEL;

        // 8 corner positions of the current cube
        Span<Vector3I> cubeCorners =
        [
            // Add this sample point to the corner array
            voxelCoord + new Vector3I(0, 0, 0),
            voxelCoord + new Vector3I(1, 0, 0),
            voxelCoord + new Vector3I(1, 0, 1),
            voxelCoord + new Vector3I(0, 0, 1),
            voxelCoord + new Vector3I(0, 1, 0),
            voxelCoord + new Vector3I(1, 1, 0),
            voxelCoord + new Vector3I(1, 1, 1),
            voxelCoord + new Vector3I(0, 1, 1),
        ];

        // Calculate unique index for each cube configuration.
        // There are 256 possible values
        // A value of 0 means cube is entirely inside surface; 255 entirely outside.
        // The value is used to look up the edge table, which indicates which edges
        // of the cube are cut by the isosurface.
        byte cubeConfig = 0;
        if (terrainData[GetSampleIndex(cubeCorners[0])] < isoLevel)
            cubeConfig |= 1;
        if (terrainData[GetSampleIndex(cubeCorners[1])] < isoLevel)
            cubeConfig |= 2;
        if (terrainData[GetSampleIndex(cubeCorners[2])] < isoLevel)
            cubeConfig |= 4;
        if (terrainData[GetSampleIndex(cubeCorners[3])] < isoLevel)
            cubeConfig |= 8;
        if (terrainData[GetSampleIndex(cubeCorners[4])] < isoLevel)
            cubeConfig |= 16;
        if (terrainData[GetSampleIndex(cubeCorners[5])] < isoLevel)
            cubeConfig |= 32;
        if (terrainData[GetSampleIndex(cubeCorners[6])] < isoLevel)
            cubeConfig |= 64;
        if (terrainData[GetSampleIndex(cubeCorners[7])] < isoLevel)
            cubeConfig |= 128;

        // Create triangles for current cube configuration
        // int numIndices = MarchingCubeTables.LUT_INDEX_LENGTHS[cubeConfig];
        // int offset = MarchingCubeTables.LUT_OFFSETS[cubeConfig];
        var currentTriangulation = MarchingCubeTables.EdgeTable[cubeConfig];

        for (int i = 0; i < currentTriangulation.Length; i += 3)
        {
            // Get indices of corner points A and B for each of the three edges
            // of the cube that need to be joined to form the triangle.
            int v0 = currentTriangulation[i];
            int a0 = MarchingCubeTables.CornerIndexAFromEdge[v0];
            int b0 = MarchingCubeTables.CornerIndexBFromEdge[v0];

            int v1 = currentTriangulation[i + 1];
            int a1 = MarchingCubeTables.CornerIndexAFromEdge[v1];
            int b1 = MarchingCubeTables.CornerIndexBFromEdge[v1];

            int v2 = currentTriangulation[i + 2];
            int a2 = MarchingCubeTables.CornerIndexAFromEdge[v2];
            int b2 = MarchingCubeTables.CornerIndexBFromEdge[v2];

            // Calculate vertex positions and add indices
            TriangleIndex newTriIndex =
                new(
                    GenerateVertex(cubeCorners[a2], cubeCorners[b2], out var pos1),
                    GenerateVertex(cubeCorners[a1], cubeCorners[b1], out var pos2),
                    GenerateVertex(cubeCorners[a0], cubeCorners[b0], out var pos3)
                );
            lock (indices)
            {
                indices.Add(newTriIndex);

                collisionVertices.Add(pos1);
                collisionVertices.Add(pos2);
                collisionVertices.Add(pos3);
            }
        }
    }

    // Returns the index of the vertex in the vert list
    unsafe int GenerateVertex(Vector3I localPosA, Vector3I localPosB, out Vector3 vertexPos)
    {
        const float isoLevel = 0;

        var worldVec1 = TerrainData.ChunkToRelativeWorldSpace(
            TerrainData.CoordToChunkSpace(localPosA)
        );
        var worldVec2 = TerrainData.ChunkToRelativeWorldSpace(
            TerrainData.CoordToChunkSpace(localPosB)
        );

        float n1 = GetFloatAtCoord(localPosA);
        float n2 = GetFloatAtCoord(localPosB);

        float t = (isoLevel - n1) / (n2 - n1);

        Vector3 position = worldVec1 + (t * (worldVec2 - worldVec1));

        var posID = GetVertexID(localPosA, localPosB);
        vertexPos = position;

        Vector3 normalA = GetCoordinateNormal(localPosA);
        Vector3 normalB = GetCoordinateNormal(localPosB);
        Vector3 normal = (normalA + (t * (normalB - normalA))).Normalized();

        int thisVertexID;

        lock (existingVertexIDs)
        {
            if (!existingVertexIDs.TryGetValue(posID, out thisVertexID))
            {
                verts.Add(position);
                normals.Add(normal);
                existingVertexIDs[posID] = verts.Count - 1;
                thisVertexID = verts.Count - 1;
            }
        }
        return thisVertexID;
    }

    unsafe Vector3 GetCoordinateNormal(Vector3I coord)
    {
        Vector3I offsetX = new(1, 0, 0);
        Vector3I offsetY = new(0, 1, 0);
        Vector3I offsetZ = new(0, 0, 1);

        Vector3 derivative =
            new(
                GetFloatAtCoord(coord + offsetX) - GetFloatAtCoord(coord - offsetX),
                GetFloatAtCoord(coord + offsetY) - GetFloatAtCoord(coord - offsetY),
                GetFloatAtCoord(coord + offsetZ) - GetFloatAtCoord(coord - offsetZ)
            );
        // GD.Print(derivative);

        return derivative.Normalized();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static unsafe (int, int) GetVertexID(Vector3I coord1, Vector3I coord2)
    {
        int index1 = TerrainData.Coord3DToIndex(coord1, TerrainData.SAMPLE_POINTS_PER_AXIS);
        int index2 = TerrainData.Coord3DToIndex(coord2, TerrainData.SAMPLE_POINTS_PER_AXIS);
        return (Math.Min(index1, index2), Math.Max(index1, index2));
    }

    void CreateMesh()
    {
        chunkMesh.ClearSurfaces();

        if (indices.Count >= INDICES_PER_TRI)
        {
            // Make our Godot array and throw the data in

            meshData[(int)Mesh.ArrayType.Vertex] = CollectionsMarshal.AsSpan(verts);
            meshData[(int)Mesh.ArrayType.Normal] = CollectionsMarshal.AsSpan(normals);
            meshData[(int)Mesh.ArrayType.Index] = MemoryMarshal.Cast<TriangleIndex, int>(
                CollectionsMarshal.AsSpan(indices)
            );

            collisionData["faces"] = CollectionsMarshal.AsSpan(collisionVertices);

            PhysicsServer3D.ShapeSetData(chunkShapeRid, collisionData);

            RenderingServer.MeshAddSurfaceFromArrays(
                chunkMeshRid,
                RenderingServer.PrimitiveType.Triangles,
                meshData
            );
            RenderingServer.MeshSurfaceSetMaterial(chunkMeshRid, 0, chunkMaterialRid);
        }
    }

    public void FinalizeInScene()
    {
        Position = (Vector3)chunkSampleCoord * TerrainData.CHUNK_SIZE;

        // Sometimes we'll have not enough vertices for a triangle
        if (indices.Count < INDICES_PER_TRI)
        {
            collider.Disabled = true;
            return;
        }
        collider.Disabled = false;
    }

    public void HibernateChunk()
    {
        collider.Disabled = true;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Ensure this chunk has the ArrayMesh
        this.Mesh = chunkMesh;
        collider.Shape = chunkShape;
        meshData.Resize((int)Mesh.ArrayType.Max);

        chunkShapeRid = chunkShape.GetRid();
        chunkMeshRid = chunkMesh.GetRid();
        chunkMaterialRid = chunkMaterial.GetRid();

        collisionData["backface_collision"] = false;
    }

    // Not needed
    // // Called every frame. 'delta' is the elapsed time since the previous frame.
    // public override void _Process(double delta) { }
}
