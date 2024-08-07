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

    // Maps vertex IDs to their index in the surface array
    readonly Dictionary<(int, int), int> existingVertexIDs = [];

    // For the Godot surface array
    readonly List<Vector3> verts = [];
    readonly List<Vector3> normals = [];
    readonly List<TriangleIndex> indices = [];

    readonly List<Vector3> collisionVertices = [];
    Vector3[] collisionVertexArray;

    const int INDICES_PER_TRI = 3;

    ChunkID currentChunkID;
    Vector3I chunkSampleCoord;

    public void ProcessChunk(ChunkID cID, byte[] terrainData) //, Stopwatch s)
    {
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

        Parallel.For(0, TerrainData.VOXELS_PER_CHUNK, (x) => ProcessVoxel(x, terrainData));
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

        // s.Stop();
        // GD.Print("finalize mesh ", s.Elapsed.TotalMicroseconds);
        // s.Restart();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetSampleIndex(Vector3I coord)
    {
        coord += Vector3I.One;
        return TerrainData.Coord3DToIndex(coord, TerrainData.SAMPLE_ARRAY_PER_AXIS);
    }

    static float GetFloatAtCoord(Vector3I coord, ReadOnlySpan<byte> data)
    {
        return Mathf.Remap(
            (float)(data[GetSampleIndex(coord)]),
            byte.MinValue,
            byte.MaxValue,
            -1.0f,
            1.0f
        );
    }

    void ProcessVoxel(int voxelIndex, ReadOnlySpan<byte> terrainData)
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
        Span<byte> currentTriangulation = MarchingCubeTables.EdgeTable[cubeConfig];

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
                    GenerateVertex(cubeCorners[a2], cubeCorners[b2], terrainData, out var pos1),
                    GenerateVertex(cubeCorners[a1], cubeCorners[b1], terrainData, out var pos2),
                    GenerateVertex(cubeCorners[a0], cubeCorners[b0], terrainData, out var pos3)
                );
            lock (indices)
            {
                indices.Add(newTriIndex);
            }

            // shouldn't be necessary
            // if (pos1.IsEqualApprox(pos2) || pos1.IsEqualApprox(pos3) || pos2.IsEqualApprox(pos3))
            // {
            //     continue;
            // }

            lock (collisionVertices)
            {
                collisionVertices.Add(pos1);
                collisionVertices.Add(pos2);
                collisionVertices.Add(pos3);
            }

            // flat normals
            // vec3 ab = currTri.b.xyz - currTri.a.xyz;
            // vec3 ac = currTri.c.xyz - currTri.a.xyz;
            // currTri.norm = -vec4(normalize(cross(ab,ac)), 0);
        }
    }

    // Returns the index of the vertex in the vert list
    int GenerateVertex(
        Vector3I localPosA,
        Vector3I localPosB,
        ReadOnlySpan<byte> terrainData,
        out Vector3 vertexPos
    )
    {
        const float isoLevel = 0;

        var worldVec1 = TerrainData.ChunkToRelativeWorldSpace(
            TerrainData.CoordToChunkSpace(localPosA)
        );
        var worldVec2 = TerrainData.ChunkToRelativeWorldSpace(
            TerrainData.CoordToChunkSpace(localPosB)
        );

        float n1 = GetFloatAtCoord(localPosA, terrainData);
        float n2 = GetFloatAtCoord(localPosB, terrainData);
        // GD.Print(n1);

        float t = (isoLevel - n1) / (n2 - n1);

        Vector3 position;
        if (Math.Abs(isoLevel - n1) < 0.0001f)
        {
            position = worldVec1;
        }
        else if (Math.Abs(isoLevel - n2) < 0.0001f)
        {
            position = worldVec2;
        }
        else if (Math.Abs(n1 - n2) < 0.0001f)
        {
            position = worldVec1;
        }
        else
        {
            // position = (worldVec1 + worldVec2) * 0.5f; // blocky version
            position = worldVec1 + (t * (worldVec2 - worldVec1));
            // position = MCInterpolateNoCracks(worldVec1, worldVec2, n1, n2, isoLevel);
        }
        // Vector3 position = worldVec1 + (t * (worldVec2 - worldVec1));

        // (Vector3I, Vector3I) posID;
        // if (localPosA > localPosB)
        // {
        //     posID = (localPosB, localPosA);
        // }
        // else
        // {
        //     posID = (localPosA, localPosB);
        // }

        var posID = GetVertexID(localPosA, localPosB);
        vertexPos = position;

        // GD.Print(position);
        Vector3 normalA = GetCoordinateNormal(localPosA, terrainData);
        Vector3 normalB = GetCoordinateNormal(localPosB, terrainData);
        Vector3 normal = (normalA + (t * (normalB - normalA))).Normalized();

        int thisVertexID;

        lock (existingVertexIDs)
        {
            if (!existingVertexIDs.TryGetValue(posID, out thisVertexID))
            {
                lock (verts)
                {
                    lock (normals)
                    {
                        verts.Add(position);
                        normals.Add(normal);
                        existingVertexIDs[posID] = verts.Count - 1;
                        thisVertexID = verts.Count - 1;
                    }
                }
            }
        }
        return thisVertexID;

        // if (!existingVertexIDs.ContainsKey(posID))
        // {
        //     existingVertexIDs[posID] = verts.Count;
        //     verts.Add(position);
        //     normals.Add(normal);
        // }
        // return existingVertexIDs[posID];
    }

    static Vector3 GetCoordinateNormal(Vector3I coord, ReadOnlySpan<byte> terrainData)
    {
        Vector3I offsetX = new(1, 0, 0);
        Vector3I offsetY = new(0, 1, 0);
        Vector3I offsetZ = new(0, 0, 1);

        Vector3 derivative =
            new(
                GetFloatAtCoord(coord + offsetX, terrainData)
                    - GetFloatAtCoord(coord - offsetX, terrainData),
                GetFloatAtCoord(coord + offsetY, terrainData)
                    - GetFloatAtCoord(coord - offsetY, terrainData),
                GetFloatAtCoord(coord + offsetZ, terrainData)
                    - GetFloatAtCoord(coord - offsetZ, terrainData)
            );
        // GD.Print(derivative);

        return derivative.Normalized();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static (int, int) GetVertexID(Vector3I coord1, Vector3I coord2)
    {
        int index1 = TerrainData.Coord3DToIndex(coord1, TerrainData.SAMPLE_POINTS_PER_AXIS);
        int index2 = TerrainData.Coord3DToIndex(coord2, TerrainData.SAMPLE_POINTS_PER_AXIS);
        return (Math.Min(index1, index2), Math.Max(index1, index2));
    }

    void CreateMesh()
    {
        if (indices.Count >= INDICES_PER_TRI)
        {
            var vertexSpan = CollectionsMarshal.AsSpan(verts);
            var indexSpan = MemoryMarshal.Cast<TriangleIndex, int>(
                CollectionsMarshal.AsSpan(indices)
            );
            // Make our Godot array and throw the data in

            meshData[(int)Mesh.ArrayType.Vertex] = vertexSpan;
            meshData[(int)Mesh.ArrayType.Normal] = CollectionsMarshal.AsSpan(normals);
            meshData[(int)Mesh.ArrayType.Index] = indexSpan;

            collisionVertexArray = [.. collisionVertices];
        }

        chunkMesh.ClearSurfaces();
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

        chunkMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
        chunkMesh.SurfaceSetMaterial(0, chunkMaterial);

        chunkShape.Data = collisionVertexArray;
        // s.Stop();
        // GD.Print(s.ElapsedMilliseconds);

        // GD.Print("verts ", verts.Count);
        // GD.Print("norms ", normals.Count);
        // GD.Print("indices ", indices.Count);

        // collider.Shape = chunkMesh.CreateTrimeshShape();

        // can also check length of meshdata vertices but wanted to be sure
        // GD.Print("vertex count ", ((ArrayMesh)Mesh).SurfaceGetArrayLen(0));
        // GD.Print("tri count ", indices.Count / 3);
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
    }

    // Not needed
    // // Called every frame. 'delta' is the elapsed time since the previous frame.
    // public override void _Process(double delta) { }
}
