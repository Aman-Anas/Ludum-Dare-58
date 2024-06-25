namespace Game.Terrain;

using System;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Godot;
using static Game.Terrain.ChunkData;

// Class to handle generating, loading/unloading chunks
public partial class ChunkManager : Node3D
{
    [Export]
    PackedScene chunkTemplate;

    [Export]
    Node3D player;

    // Manage the current chunks
    readonly Dictionary<ChunkID, Chunk> loadedChunks = [];
    readonly Queue<ChunkID> chunksToReload = new(); // If it doesn't exist, load it, otherwise reload
    readonly HashSet<ChunkID> queuedChunkIDs = [];

    readonly Queue<Chunk> freeChunks = new(); // For object pooling

    ChunkID currentPlayerChunk = new();

    // How often to check for all chunks loaded
    const int CHECK_CHUNK_INTERVAL = 1500; //ms
    bool checkForChunks = true;

    // Chunk generation
    // We'll have to clear this after some limit and on area changes
    readonly Dictionary<ChunkID, byte[]> knownChunkMods = [];

    // This is the task tracking whether we already are processing a chunk
    bool currentlyComputing;

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
        const uint MAX_BYTES_TRIS = TerrainParams.BYTES_PER_TRI * TerrainParams.MAX_TRIANGLES;
        GD.Print("MAX_BYTES ", MAX_BYTES_TRIS);
        // eeesh this is a lot of bytes

        // Make our storage buffers (this one for triangles)
        triangleBuffer = InitStorageBuffer(
            out var trianglesUniform,
            MAX_BYTES_TRIS,
            TRIANGLE_BIND_INDEX
        );

