namespace Game.Terrain.Old;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using Godot;
using static Game.Terrain.Old.ChunkDataGPU;

// The original GPU implementation of a chunk
public partial class ChunkGPU : MeshInstance3D
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
    Godot.Collections.Array meshData = [];

    // Maps vertex IDs to their index in the surface array
    readonly Dictionary<(int, int, int), int> existingVertexIDs = [];

    // For the Godot surface array
    readonly List<Vector3> verts = [];
    readonly List<Vector3> normals = [];
    int[] indices = [];
    int numIndices;
    const int INDICES_PER_TRI = 3;

    public ChunkID CurrentChunkID { get; set; }

    public void ProcessChunk(Span<Triangle> triangles, uint count)
    {
        ProcessMeshData(triangles, count);
        CreateMesh();

        // Now that the mesh has been generated, let's generate the collision
        CallDeferredThreadGroup(finalizeName);
    }

    void ProcessMeshData(Span<Triangle> triangles, uint count)
    {
        numIndices = (int)(count * INDICES_PER_TRI);
        existingVertexIDs.Clear();
        verts.Clear();
        normals.Clear();

        // GD.Print("count ", count);
        if (numIndices > indices.Length)
        {
            Array.Resize(ref indices, numIndices);
        }

        int currentIndex = 0;
        for (int triIndex = 0; triIndex < count; triIndex++)
        {
            var currentTri = triangles[triIndex];
            int aIndex = GetVertexIndex(currentTri.vertexA);
            int bIndex = GetVertexIndex(currentTri.vertexB);
            int cIndex = GetVertexIndex(currentTri.vertexC);

            // If two indices are identical, that means something is wack
            // because that's not a triangle
            if ((aIndex == bIndex) || (aIndex == cIndex) || (bIndex == cIndex))
            {
                numIndices -= INDICES_PER_TRI;
                continue;
            }

            indices[currentIndex] = aIndex;
            currentIndex++;
            indices[currentIndex] = bIndex;
            currentIndex++;
            indices[currentIndex] = cIndex;
            currentIndex++;
        }
    }

    void CreateMesh()
    {
        if (numIndices >= INDICES_PER_TRI)
        {
            // TODO: Test that using AsSpan doesn't cause issues if I re-use a chunk
            // later... hopefully not
            meshData.Resize((int)Mesh.ArrayType.Max);
            meshData[(int)Mesh.ArrayType.Vertex] = CollectionsMarshal.AsSpan(verts);
            meshData[(int)Mesh.ArrayType.Normal] = CollectionsMarshal.AsSpan(normals);
            meshData[(int)Mesh.ArrayType.Index] = indices.AsSpan(0, numIndices);
        }
        chunkMesh.ClearSurfaces();
    }

    static (int, int, int) GetVertexID(Vertex v)
    {
        return (
            (int)(MathF.Round(v.posX, 3) * 1_000),
            (int)(MathF.Round(v.posY, 3) * 1_000),
            (int)(MathF.Round(v.posZ, 3) * 1_000)
        );
    }

    int GetVertexIndex(Vertex v)
    {
        (int, int, int) id = GetVertexID(v);

        if (!existingVertexIDs.TryGetValue(id, out int index))
        {
            // If it doesn't exist yet, add it
            index = verts.Count;
            existingVertexIDs[id] = index;
            verts.Add(new Vector3(v.posX, v.posY, v.posZ));
            normals.Add(new Vector3(v.normX, v.normY, v.normZ));
        }
        // Yes, this is kind of a waste of data (duplicate verts coming from gpu) but
        // triangles are constructed in parallel on the GPU so we can't
        // know how they fit together ahead of time

        // An alternative to GetVertexID is to have a nice hashable ID for each voxel edge and
        // include it in the GPU data per vertex, but that balloons the total amount of data
        // coming from the GPU and GetVertexID() is simple and fast enough

        return index;
    }

    public void FinalizeInScene()
    {
        Position = CurrentChunkID.GetSampleVector() * TerrainData.CHUNK_SIZE;

        // Sometimes we'll have not enough vertices for a triangle
        if (numIndices < INDICES_PER_TRI)
        {
            return;
        }

        chunkMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
        chunkMesh.SurfaceSetMaterial(0, chunkMaterial);
        collider.Shape = chunkMesh.CreateTrimeshShape();

        physicsBody.SetPhysicsProcess(true);
        physicsBody.SetProcess(true);

        // can also check length of meshdata vertices but wanted to be sure
        // GD.Print("vertex count ", ((ArrayMesh)Mesh).SurfaceGetArrayLen(0));
    }

    public void HibernateChunk()
    {
        physicsBody.SetPhysicsProcess(false);
        physicsBody.SetProcess(false);
        collider.Shape = null;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Ensure this chunk has the ArrayMesh
        this.Mesh = chunkMesh;
    }

    // Not needed
    // // Called every frame. 'delta' is the elapsed time since the previous frame.
    // public override void _Process(double delta) { }
}
