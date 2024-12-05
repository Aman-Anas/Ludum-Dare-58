namespace Game.Terrain;

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
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
    readonly Dictionary<ChunkID, byte[]> knownChunkData = [];

    // This is the var tracking whether we already are processing a chunk
    bool currentlyComputing;

    TerrainParameters terrainParams = new(true, 1.0f, new Vector3(0, 0, 0));

    // readonly FastNoiseLiteSharp noiseGenerator = new();

    public void QueueChunk(ChunkID chunkID)
    {
        if (!queuedChunkSet.TryAdd(chunkID, 0))
        {
            return;
        }
        chunksToReload.Enqueue(chunkID);
    }

    // For when we receive some chunk mods from the server
    public void UpdateChunkData(ChunkID chunkID, byte[] chunkData)
    {
        lock (knownChunkData)
        {
            knownChunkData[chunkID] = chunkData;
        }
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

        // normally threaded below
        return Task.Run(() =>
        {
            // var s = Stopwatch.StartNew();
            // s.Restart();
            if (!knownChunkData.TryGetValue(chunkID, out var chunkData))
            {
                lock (knownChunkData)
                {
                    chunkData = PopulateNewSampleData(chunkID);
                    knownChunkData[chunkID] = chunkData;
                }
            }
            // s.Stop();
            // GD.Print("sample time: ", s.Elapsed.TotalMilliseconds);
            // s.Restart();

            // Chuck the data over to the chunk and let it finish processing
            chunkToLoad.ProcessChunk(chunkID, chunkData);

            loadedChunks[chunkID] = chunkToLoad;

            currentlyComputing = false;

            // s.Stop();
            // GD.Print("chunk time ", s.Elapsed.TotalMilliseconds);
        });
    }

    byte EvaluateNoise(Vector3I coordinate, Vector3I chunkCoord)
    {
        // -0.5 to 0.5  position relative to chunk
        Vector3 percentPos =
            (((Vector3)coordinate) / TerrainData.VoxelAxisLengths) - new Vector3(0.5f, 0.5f, 0.5f);
        //TerrainData.CoordToChunkSpace(coordinate);

        // Position of this coord in sample space
        Vector3 samplePos =
            ((percentPos + chunkCoord) * terrainParams.NoiseScale) + terrainParams.NoiseOffset;

        // if (samplePos.LengthSquared() > 5.0)
        // {
        //     return byte.MaxValue;
        // }

        // float sum = 0;
        // float amplitude = 1;
        // float weight = 1;

        // Vector3 fbmSample = samplePos;
        // noiseGenerator.SetFrequency(1.0f);
        // noiseGenerator.SetNoiseType(FastNoiseLiteSharp.NoiseType.OpenSimplex2S);
        // noiseGenerator.SetFractalType(FastNoiseLiteSharp.FractalType.FBm);
        // noiseGenerator.SetFractalOctaves(1);

        const int OCTAVES = 3;
        const float PERSISTENCE = 0.5f;
        const float LACUNARITY = 2.0f;
        float amplitude = 1.0f;
        Vector3 frequency = new(1.0f, 1.0f, 1.0f);
        // NoisePeriod period = new(2, 2, 2);

        float noiseValue2D = 0;

        Vector3 startPos = samplePos.Normalized() * 2.0f;

        for (int i = 0; i < OCTAVES; i++)
        {
            var noisePoint = frequency * startPos; //new Vector2(samplePos.X, samplePos.Z);
            noiseValue2D +=
                amplitude * IcariaNoise.GradientNoise3D(noisePoint.X, noisePoint.Y, noisePoint.Z);
            amplitude *= PERSISTENCE;
            frequency *= LACUNARITY;
        }

        // var noise = noiseGenerator.GetNoise(samplePos.X, samplePos.Y, samplePos.Z);

        // float density = Math.Clamp(noise, 0.0f, 1.0f);
        // density += Math.Clamp(SDFUtils.SdSphere(samplePos, 2f), -1.0f, 0.1f);
        // float density = (samplePos.Y * 5f) - noiseValue2D; //noiseGenerator.GetNoise(samplePos.X, samplePos.Z); //samplePos.Y;

        // value 0 to 1
        // float elevation = (noiseValue2D + 1.0f) * 0.5f;
        // float density = noiseValue2D;
        float density = SDFUtils.SdSphere(samplePos, (noiseValue2D * 0.5f) + 2.0f); // * ();
        density = Math.Clamp(density, -1.0f, 1.0f);

        // convert to 0 to 1 for data storage
        density = (density + 1.0f) / 2.0f;

        byte output = (byte)Mathf.RoundToInt(density * byte.MaxValue);
        // if (output > TerrainData.CENTER_ISOLEVEL)
        // {
        //     return TerrainData.CENTER_ISOLEVEL + 1;
        // }
        // GD.Print(output);
        return output;
    }

    byte[] PopulateNewSampleData(ChunkID cID)
    {
        var newData = new byte[TerrainData.SAMPLE_ARRAY_SIZE];
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
        Manager.Instance.ChunkManager = this;
        // GD.Print("Size ", Marshal.SizeOf<TerrainParameters>());

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
                            x + currentPlayerChunk.X,
                            y + currentPlayerChunk.Y,
                            z + currentPlayerChunk.Z
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
        if (!currentlyComputing)
        {
            if (chunksToReload.TryDequeue(out var chunkID))
            {
                if (!chunksToReload.Contains(chunkID))
                {
                    queuedChunkSet.Remove(chunkID, out _);
                }
                _ = ReloadChunk(chunkID);
            }
        }
    }

    public void TerraformPoint(Vector3 worldPoint, float strength, bool add)
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
            if (!knownChunkData.ContainsKey(currentChunkID))
            {
                continue;
            }
            var chunkData = knownChunkData[currentChunkID];

            var chunkWorldPos = currentChunkID.GetSampleVector() * TerrainData.CHUNK_SIZE;

            var localTerraformPos = (worldPoint - chunkWorldPos) / TerrainData.CHUNK_SIZE;

            // Now the terraform pos is from 0 to 1
            localTerraformPos += new Vector3(0.5f, 0.5f, 0.5f);

            // Scale it to the number of sample points per chunk
            localTerraformPos *= TerrainData.VOXELS_PER_AXIS;

            localTerraformPos = localTerraformPos.Round();
            // if (chunkOffset == Vector3I.Zero)
            // {
            //     GD.Print(currentChunkID, " ", localTerraformPos);
            // }
            foreach (var terraformOffset in offsetsToUpdate)
            {
                var curTerraformPos = localTerraformPos + terraformOffset;
                // If this point isn't in range, we should ignore it
                bool validPosition = true;

                // Check the X, Y, Z for out of range
                for (int element = 0; element < 3; element++)
                {
                    /**
                        voxels = 16
                        sample points = 17

                        array 0...16
                        include the ends
                        -1 ... 17
                    **/
                    if (
                        (curTerraformPos[element] < -1)
                        || (curTerraformPos[element] > TerrainData.SAMPLE_POINTS_PER_AXIS)
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

                // The -1...17 coord becomes 0...18
                var correctedCoord = curTerraformPos + Vector3.One;

                int modIndex = TerrainData.Coord3DToIndex(
                    (Vector3I)correctedCoord.Round(),
                    TerrainData.SAMPLE_ARRAY_PER_AXIS
                );

                if (modIndex >= 0 && modIndex < chunkData.Length)
                {
                    var thisTerraAmount = strength;
                    if (curTerraformPos != localTerraformPos)
                    {
                        thisTerraAmount *= 0.15f;
                    }

                    // Whether to add or subtract
                    // Adding terrain "adds" negative values to the terrain
                    // because isolevel < 0 is "inside" terrain
                    int sign = add ? -1 : 1;

                    chunkData[modIndex] = Convert.ToByte(
                        Math.Clamp(
                            chunkData[modIndex] + (sign * (thisTerraAmount * byte.MaxValue)),
                            byte.MinValue,
                            byte.MaxValue
                        )
                    );
                    // if (chunkData[modIndex] > TerrainData.CENTER_ISOLEVEL)
                    // {
                    //     chunkData[modIndex] = TerrainData.CENTER_ISOLEVEL + 1;
                    // }
                    // chunkData[modIndex] = Math.Clamp(chunkData[modIndex], -1.0f, 1.0f);
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
