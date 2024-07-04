namespace Game.Terrain;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Game.Terrain.Noise;
using Godot;
using static Game.Terrain.ChunkData;

// Class to handle generating, loading/unloading chunks
public partial class ChunkManager : Node3D
{
    [Export]
    PackedScene chunkTemplate;

    [Export]
    SimpleRigidPlayer player;

    [Export]
    Node3D terraformMarker;

    // Manage the current chunks
    readonly ConcurrentDictionary<ChunkID, Chunk> loadedChunks = [];

    readonly ConcurrentQueue<ChunkID> chunksToReload = new(); // If it doesn't exist, load it, otherwise reload
    readonly ConcurrentDictionary<ChunkID, byte> queuedChunkSet = [];

    readonly ConcurrentQueue<Chunk> freeChunks = new(); // For object pooling

    bool currentlyProcessing = false;

    // Minimum time before triggering a queue, so it doesnt happen too often at chunk borders
    const int MIN_REFRESH_INTERVAL = 100; //ms
    ulong lastRefreshMs = Time.GetTicksMsec();

    ChunkID currentPlayerChunk = new();

    // Chunk generation
    // We'll have to clear this after some limit and on area changes
    readonly ConcurrentDictionary<ChunkID, float[]> knownChunkData = [];

    [Export]
    PackedScene testMarkerScene;

    // This is the var tracking whether we already are processing a chunk
    bool currentlyComputing;

    TerrainParameters terrainParams = new(1.0f, new Vector3(0, 0, 0));
    readonly FastNoiseLiteSharp noiseGenerator = new();

    public void QueueChunk(ChunkID chunkID)
    {
        if (!queuedChunkSet.TryAdd(chunkID, 0))
        {
            return;
        }
        chunksToReload.Enqueue(chunkID);
    }

    // For when we receive some chunk mods from the server
    public void UpdateChunkData(ChunkID chunkID, float[] chunkData)
    {
        knownChunkData[chunkID] = chunkData;
        if (loadedChunks.ContainsKey(chunkID))
        {
            ReloadChunk(chunkID).Wait();
            player.ForceUpdateTransform();
        }
    }

    Task ReloadChunk(ChunkID chunkID)
    {
        currentlyComputing = true;
        // If this chunk is already loaded, reload it
        // Otherwise grab a new one and move it where it needs to go
        if (!loadedChunks.TryGetValue(chunkID, out Chunk chunkToLoad))
        {
            chunkToLoad = GetNewChunk();
        }
        // var s = Stopwatch.StartNew();

        return Task.Run(() =>
        {
            loadedChunks[chunkID] = ComputeChunk(chunkID, chunkToLoad);

            currentlyComputing = false;

            // s.Stop();
            // GD.Print("chunk time ", s.ElapsedMilliseconds);
        });
    }

    float EvaluateNoise(Vector3I coordinate, Vector3I chunkCoord)
    {
        // -0.5 to 0.5  position relative to chunk
        Vector3 percentPos =
            (((Vector3)coordinate) / TerrainData.VoxelAxisLengths) - new Vector3(0.5f, 0.5f, 0.5f);
        //TerrainData.CoordToChunkSpace(coordinate);

        // Position of this coord in sample space
        Vector3 samplePos =
            ((percentPos + chunkCoord) * terrainParams.NoiseScale) + terrainParams.NoiseOffset;

        float sum = 0;
        float amplitude = 1;
        float weight = 1;

        Vector3 fbmSample = samplePos;
        noiseGenerator.SetFrequency(2.0f);
        noiseGenerator.SetNoiseType(FastNoiseLiteSharp.NoiseType.Perlin);

        for (int i = 0; i < 3; i++) // 6
        {
            float noise = noiseGenerator.GetNoise(fbmSample.X, fbmSample.Y, fbmSample.Z);
            noise *= weight;
            weight = Math.Max(0, Math.Min(1, noise * 10));
            weight *= 0.5f;
            sum += noise * amplitude;
            fbmSample *= 2;
            amplitude *= 0.5f;
        }

        float density = Math.Clamp(sum, 0.0f, 1.0f);
        density += Math.Clamp(SDFUtils.SdSphere(samplePos, 3f), -1.0f, 0.1f);

        // GD.Print(density);
        return Math.Clamp(density, -1.0f, 1.0f);
    }

