using System;
using System.Threading.Tasks;
using Game.Terrain;
using Godot;
using Godot.Collections;

public partial class TerrainGenerator : Node3D
{
    float noiseScale = 2.0f;
    Vector3 noiseOffset = new(0.0f, 0f, 0f);
    float isoLevel = 1.00f;
    float chunkScale = 1000;
    Vector3 playerPos = new(0, 0, 0);

    const int TERRAIN_RESOLUTION = 8;
    const int NUM_WAITFRAMES_GPUSYNC = 12;
    const int NUM_WAITFRAMES_MESHTHREAD = 90;

    const int WORK_GROUP_SIZE = 8;
    const int NUM_VOXELS_PER_AXIS = WORK_GROUP_SIZE * TERRAIN_RESOLUTION;

    const int BUFFER_SET_INDEX = 0;

    // Bind Indices
    const int TRIANGLE_BIND_INDEX = 0;
    const int PARAMS_BIND_INDEX = 1;
    const int COUNTER_BIND_INDEX = 2;
    const int LUT_BIND_INDEX = 3;

    const string SHADER_PATH = "res://scripts/terrain/MarchingCubes.glsl";

    RenderingDevice renderDevice;

    // Resource IDs
    Rid shader;
    Rid pipeline;
    Rid bufferSet;
    Rid triangleBuffer;
    Rid paramsBuffer;
    Rid counterBuffer;
    Rid lutBuffer;

    // Received data
    byte[] triangleDataBytes;
    byte[] counterDataBytes;
    int numTriangles;

    // Terrain mesh for testing
    ArrayMesh terrainMesh = new();
    Vector3[] verts = System.Array.Empty<Vector3>();
    Vector3[] normals = System.Array.Empty<Vector3>();

    // Measure state I guess
    float time = 0;
    int frameCount = 0;
    int lastComputeDispatch;
    int lastMeshthreadStart;
    bool waitingForCompute;
    bool waitingForMeshthread;

    Task meshProcess;

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Make one child node and assign it the mesh
        var newMesh = new MeshInstance3D { Name = "ChunkOne" };
        AddChild(newMesh);

        newMesh.Mesh = terrainMesh;

