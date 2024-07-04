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
    readonly ConcurrentDictionary<(int, int), int> existingVertexIDs = new();

    // For the Godot surface array
    readonly List<Vector3> verts = [];
    readonly List<Vector3> normals = [];
    readonly List<TriangleIndex> indices = [];

    Vector3[] collisionVertices = [];

    const int INDICES_PER_TRI = 3;

    ChunkID currentChunkID;
    Vector3I chunkSampleCoord;

    // Stopwatch s = new();

    public void ProcessChunk(ChunkID cID, float[] terrainData)
    {
        // s.Restart();
        currentChunkID = cID;
        chunkSampleCoord = currentChunkID.GetSampleVector3I();

        existingVertexIDs.Clear();
        verts.Clear();
        normals.Clear();
        indices.Clear();

        Parallel.For(0, TerrainData.VOXELS_PER_CHUNK, (x) => ProcessVoxel(x, terrainData));
        // for (int x = 0; x < TerrainData.VOXELS_PER_CHUNK; x++)
        // {
        //     ProcessVoxel(x, terrainData);
        // }
        CreateMesh();

        // Now that the mesh has been generated, let's assign the mesh
        CallDeferredThreadGroup(finalizeName);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static int GetSampleIndex(Vector3I coord)
    {
        coord += Vector3I.One;
        return TerrainData.Coord3DToIndex(coord, TerrainData.SAMPLE_ARRAY_PER_AXIS);
    }

    void ProcessVoxel(int voxelIndex, ReadOnlySpan<float> terrainData)
    {
        // this voxel coord from 0-15
        var voxelCoord = TerrainData.IndexToCoord3D(voxelIndex, TerrainData.VOXELS_PER_AXIS);
        const float isoLevel = 0;

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
        int cubeConfig = 0;
        for (int i = 0; i < cubeCorners.Length; i++)
        {
            // Think of the configuration as an 8-bit binary number (each bit represents the state of a corner point).
            // The state of each corner point is either 0: above the surface, or 1: below the surface.
            // The code below sets the corresponding bit to 1, if the point is below the surface.
            if (terrainData[GetSampleIndex(cubeCorners[i])] < isoLevel)
            {
                cubeConfig |= 1 << i;
            }
        }
        // if (terrainData[GetSampleIndex(cubeCorners[0])] < isoLevel)
        //     cubeConfig |= 1;
        // if (terrainData[GetSampleIndex(cubeCorners[1])] < isoLevel)
        //     cubeConfig |= 2;
        // if (terrainData[GetSampleIndex(cubeCorners[2])] < isoLevel)
        //     cubeConfig |= 4;
        // if (terrainData[GetSampleIndex(cubeCorners[3])] < isoLevel)
        //     cubeConfig |= 8;
        // if (terrainData[GetSampleIndex(cubeCorners[4])] < isoLevel)
        //     cubeConfig |= 16;
        // if (terrainData[GetSampleIndex(cubeCorners[5])] < isoLevel)
        //     cubeConfig |= 32;
        // if (terrainData[GetSampleIndex(cubeCorners[6])] < isoLevel)
        //     cubeConfig |= 64;
        // if (terrainData[GetSampleIndex(cubeCorners[7])] < isoLevel)
        //     cubeConfig |= 128;

        // Create triangles for current cube configuration
        // int numIndices = MarchingCubeTables.LUT_INDEX_LENGTHS[cubeConfig];
        // int offset = MarchingCubeTables.LUT_OFFSETS[cubeConfig];
        Span<int> currentTriangulation = MarchingCubeTables.EdgeTable[cubeConfig];

        for (int i = 0; i < 16; i += 3)
        {
            // If edge index is -1, then no further vertices exist in this configuration
            if (currentTriangulation[i] == -1)
            {
                break;
            }
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
                    GenerateVertex(
                        cubeCorners[a2],
                        cubeCorners[b2],
                        terrainData,
                        isoLevel,
                        out var pos1
                    ),
                    GenerateVertex(
                        cubeCorners[a1],
                        cubeCorners[b1],
                        terrainData,
                        isoLevel,
                        out var pos2
                    ),
                    GenerateVertex(
                        cubeCorners[a0],
                        cubeCorners[b0],
                        terrainData,
                        isoLevel,
                        out var pos3
                    )
                );

            // lock (collisionVertices)
            // {
            //     collisionVertices.Add(pos1);
            //     collisionVertices.Add(pos2);
            //     collisionVertices.Add(pos3);
            // }
            lock (indices)
            {
                indices.Add(newTriIndex);
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
        ReadOnlySpan<float> terrainData,
        float isoLevel,
        out Vector3 vertexPos
    )
    {
        var worldVec1 = TerrainData.ChunkToRelativeWorldSpace(
            TerrainData.CoordToChunkSpace(localPosA)
        );
        var worldVec2 = TerrainData.ChunkToRelativeWorldSpace(
            TerrainData.CoordToChunkSpace(localPosB)
        );

        float n1 = terrainData[GetSampleIndex(localPosA)];
        float n2 = terrainData[GetSampleIndex(localPosB)];

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

        // GD.Print(position);
        Vector3 normalA = GetCoordinateNormal(localPosA, terrainData);
        Vector3 normalB = GetCoordinateNormal(localPosB, terrainData);
        Vector3 normal = (normalA + (t * (normalB - normalA))).Normalized();

        vertexPos = position;

        return existingVertexIDs.GetOrAdd(
            posID,
            (_) =>
            {
                lock (verts)
                {
                    lock (normals)
                    {
                        verts.Add(position);
                        normals.Add(normal);
                        return verts.Count - 1;
                    }
                }
            }
        );

        // if (!existingVertexIDs.ContainsKey(posID))
        // {
        //     existingVertexIDs[posID] = verts.Count;
        //     verts.Add(position);
        //     normals.Add(normal);
        // }
        // return existingVertexIDs[posID];
    }

    static Vector3 GetCoordinateNormal(Vector3I coord, ReadOnlySpan<float> terrainData)
    {
        Vector3I offsetX = new(1, 0, 0);
        Vector3I offsetY = new(0, 1, 0);
        Vector3I offsetZ = new(0, 0, 1);

        Vector3 derivative =
            new(
                terrainData[GetSampleIndex(coord + offsetX)]
                    - terrainData[GetSampleIndex(coord - offsetX)],
                terrainData[GetSampleIndex(coord + offsetY)]
                    - terrainData[GetSampleIndex(coord - offsetY)],
                terrainData[GetSampleIndex(coord + offsetZ)]
                    - terrainData[GetSampleIndex(coord - offsetZ)]
            );

        return derivative.Normalized();
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    static (int, int) GetVertexID(Vector3I coord1, Vector3I coord2)
    {
        int index1 = TerrainData.Coord3DToIndex(coord1, TerrainData.SAMPLE_POINTS_PER_AXIS);
        int index2 = TerrainData.Coord3DToIndex(coord2, TerrainData.SAMPLE_POINTS_PER_AXIS);
        return (Math.Min(index1, index2), Math.Max(index1, index2));
    }

    // void ProcessMeshData(Span<Triangle> triangles, uint count)
    // {
    //     numIndices = (int)(count * INDICES_PER_TRI);
    //     existingVertexIDs.Clear();
    //     verts.Clear();
    //     normals.Clear();

    //     // GD.Print("count ", count);
    //     if (numIndices > indices.Length)
    //     {
    //         Array.Resize(ref indices, numIndices);
    //     }

    //     int currentIndex = 0;
    //     for (int triIndex = 0; triIndex < count; triIndex++)
    //     {
    //         var currentTri = triangles[triIndex];
    //         int aIndex = GetVertexIndex(currentTri.vertexA);
    //         int bIndex = GetVertexIndex(currentTri.vertexB);
    //         int cIndex = GetVertexIndex(currentTri.vertexC);

    //         // If two indices are identical, that means something is wack
    //         // because that's not a triangle
    //         if ((aIndex == bIndex) || (aIndex == cIndex) || (bIndex == cIndex))
    //         {
    //             numIndices -= INDICES_PER_TRI;
    //             continue;
    //         }

    //         indices[currentIndex] = aIndex;
    //         currentIndex++;
    //         indices[currentIndex] = bIndex;
    //         currentIndex++;
    //         indices[currentIndex] = cIndex;
    //         currentIndex++;
    //     }
    // }

    void CreateMesh()
    {
        if (indices.Count >= INDICES_PER_TRI)
        {
            var vertexSpan = CollectionsMarshal.AsSpan(verts);
            var indexSpan = MemoryMarshal.Cast<TriangleIndex, int>(
                CollectionsMarshal.AsSpan(indices)
            );
            // Make our Godot array and throw the data in
            meshData.Resize((int)Mesh.ArrayType.Max);
            meshData[(int)Mesh.ArrayType.Vertex] = vertexSpan;
            meshData[(int)Mesh.ArrayType.Normal] = CollectionsMarshal.AsSpan(normals);
            meshData[(int)Mesh.ArrayType.Index] = indexSpan;

            if (collisionVertices.Length != indexSpan.Length)
            {
                collisionVertices = new Vector3[indexSpan.Length];
            }
            for (int x = 0; x < collisionVertices.Length; x++)
            {
                collisionVertices[x] = vertexSpan[indexSpan[x]];
            }
        }

        chunkMesh.ClearSurfaces();
    }

    // int GetVertexIndex(Vertex v)
    // {
    //     (int, int, int) id = GetVertexID(v);

    //     if (!existingVertexIDs.TryGetValue(id, out int index))
    //     {
    //         // If it doesn't exist yet, add it
    //         index = verts.Count;
    //         existingVertexIDs[id] = index;
    //         verts.Add(MemoryMarshal.AsRef<Vector3>(v.Position.));
    //         normals.Add(new Vector3(v.normX, v.normY, v.normZ));
    //     }
    //     // Yes, this is kind of a waste of data (duplicate verts coming from gpu) but
    //     // triangles are constructed in parallel on the GPU so we can't
    //     // know how they fit together ahead of time

    //     // An alternative to GetVertexID is to have a nice hashable ID for each voxel edge and
    //     // include it in the GPU data per vertex, but that balloons the total amount of data
    //     // coming from the GPU and GetVertexID() is simple and fast enough

    //     return index;
    // }

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

        chunkShape.Data = collisionVertices;
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
    }

    // Not needed
    // // Called every frame. 'delta' is the elapsed time since the previous frame.
    // public override void _Process(double delta) { }
}