    float[] PopulateNewSampleData(ChunkID cID)
    {
        var newData = new float[TerrainData.SAMPLE_ARRAY_SIZE];
        var chunkCoord = cID.GetSampleVector3I();

        Parallel.For(
            0,
            newData.Length,
            (int index) =>
            {
                var currentCoord = TerrainData.IndexToCoord3D(
                    index,
                    TerrainData.SAMPLE_ARRAY_PER_AXIS
                );
                currentCoord -= Vector3I.One;

                newData[index] = EvaluateNoise(currentCoord, chunkCoord);
            }
        );

        return newData;
    }

    // This method should run in a Task
    Chunk ComputeChunk(ChunkID cID, Chunk c)
    {
        var chunkData = knownChunkData.GetOrAdd(cID, PopulateNewSampleData);

        // Chuck the data over to the chunk and let it finish processing
        c.ProcessChunk(cID, chunkData);

        // It's fine if this returns false, that just means the chunkID
        // was (re)loaded manually instead of being in the queue

        return c;
    }

    // Grab an available new chunk
    Chunk GetNewChunk()
    {
        if (!freeChunks.TryDequeue(out var newChunk))
        {
            // Otherwise instantiate a new one
            newChunk = InstantiateChunk();
        }

        return newChunk;
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
            removedChunk.HibernateChunk();
            freeChunks.Enqueue(removedChunk);
        }
        // Otherwise can't unload it if it's not loaded :V
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        Manager.Instance.MainWorld.chunkManager = this;
        GD.Print("Size ", Marshal.SizeOf<TerrainParameters>());

        // InitializeCompute();

        // make an initial pool of chunk nodes
        // const int PoolChunks = 40;
        // for (int x = 0; x < PoolChunks; x++)
        // {
        //     var c = InstantiateChunk();
        //     freeChunks.Enqueue(c);
        // }
        // GD.Print("pool ", freeChunks.Count);

        // ReloadChunk(new(0, 0, 0)).Wait();