        InitializeCompute();
        RunNewCompute();
        FetchAndProcessCompute();
        CreateMesh();
    }

    void InitializeCompute()
    {
        renderDevice = RenderingServer.CreateLocalRenderingDevice();
        var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH);
        var shaderBytecode = shaderFile.GetSpirV();
        var shader = renderDevice.ShaderCreateFromSpirV(shaderBytecode);

        // Make triangle buffer
        const int MAX_TRIANGLES_PER_VOXEL = 5;
        int MAX_TRIANGLES = MAX_TRIANGLES_PER_VOXEL * (int)Math.Pow(NUM_VOXELS_PER_AXIS, 3);
        const int BYTES_PER_FLOAT = sizeof(float);
        const int FLOATS_PER_TRI = 4 * 3;
        const int BYTES_PER_TRI = FLOATS_PER_TRI * BYTES_PER_FLOAT;
        uint MAX_BYTES = (uint)(BYTES_PER_TRI * MAX_TRIANGLES);
        GD.Print("MAX_BYTES ", MAX_BYTES);
        GD.Print("Bytes per float", BYTES_PER_FLOAT);

        // Make our storage buffers (this one for triangles)
        triangleBuffer = InitStorageBuffer(
            out var trianglesUniform,
            MAX_BYTES,
            TRIANGLE_BIND_INDEX
        );

        // Buffer for our adjustable parameters
        var paramBytes = GetTerrainParametersBytes();

        paramsBuffer = InitStorageBuffer(
            out var paramsUniform,
            (uint)paramBytes.Length,
            PARAMS_BIND_INDEX,
            paramBytes
        );

        // So much boilerplate.... :(
        // Make a buffer for a counter (?)
        var counterBytes = GetResetCounterBytes();

        counterBuffer = InitStorageBuffer(
            out var counterUniform,
            (uint)counterBytes.Length,
            COUNTER_BIND_INDEX,
            counterBytes
        );

        // make our marching cubes LUT table buffer
        var lutBytes = new byte[MarchingCubesLUT.LUT_ARRAY.Length * sizeof(int)];
        Buffer.BlockCopy(MarchingCubesLUT.LUT_ARRAY, 0, lutBytes, 0, lutBytes.Length);

        lutBuffer = InitStorageBuffer(
            out var lutUniform,
            (uint)lutBytes.Length,
            LUT_BIND_INDEX,
            lutBytes
        );

        bufferSet = renderDevice.UniformSetCreate(
            new Array<RDUniform> { trianglesUniform, paramsUniform, counterUniform, lutUniform },
            shader,
            BUFFER_SET_INDEX
        );

        pipeline = renderDevice.ComputePipelineCreate(shader);
    }

    // Helper function to auto setup a storage buffer
    Rid InitStorageBuffer(out RDUniform uniform, uint size, int bindIndex, byte[] data = null)
    {
        var newBuffer = renderDevice.StorageBufferCreate(size, data);
        var newUniform = new RDUniform
        {
            UniformType = RenderingDevice.UniformType.StorageBuffer,
            Binding = bindIndex
        };
        newUniform.AddId(newBuffer);
        uniform = newUniform;
        return newBuffer;
    }

    byte[] GetTerrainParametersBytes()
    {
        float[] terrainData =
        {
            time,
            noiseScale,
            isoLevel, // ONE AND A HALF DAYS WASTED BECAUSE I FORGOR TO COPY THIS IN
            // IT DIDNT EVEN SHOW ANY ERROR AAAAAAAAAAAAAAAAAAAAAAAAAAAA
            // I guess at least I learned more about compoop shaders :(

            NUM_VOXELS_PER_AXIS,
            chunkScale,
            playerPos.X,
            playerPos.Y,
            playerPos.Z,
            noiseOffset.X,
            noiseOffset.Y,
            noiseOffset.Z
        };
        byte[] terrainDataBytes = new byte[terrainData.Length * sizeof(float)];
        Buffer.BlockCopy(terrainData, 0, terrainDataBytes, 0, terrainDataBytes.Length);
        return terrainDataBytes;
    }

    static byte[] GetResetCounterBytes()
    {
        var counter = new uint[] { 0 };
        var counterBytes = new byte[counter.Length * sizeof(uint)];
        Buffer.BlockCopy(counter, 0, counterBytes, 0, counterBytes.Length);
        return counterBytes;
    }

    void RunNewCompute()
    {
        // First let's update our params buffer with the latest values
        var newParamsBytes = GetTerrainParametersBytes();
        renderDevice.BufferUpdate(paramsBuffer, 0, (uint)newParamsBytes.Length, newParamsBytes);

        // And then reset the counter
        var newResetCounter = GetResetCounterBytes();
        renderDevice.BufferUpdate(counterBuffer, 0, (uint)newResetCounter.Length, newResetCounter);

        // time for more goofy boilerplate ig
        var computeList = renderDevice.ComputeListBegin();
        renderDevice.ComputeListBindComputePipeline(computeList, pipeline);
        renderDevice.ComputeListBindUniformSet(computeList, bufferSet, BUFFER_SET_INDEX);
        renderDevice.ComputeListDispatch(
            computeList,
            TERRAIN_RESOLUTION,
            TERRAIN_RESOLUTION,
            TERRAIN_RESOLUTION
        );
        renderDevice.ComputeListEnd();

        // Submit the stuff (whatever that means)
        renderDevice.Submit();
        lastComputeDispatch = frameCount;
        waitingForCompute = true;
    }

    void FetchAndProcessCompute()
    {
        renderDevice.Sync();
        waitingForCompute = false;

        triangleDataBytes = renderDevice.BufferGetData(triangleBuffer);
        counterDataBytes = renderDevice.BufferGetData(counterBuffer);

        // Start up our mesh processing task
        meshProcess = Task.Run(ProcessMeshData);
        waitingForMeshthread = true;
        lastMeshthreadStart = frameCount;
    }

    void ProcessMeshData()
    {
        var triangles = new float[triangleDataBytes.Length / sizeof(float)];
        Buffer.BlockCopy(triangleDataBytes, 0, triangles, 0, triangleDataBytes.Length);

        var counterFloatArr = new uint[counterDataBytes.Length / sizeof(uint)];
        Buffer.BlockCopy(counterDataBytes, 0, counterFloatArr, 0, counterDataBytes.Length);

        numTriangles = (int)counterFloatArr[0];
        var numVerts = numTriangles * 3;

        System.Array.Resize(ref verts, numVerts);
        System.Array.Resize(ref normals, numVerts);

        for (int triIndex = 0; triIndex < numTriangles; triIndex++)
        {
            var i = triIndex * 16;
            var posA = new Vector3(triangles[i + 0], triangles[i + 1], triangles[i + 2]);
            var posB = new Vector3(triangles[i + 4], triangles[i + 5], triangles[i + 6]);
            var posC = new Vector3(triangles[i + 8], triangles[i + 9], triangles[i + 10]);
            var norm = new Vector3(triangles[i + 12], triangles[i + 13], triangles[i + 14]);
            verts[(triIndex * 3) + 0] = posA;
            verts[(triIndex * 3) + 1] = posB;
            verts[(triIndex * 3) + 2] = posC;
            normals[(triIndex * 3) + 0] = norm;
            normals[(triIndex * 3) + 1] = norm;
            normals[(triIndex * 3) + 2] = norm;
        }
    }

    void CreateMesh()
    {
        meshProcess.Wait();
        waitingForMeshthread = false;
        GD.Print("Num tris: ", numTriangles);
        if (verts.Length > 0)
        {
            var meshData = new Godot.Collections.Array();
            meshData.Resize((int)Mesh.ArrayType.Max);
            meshData[(int)Mesh.ArrayType.Vertex] = verts;
            meshData[(int)Mesh.ArrayType.Normal] = normals;
            terrainMesh.ClearSurfaces();
            terrainMesh.AddSurfaceFromArrays(Mesh.PrimitiveType.Triangles, meshData);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            ReleaseData();
        }
    }

    void ReleaseData()
    {
        renderDevice.FreeRid(pipeline);
        renderDevice.FreeRid(triangleBuffer);
        renderDevice.FreeRid(paramsBuffer);
        renderDevice.FreeRid(counterBuffer);
        renderDevice.FreeRid(lutBuffer);
        renderDevice.FreeRid(shader);

        pipeline = new Rid();
        triangleBuffer = new Rid();
        paramsBuffer = new Rid();
        counterBuffer = new Rid();
        lutBuffer = new Rid();
        shader = new Rid();

        renderDevice.Free();
        renderDevice = null;
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    // public override void _Process(double delta)
    // {
    //     if (waitingForCompute && (frameCount - lastComputeDispatch >= NUM_WAITFRAMES_GPUSYNC))
    //         FetchAndProcessCompute();
    //     else if (
    //         waitingForMeshthread && (frameCount - lastMeshthreadStart >= NUM_WAITFRAMES_MESHTHREAD)
    //     )
    //         CreateMesh();
    //     else if (!waitingForCompute && !waitingForMeshthread)
    //         RunNewCompute();

    //     frameCount += 1;
    //     time += (float)(delta);
    // }
}
