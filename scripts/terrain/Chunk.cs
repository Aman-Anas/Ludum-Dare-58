namespace Game.Terrain;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
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
    Godot.Collections.Array meshData = [];

    // Maps vertex IDs to their index in the surface array
    readonly Dictionary<(int, int, int), int> existingVertexIDs = [];

    // For the Godot surface array
    readonly List<Vector3> verts = [];
    readonly List<Vector3> normals = [];
    int[] indices = [];
    int numIndices;

    public void ProcessChunk(Span<Triangle> triangles, uint count)
    {
        ProcessMeshData(triangles, count);
        CreateMesh();

        // Now that the mesh has been generated, let's generate the collision
        CallDeferredThreadGroup(finalizeName);
    }

    void ProcessMeshData(Span<Triangle> triangles, uint count)
    {
        numIndices = (int)(count * 3);
        existingVertexIDs.Clear();
        verts.Clear();
        normals.Clear();

        if (numIndices > indices.Length)
        {
            Array.Resize(ref indices, numIndices);
        }

        for (int triIndex = 0; triIndex < count; triIndex++)
        {
            var currentTri = triangles[triIndex];
            int aIndex = GetVertexIndex(currentTri.vertexA);
            int bIndex = GetVertexIndex(currentTri.vertexB);
            int cIndex = GetVertexIndex(currentTri.vertexC);
            indices[(triIndex * 3) + 0] = aIndex;
            indices[(triIndex * 3) + 1] = bIndex;
            indices[(triIndex * 3) + 2] = cIndex;
        }
    }

    void CreateMesh()
    {
        if (verts.Count > 0)
        {
            // TODO: Test that using AsSpan doesn't cause issues if I re-use a chunk
            // later... hopefully not
            meshData.Resize((int)Mesh.ArrayType.Max);
            meshData[(int)Mesh.ArrayType.Vertex] = CollectionsMarshal.AsSpan(verts);
            meshData[(int)Mesh.ArrayType.Normal] = CollectionsMarshal.AsSpan(normals);
            meshData[(int)Mesh.ArrayType.Index] = indices.AsSpan(0, numIndices);
            chunkMesh.ClearSurfaces();
        }
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

        return index;
    }

    public void FinalizeInScene()
    {
        if (verts.Count == 0)
        {
            return;
        }
        chunkMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
        collider.Shape = chunkMesh.CreateTrimeshShape();
        // collider
        chunkMesh.SurfaceSetMaterial(0, chunkMaterial);
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Ensure this chunk has an ArrayMesh
        this.Mesh = chunkMesh;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta) { }
}