        // Buffer for our adjustable parameters
        UpdateChunkParams(new ChunkID(0, 0, 0), false);

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
            (uint)TerrainParams.MAX_MOD_BYTES,
            MODDATA_BIND_INDEX
        );

        bufferSet = renderDevice.UniformSetCreate(
            [trianglesUniform, paramsUniform, counterUniform, lutUniform, modUniform],
            shader,
            BUFFER_SET_INDEX
        );

        pipeline = renderDevice.ComputePipelineCreate(shader);
    }

    void UpdateChunkParams(ChunkID cID, bool useMods)
    {
        ref var chunkParams = ref MemoryMarshal.AsRef<ChunkParameters>(chunkParamBytes);
        chunkParams.noiseScale = 1.0f;
        chunkParams.isoLevel = 0.00f;
        chunkParams.numVoxelsPerAxis = TerrainParams.NUM_VOXELS_PER_AXIS;
        chunkParams.chunkScale = TerrainParams.CHUNK_SIZE;
        chunkParams.chunkX = cID.posX;
        chunkParams.chunkY = cID.posY;
        chunkParams.chunkZ = cID.posZ;
        chunkParams.noiseOffsetX = 0;
        chunkParams.noiseOffsetY = 0;
        chunkParams.noiseOffsetZ = 0;

        if (useMods)
        {
            chunkParams.useMods = 1;
        }
        else
        {
            chunkParams.useMods = 0;
        }
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
        queuedChunkIDs.Add(chunkID);
    }

    // For when we receive some chunk mods from the server
    public void ApplyChunkMods(ChunkID chunkID, byte[] chunkMods)
    {
        GD.Print(chunkID);
        knownChunkMods[chunkID] = chunkMods;
        if (loadedChunks.ContainsKey(chunkID))
        {
            QueueChunk(chunkID);
        }
    }

    // Simple helper to start reloading the next chunk
    void ReloadNextChunk()
    {
        var _ = ReloadChunk(chunksToReload.Dequeue());
    }

    Task ReloadChunk(ChunkID chunkID)
    {
        currentlyComputing = true;
        // If this chunk is already loaded, reload it
        // Otherwise grab a new one and move it where it needs to go
        if (!loadedChunks.TryGetValue(chunkID, out Chunk chunkToLoad))
        {
            chunkToLoad = GetNewChunk();
            chunkToLoad.Position =
                new Vector3(chunkID.posX, chunkID.posY, chunkID.posZ) * TerrainParams.CHUNK_SIZE;
            loadedChunks[chunkID] = chunkToLoad;
        }

        return Task.Run(() => ComputeChunk(chunkID, chunkToLoad));
    }

    // This method should run in a Task
    void ComputeChunk(ChunkID cID, Chunk c)
    {
        c.CurrentChunkID = cID;
        var useMods = knownChunkMods.ContainsKey(cID);
        // First let's update our buffers
        UpdateChunkParams(cID, useMods);
        ResetTriangleCounter();

        renderDevice.BufferUpdate(paramsBuffer, 0, (uint)chunkParamBytes.Length, chunkParamBytes);
        renderDevice.BufferUpdate(
            counterBuffer,
            0,
            (uint)counterDataBytes.Length,
            counterDataBytes
        );

        if (useMods)
        {
            var modData = knownChunkMods[cID];
            renderDevice.BufferUpdate(modBuffer, 0, (uint)modData.Length, modData);
        }
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

        // It's fine if it returns false, that just means the chunkID
        // was (re)loaded manually instead of being in the queue
        queuedChunkIDs.Remove(cID);

        currentlyComputing = false;
    }

    // Grab an available new chunk
    Chunk GetNewChunk()
    {
        if (freeChunks.Count > 0)
        {
            return freeChunks.Dequeue();
        }
        // Otherwise instantiate a new one
        return InstantiateChunk();
    }

    // Manually instantiate a new chunk
    Chunk InstantiateChunk()
    {
        var newChunk = chunkTemplate.Instantiate<Chunk>();
        AddChild(newChunk);
        return newChunk;
    }

    void TryUnloadChunk(ChunkID cID)
    {
        if (loadedChunks.Remove(cID, out var removedChunk))
        {
            freeChunks.Enqueue(removedChunk);
        }
        // Otherwise can't unload it if it's not loaded :V
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        InitializeCompute();

        // make an initial pool of chunk nodes
        for (int x = 0; x < 900; x++)
        {
            var c = InstantiateChunk();
            freeChunks.Enqueue(c);
        }

        checkForChunks = true; // Use a cancellation token maybe in the future
        var _ = Task.Run(EnsurePlayerChunksLoaded);
    }

    async Task EnsurePlayerChunksLoaded()
    {
        // make the loop bigger by one to remove the outermost chunk IDs
        const int numChunksCheck = TerrainParams.CHUNK_VIEW_DIST + 1;

        while (checkForChunks)
        {
            for (int x = -numChunksCheck; x <= numChunksCheck; x++)
            {
                for (int y = -numChunksCheck; y <= numChunksCheck; y++)
                {
                    for (int z = -numChunksCheck; z <= numChunksCheck; z++)
                    {
                        ChunkID toQueue =
                            new(
                                x + currentPlayerChunk.posX,
                                y + currentPlayerChunk.posY,
                                z + currentPlayerChunk.posZ
                            );

                        // If this chunk is on the border of our view distance
                        // Then 'remove' it (to be reused)
                        if (
                            Math.Abs(x) == numChunksCheck
                            || Math.Abs(y) == numChunksCheck
                            || Math.Abs(z) == numChunksCheck
                        )
                        {
                            TryUnloadChunk(toQueue);

                            continue; // Continue to next chunk
                        }

                        bool chunkIsLoaded = loadedChunks.ContainsKey(toQueue);
                        bool chunkIsQueued = queuedChunkIDs.Contains(toQueue);

                        if ((!chunkIsLoaded) && (!chunkIsQueued))
                        {
                            // Queue only if the chunk is not there and it's not already queued
                            QueueChunk(toQueue);
                            // GD.Print(
                            //     "Queueing ",
                            //     toQueue.posX,
                            //     " ",
                            //     toQueue.posY,
                            //     " ",
                            //     toQueue.posZ
                            // );
                        }
                    }
                }
            }

            // No need to constantly check for chunks all the time, just once in a while
            await Task.Delay(CHECK_CHUNK_INTERVAL);
            // GD.Print("[");
            // foreach (var id in queuedChunkIDs)
            // {
            //     GD.Print(id);
            // }
            // GD.Print("]");
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        currentPlayerChunk = ChunkID.GetNearestID(player.Position);
    }

    byte[] funnyMods = new byte[TerrainParams.MAX_MOD_BYTES];

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        if (chunksToReload.Count > 0)
        {
            // Make sure there isn't a current chunk computing
            if (!currentlyComputing)
            {
                if (!loadedChunks.ContainsKey(currentPlayerChunk))
                {
                    // wait for this chunk because we at least
                    // need the chunk the player is in to be loaded
                    ReloadChunk(currentPlayerChunk).Wait();
                }
                else
                {
                    ReloadNextChunk();
                }
            }
        }

        if (Input.IsActionJustPressed(GameActions.PLAYER_ROLL_RIGHT))
        {
            var readableMods = MemoryMarshal.Cast<byte, float>(funnyMods);
            for (int idx = 0; idx < readableMods.Length; idx++)
            {
                const int numVoxels = TerrainParams.NUM_VOXELS_PER_AXIS;
                var zQuotient = Math.DivRem(idx, numVoxels, out var zPos);
                var yQuotient = Math.DivRem(zQuotient, numVoxels, out var yPos);
                var xPos = yQuotient % numVoxels;
                var calcPos = new Vector3(xPos, yPos, zPos);
                if (
                    xPos > 0
                    && xPos < numVoxels
                    && yPos > 0
                    && yPos < numVoxels
                    && zPos > 0
                    && zPos < numVoxels
                )
                {
                    readableMods[idx] = 20.1f; // * (xPos / numVoxels);
                }
            }
            ApplyChunkMods(currentPlayerChunk, funnyMods);
        }
    }

    public override void _Notification(int what)
    {
        if (what == NotificationPredelete)
        {
            checkForChunks = false;
            ReleaseData();
        }
    }

    // I don't know if this is necessary, but it does say to free RIDs
    // when you're done with them in the docs.
    void ReleaseData()
    {
        renderDevice.FreeRid(pipeline);
        renderDevice.FreeRid(triangleBuffer);
        renderDevice.FreeRid(paramsBuffer);
        renderDevice.FreeRid(counterBuffer);
        renderDevice.FreeRid(lutBuffer);
        renderDevice.FreeRid(shader);
        renderDevice.Free();
        renderDevice = null;
    }
}