        // ReloadChunk(new(0, 1, 0));
        // ReloadChunk(new(0, -1, 0));
        // QueueChunk(new(0, 0, 0));
        // // QueueChunk(new(0, 2, 1));
        RefreshPlayerChunks();
        // GD.Print("Max Mod Bytes ", TerrainParams.MAX_MOD_BYTES);
    }

    void RefreshPlayerChunks()
    {
        // make the loop bigger by one to remove the outermost chunk IDs
        const int GenerateDistance = TerrainData.CHUNK_VIEW_DIST;

        for (int x = -GenerateDistance; x <= GenerateDistance; x++)
        {
            for (int y = -GenerateDistance; y <= GenerateDistance; y++)
            {
                for (int z = -GenerateDistance; z <= GenerateDistance; z++)
                {
                    ChunkID toQueue =
                        new(
                            x + currentPlayerChunk.posX,
                            y + currentPlayerChunk.posY,
                            z + currentPlayerChunk.posZ
                        );

                    bool chunkIsLoaded = loadedChunks.ContainsKey(toQueue);
                    bool chunkIsQueued = queuedChunkSet.ContainsKey(toQueue);

                    if (!chunkIsLoaded && !chunkIsQueued)
                    {
                        // queue only if the chunk is not there and it's not already queued
                        QueueChunk(toQueue);
                        // GD.Print("Queueing ", toQueue);
                    }
                }
            }
        }

        // Unload far chunks
        var playerPos = currentPlayerChunk.GetSampleVector3I();

        foreach (ChunkID cID in loadedChunks.Keys)
        {
            var disp = playerPos - cID.GetSampleVector3I();
            for (int x = 0; x < 3; x++)
            {
                if (Math.Abs(disp[x]) > TerrainData.CHUNK_VIEW_DIST + 1)
                {
                    TryUnloadChunk(cID);
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        if (!loadedChunks.ContainsKey(currentPlayerChunk))
        {
            ReloadChunk(currentPlayerChunk).Wait();
        }

        var newPlayerChunk = ChunkID.GetNearestID(player.Position);

        if (currentPlayerChunk != newPlayerChunk)
        {
            // Update our current chunk
            currentPlayerChunk = newPlayerChunk;

            if ((Time.GetTicksMsec() - lastRefreshMs) >= MIN_REFRESH_INTERVAL)
            {
                RefreshPlayerChunks();

                // the timer is so we don't trigger a ton of reloads
                // when near the edge of two chunks. Shouldn't cause a problem
                // because we already ensure the current player chunk is loaded.
                lastRefreshMs = Time.GetTicksMsec();
            }
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // Reload a chunk if it's in the queue
        if (!currentlyComputing && chunksToReload.TryDequeue(out var chunkID))
        {
            queuedChunkSet.Remove(chunkID, out _);
            _ = ReloadChunk(chunkID);
        }
    }

    public void TerraformPoint(Vector3 worldPoint, float strength)
    {
        terraformMarker.Position = worldPoint;

        var closestChunk = ChunkID.GetNearestID(worldPoint);
        var closestChunkPos = closestChunk.GetSampleVector3I();

        // Might as well hard code it since it's easier that way
        Span<Vector3I> offsetsToUpdate =
        [
            new Vector3I(-1, -1, -1),
            new Vector3I(-1, -1, 0),
            new Vector3I(-1, -1, 1),
            new Vector3I(-1, 0, -1),
            new Vector3I(-1, 0, 0),
            new Vector3I(-1, 0, 1),
            new Vector3I(-1, 1, -1),
            new Vector3I(-1, 1, 0),
            new Vector3I(-1, 1, 1),
            new Vector3I(0, -1, -1),
            new Vector3I(0, -1, 0),
            new Vector3I(0, -1, 1),
            new Vector3I(0, 0, -1),
            new Vector3I(0, 0, 0),
            new Vector3I(0, 0, 1),
            new Vector3I(0, 1, -1),
            new Vector3I(0, 1, 0),
            new Vector3I(0, 1, 1),
            new Vector3I(1, -1, -1),
            new Vector3I(1, -1, 0),
            new Vector3I(1, -1, 1),
            new Vector3I(1, 0, -1),
            new Vector3I(1, 0, 0),
            new Vector3I(1, 0, 1),
            new Vector3I(1, 1, -1),
            new Vector3I(1, 1, 0),
            new Vector3I(1, 1, 1)
        ];

        foreach (var chunkOffset in offsetsToUpdate)
        {
            bool modified = false;
            var currentChunkID = new ChunkID(closestChunkPos + chunkOffset);

            // Either get the existing data array or make a new one
            var chunkData = knownChunkData.GetOrAdd(currentChunkID, PopulateNewSampleData);

            var chunkWorldPos = currentChunkID.GetSampleVector() * TerrainData.CHUNK_SIZE;
            var localTerraformPos = (worldPoint - chunkWorldPos) / TerrainData.CHUNK_SIZE;

            // Now the terraform pos is from 0 to 1
            localTerraformPos += new Vector3(0.5f, 0.5f, 0.5f);

            // Scale it to the number of sample points per chunk
            localTerraformPos *= TerrainData.VOXELS_PER_AXIS;

            localTerraformPos = localTerraformPos.Round();

            foreach (var terraformOffset in offsetsToUpdate)
            {
                var curTerraformPos = localTerraformPos + terraformOffset;
                // If this point isn't in range, we should ignore it
                bool validPosition = true;

                // Check the X, Y, Z for out of range
                for (int element = 0; element < 3; element++)
                {
                    if (
                        (curTerraformPos[element] < -1)
                        || (curTerraformPos[element] > TerrainData.VOXELS_PER_AXIS + 1)
                    )
                    {
                        validPosition = false;
                    }
                }

                if (!validPosition)
                {
                    continue;
                }

                // GD.Print(localTerraformPos, " local ", currentChunkID);

                var correctedCoord = curTerraformPos + Vector3.One;

                int modIndex = TerrainData.Coord3DToIndex(
                    (Vector3I)correctedCoord.Round(),
                    TerrainData.SAMPLE_ARRAY_PER_AXIS
                );

                if (modIndex >= 0 && modIndex < chunkData.Length)
                {
                    if (terraformOffset.LengthSquared() != 0)
                    {
                        chunkData[modIndex] += (-strength) * 0.1f;
                    }
                    else
                    {
                        chunkData[modIndex] += -strength;
                    }

                    chunkData[modIndex] = Math.Clamp(chunkData[modIndex], -1.0f, 1.0f);
                    modified = true;
                }
            }
            if (modified)
            {
                UpdateChunkData(currentChunkID, chunkData);
            }
        }
    }
}
