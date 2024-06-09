using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Game.Terrain;
using Godot;
using static ChunkData;

// Class to handle generating, loading/unloading chunks
public partial class ChunkManager : Node3D
{
    [Export]
    PackedScene chunkTemplate;

    // Manage the current chunks
    readonly Dictionary<ChunkID, Chunk> loadedChunks = new();
    readonly Queue<ChunkID> chunksToReload = new(); // If it doesn't exist, load it, otherwise reload
    readonly Queue<Chunk> freeChunks = new(); // For object pooling

    // Chunk generation
    // We'll have to clear this after some limit and on area changes
    readonly Dictionary<ChunkID, byte[]> knownChunkMods = new();

    // This is the task tracking the currently processing chunk
    Task currentChunkCompute;

    // Index of the bufferset for the shader
    const int BUFFER_SET_INDEX = 0;

    // Bind Indices
    const int TRIANGLE_BIND_INDEX = 0;
    const int PARAMS_BIND_INDEX = 1;
    const int COUNTER_BIND_INDEX = 2;
    const int LUT_BIND_INDEX = 3;
    const int MODDATA_BIND_INDEX = 4;

    // Compute shader path
    const string SHADER_PATH = "res://scripts/terrain/ChunkGenerator.glsl";

    RenderingDevice renderDevice;

    // Resource IDs
    Rid shader;
    Rid pipeline;
    Rid bufferSet;
    Rid triangleBuffer;
    Rid paramsBuffer;
    Rid counterBuffer;
    Rid lutBuffer;
    Rid modBuffer;

    // Buffer data
    byte[] counterDataBytes = new byte[sizeof(uint)];
    byte[] chunkParamBytes = new byte[Marshal.SizeOf(typeof(ChunkParameters))];

    void InitializeCompute()
    {
        renderDevice = RenderingServer.CreateLocalRenderingDevice();
        var shaderFile = GD.Load<RDShaderFile>(SHADER_PATH);
        var shaderBytecode = shaderFile.GetSpirV();
        var shader = renderDevice.ShaderCreateFromSpirV(shaderBytecode);

        // Make triangle buffer
        uint MAX_BYTES_TRIS = (uint)(TerrainParams.BYTES_PER_TRI * TerrainParams.MAX_TRIANGLES);
        GD.Print("MAX_BYTES ", MAX_BYTES_TRIS);
        // eeesh this is a lot of bytes

        // Make our storage buffers (this one for triangles)
        this.triangleBuffer = InitStorageBuffer(
            out var trianglesUniform,
            MAX_BYTES_TRIS,
            TRIANGLE_BIND_INDEX
        );

        // Buffer for our adjustable parameters
        UpdateChunkParams(
            new ChunkID
            {
                posX = 0,
                posY = 0,
                posZ = 0
            }
        );

        paramsBuffer = InitStorageBuffer(
            out var paramsUniform,
            (uint)chunkParamBytes.Length,
            PARAMS_BIND_INDEX,
            chunkParamBytes
        );

        // So much boilerplate.... :(
        // Make a buffer for a counter (?)
        ResetTriangleCounter();
        counterBuffer = InitStorageBuffer(
            out var counterUniform,
            (uint)counterDataBytes.Length,
            COUNTER_BIND_INDEX,
            counterDataBytes
        );

        // make our marching cubes LUT table buffer (shouldn't need to touch this later)
        var lutBytes = new byte[MarchingCubesLUT.LUT_ARRAY.Length * sizeof(int)];
        Buffer.BlockCopy(MarchingCubesLUT.LUT_ARRAY, 0, lutBytes, 0, lutBytes.Length);

        lutBuffer = InitStorageBuffer(
            out var lutUniform,
            (uint)lutBytes.Length,
            LUT_BIND_INDEX,
            lutBytes
        );

        modBuffer = InitStorageBuffer(
            out var modUniform,
            (uint)TerrainParams.MAX_MOD_POINTS,
            MODDATA_BIND_INDEX
        );

        bufferSet = renderDevice.UniformSetCreate(
            new Godot.Collections.Array<RDUniform>
            {
                trianglesUniform,
                paramsUniform,
                counterUniform,
                lutUniform,
                modUniform
            },
            shader,
            BUFFER_SET_INDEX
        );

        pipeline = renderDevice.ComputePipelineCreate(shader);
    }

    void UpdateChunkParams(ChunkID cID)
    {
        ref var chunkParams = ref MemoryMarshal.AsRef<ChunkParameters>(chunkParamBytes);
        chunkParams.noiseScale = 1.0f;
        chunkParams.isoLevel = 1.00f;
        chunkParams.numVoxelsPerAxis = TerrainParams.NUM_VOXELS_PER_AXIS;
        chunkParams.chunkScale = TerrainParams.CHUNK_SIZE;
        chunkParams.chunkX = cID.posX;
        chunkParams.chunkY = cID.posY;
        chunkParams.chunkZ = cID.posZ;
        chunkParams.noiseOffsetX = 0;
        chunkParams.noiseOffsetY = 0;
        chunkParams.noiseOffsetZ = 0;
        chunkParams.useMods = 0;
    }

    void ResetTriangleCounter()
    {
        var counter = new uint[] { 0 };
        Buffer.BlockCopy(counter, 0, counterDataBytes, 0, counterDataBytes.Length);
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

    // Helper to queue up a chunk
    public void QueueChunk(ChunkID chunkID)
    {
        chunksToReload.Enqueue(chunkID);
    }

    // For when we receive some chunk mods from the server
    public void ApplyChunkMods(ChunkID chunkID, byte[] chunkMods)
    {
        knownChunkMods[chunkID] = chunkMods;
        if (loadedChunks.ContainsKey(chunkID))
        {
            chunksToReload.Enqueue(chunkID);
        }
    }

    // Simple helper to start reloading the next chunk
    void ReloadNextChunk()
    {
        ReloadChunk(chunksToReload.Dequeue());
    }

    void ReloadChunk(ChunkID chunkID)
    {
        // If this chunk is already loaded, reload it
        // Otherwise grab a new one and move it where it needs to go
        Chunk chunkToLoad;
        if (loadedChunks.ContainsKey(chunkID))
        {
            chunkToLoad = loadedChunks[chunkID];
        }
        else
        {
            chunkToLoad = GetNewChunk();
            chunkToLoad.Position =
                new Vector3(chunkID.posX, chunkID.posY, chunkID.posZ) * TerrainParams.CHUNK_SIZE;
        }

        loadedChunks.Add(chunkID, chunkToLoad);
        currentChunkCompute = Task.Run(() => ComputeChunk(chunkID, chunkToLoad));
    }

    // This method should run in a Task
    void ComputeChunk(ChunkID cID, Chunk c)
    {
        // First let's update our buffers
        UpdateChunkParams(cID);
        ResetTriangleCounter();

        renderDevice.BufferUpdate(paramsBuffer, 0, (uint)chunkParamBytes.Length, chunkParamBytes);
        renderDevice.BufferUpdate(
            counterBuffer,
            0,
            (uint)counterDataBytes.Length,
            counterDataBytes
        );

        // Now that they're updated, let's start the compute
        var computeList = renderDevice.ComputeListBegin();
        renderDevice.ComputeListBindComputePipeline(computeList, pipeline);
        renderDevice.ComputeListBindUniformSet(computeList, bufferSet, BUFFER_SET_INDEX);
        renderDevice.ComputeListDispatch(
            computeList,
            TerrainParams.TERRAIN_RESOLUTION,
            TerrainParams.TERRAIN_RESOLUTION,
            TerrainParams.TERRAIN_RESOLUTION
        );
        renderDevice.ComputeListEnd();

        // Submit it and wait (should be fine since we're in an async task/thread thing)
        renderDevice.Submit();
        renderDevice.Sync();

        // Get our data back
        counterDataBytes = renderDevice.BufferGetData(counterBuffer);
        uint count = MemoryMarshal.Cast<byte, uint>(counterDataBytes)[0];

        // Only get the number of triangles that actually exist
        var triangleDataBytes = renderDevice.BufferGetData(
            triangleBuffer,
            0,
            TerrainParams.BYTES_PER_TRI * count
        );

        Span<Triangle> triangles = MemoryMarshal.Cast<byte, Triangle>(triangleDataBytes);

        // Chuck the data over to the new chunk and let it finish processing
        c.ProcessChunk(triangles, count);
    }

    Chunk GetNewChunk()
    {
        if (freeChunks.Count > 0)
        {
            return freeChunks.Dequeue();
        }
        // Otherwise instantiate a new one
        var newChunk = chunkTemplate.Instantiate<Chunk>();
        AddChild(newChunk);
        return newChunk;
    }

    void UnloadChunk(ChunkID cID)
    {
        if (loadedChunks.ContainsKey(cID))
        {
            loadedChunks.Remove(cID, out var removedChunk);
            freeChunks.Enqueue(removedChunk);
        }
        // Otherwise can't unload it if it's not loaded :V
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        InitializeCompute();

        QueueChunk(
            new ChunkID
            {
                posX = 0,
                posY = 0,
                posZ = 0
            }
        );
        QueueChunk(
            new ChunkID
            {
                posX = 1,
                posY = 0,
                posZ = 0
            }
        );
        for (int x = -10; x <= 10; x++)
        {
            for (int z = -10; z <= 10; z++)
            {
                for (int y = 0; y <= 2; y++)
                {
                    QueueChunk(
                        new ChunkID
                        {
                            posX = x,
                            posY = y,
                            posZ = z
                        }
                    );
                }
            }
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // We want to process *at most* one chunk per frame
        // to avoid lag. This can be adjusted maybe
        if (chunksToReload.Count > 0)
        {
            // Make sure there isn't a current chunk computing
            // Null is neither true or false in C#
            // So this'll trigger if the field is null or completed
            if (currentChunkCompute?.IsCompleted != false)
            {
                // Thread.Sleep(500); // uncomment for SUPER lag :D (maybe useful for debug)
                ReloadNextChunk();
            }
        }

        // TODO: Add some code here to flag farther chunks
        // as free to unload
    }
}
