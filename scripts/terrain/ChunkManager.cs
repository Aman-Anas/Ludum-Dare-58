using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Game.Terrain.Noise;
using Godot;
using static Game.Terrain.ChunkData;

namespace Game.Terrain;

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
    readonly ConcurrentDictionary<ChunkID, byte> loadingChunks = [];

    readonly ConcurrentQueue<Chunk> freeChunks = new(); // For object pooling

    bool currentlyProcessing;

    // Minimum time before triggering a queue, so it doesnt happen too often at chunk borders
    const int MinRefreshInterval = 100; //ms

    ulong lastRefreshMs = Time.GetTicksMsec();

    ChunkID currentPlayerChunk;

    // Chunk generation
    // We'll have to clear this after some limit and on area changes
    readonly Dictionary<ChunkID, sbyte[]> knownChunkData = [];

    // This is the var tracking whether we already are processing a chunk
    bool currentlyComputing;

    TerrainParameters terrainParams = new(true, 1.0f, new Vector3(0, 0, 0));

    // readonly FastNoiseLiteSharp noiseGenerator = new();

    // public void QueueChunk(ChunkID chunkID)
    // {
    //     if (!loadingChunks.TryAdd(chunkID, 0))
    //     {
    //         return;
    //     }
    //     chunksToReload.Enqueue(chunkID);
    //     GD.Print(chunksToReload.Count);
    // }

    // For when we receive some chunk mods from the server
    public void UpdateChunkData(ChunkID chunkID, sbyte[] chunkData)
    {
        lock (knownChunkData)
        {
            knownChunkData[chunkID] = chunkData;
        }
        if (currentPlayerChunk == chunkID)
        {
            ReloadChunk(chunkID).Wait();
            player.ForceUpdateTransform();
        }
        else
        {
            ReloadChunk(chunkID).Wait();
            // QueueChunk(chunkID);
            // chunksToReload.
        }
    }

    Task ReloadChunk(ChunkID chunkID)
    {
        loadingChunks.TryAdd(chunkID, 0);

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
            if (!knownChunkData.TryGetValue(chunkID, out var chunkData))
            {
                chunkData = PopulateNewSampleData(chunkID);
                lock (knownChunkData)
                {
                    knownChunkData[chunkID] = chunkData;
                }
                // s.Stop();
                // GD.Print("sample time: ", s.Elapsed.TotalMilliseconds);
                // s.Restart();
            }

            // Chuck the data over to the chunk and let it finish processing
            chunkToLoad.ProcessChunk(chunkID, chunkData);

            loadedChunks[chunkID] = chunkToLoad;

            currentlyComputing = false;

            _ = loadingChunks.Remove(chunkID, out _);

            // s.Stop();
            // GD.Print("chunk time ", s.Elapsed.TotalMilliseconds);
        });
    }

    // static readonly FastNoiseLiteSharp noiseGenerator = new(3);

    static unsafe sbyte EvaluateNoise(Vector3I coordinate, Vector3I chunkCoord)
    {
        // -0.5 to 0.5  position relative to chunk
        // Vector3 percentPos = ((Vector3)coordinate) - TerrainConsts.HalfVoxelAxisLengths;
        // TerrainConsts.CoordToChunkSpace(coordinate);

        // Position of this coord in sample space
        // The -2 allows us to "sample a little extra on the edges" so that we can chunk seamlessly.
        // e.g. coord (0, 0, 32) becomes (0, 0, 32/30)
        // Then later on when we generate our vertex positions we multiply by 32/30 to scale up the
        // whole chunk so it overlaps the next one.
        Vector3 samplePos =
            (((Vector3)coordinate) / (TerrainConsts.VoxelsPerAxis - 2)) + chunkCoord;

        // var noiseValue2D = noiseGenerator.GetNoise(samplePos.X, samplePos.Y, samplePos.Z);
        float noiseValue2D = 0;

        // Vector3 fbmSample = samplePos;

        const int OCTAVES = 3;
        const float PERSISTENCE = 0.5f;
        const float LACUNARITY = 2.0f;
        float amplitude = 1.0f;
        var frequency = 1.0f; //new(1.0f, 1.0f, 1.0f);
        // // NoisePeriod period = new(2, 2, 2);

        // float noiseValue2D = 0;

        // Vector3 startPos = samplePos.Normalized() * 2.0f;
        // var noise = new FastNoise2.FastNoise(FastNoise2.)
        // GD.Print()

        // noiseValue2D = fractal.GenSingle3D(samplePos.X, samplePos.Y, samplePos.Z, 1);

        for (int i = 0; i < OCTAVES; i++)
        {
            var noisePoint = frequency * samplePos; //* new Vector3(samplePos.X, 0, samplePos.Z);
            noiseValue2D +=
                amplitude * IcariaNoise.GradientNoise3D(noisePoint.X, noisePoint.Y, noisePoint.Z);
            amplitude *= PERSISTENCE;
            frequency *= LACUNARITY;
        }

        // if (samplePos.X == 0 && samplePos.Y == 0)
        // {
        //     GD.Print(noiseValue2D);
        // }
        // GD.Print();

        // var noise = noiseGenerator.GetNoise(samplePos.X, samplePos.Y, samplePos.Z);

        // float density = Math.Clamp(noise, 0.0f, 1.0f);
        // density += Math.Clamp(SDFUtils.SdSphere(samplePos, 2f), -1.0f, 0.1f);
        // float density = (samplePos.Y * 5f) - noiseValue2D; //noiseGenerator.GetNoise(samplePos.X, samplePos.Z); //samplePos.Y;

        // value 0 to 1
        // float elevation = (noiseValue2D + 1.0f) * 0.5f;
        // float density = samplePos.Y - noiseValue2D;
        // float density = SDFUtils.SdSphere(samplePos, 1); // * ();
        // density = Math.Clamp(density, -1.0f, 1.0f);
        // float density = ;
        // density += 1;
        // density /= 2;
        // float density = Math.Clamp(2 - samplePos.Length(), -1.0f, 1.0f);
        // convert to 0 to 1 for data storage

        // density *= sbyte.MaxValue;

        sbyte output = sbyte.CreateSaturating(noiseValue2D * sbyte.MaxValue);
        // existing[tupleIndex] = ;
        // byte output = (byte)Mathf.RoundToInt(density * byte.MaxValue);
        // if (output > TerrainData.CENTER_ISOLEVEL)
        // {
        //     return TerrainData.CENTER_ISOLEVEL + 1;
        // }
        // GD.Print(output);
        return output;
    }

    static unsafe sbyte[] PopulateNewSampleData(ChunkID cID)
    {
        // var s = Stopwatch.StartNew();
        var newData = new sbyte[TerrainConsts.VoxelArrayLength];
        // return newData;
        // var sphereTex = new sbyte[
        // var existing = new Dictionary<(int, int), float>();

        var chunkCoord = cID.GetSampleVector3I();
        // return newData;
        // noiseGenerator.

        Parallel.For(
            0,
            TerrainConsts.VoxelArrayLength,
            (int index) =>
            {
                // for (int index = 0; index < TerrainConsts.VoxelArrayLength; index++)
                // {
                var currentCoord = TerrainConsts.IndexToCoord3D(index, TerrainConsts.VoxelsPerAxis);
                newData[index] = EvaluateNoise(currentCoord, chunkCoord);
                // }
            }
        );
        // s.Stop();
        // GD.Print("eee ", s.Elapsed.TotalMilliseconds);

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
        const int PoolChunks = 40;
        for (int x = 0; x < PoolChunks; x++)
        {
            var c = InstantiateChunk();
            freeChunks.Enqueue(c);
        }
        // GD.Print("pool ", freeChunks.Count);

        // ReloadChunk(new(0, 0, 0)).Wait();

        // noiseGenerator.SetNoiseType(FastNoiseLiteSharp.NoiseType.OpenSimplex2S);
        // noiseGenerator.SetFractalType(FastNoiseLiteSharp.FractalType.Ridged);
        // noiseGenerator.SetFractalOctaves(3);
        // noiseGenerator.SetFractalLacunarity(2.010f);
        // noiseGenerator.SetFractalGain(0.670f);
        // noiseGenerator.SetFrequency(0.40f);

        // ReloadChunk(new(0, 1, 0));
        // ReloadChunk(new(0, -1, 0));
        // QueueChunk(new(0, 0, 0));
        // // QueueChunk(new(0, 2, 1));
        RefreshPlayerChunks();
        lastRefreshMs = Time.GetTicksMsec();
        // GD.Print("Max Mod Bytes ", TerrainParams.MAX_MOD_BYTES);
    }

    void RefreshPlayerChunks()
    {
        // int idx = 0;
        // make the loop bigger by one to remove the outermost chunk IDs
        const int GenerateDistance = TerrainConsts.ChunkViewDistance;

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
                    bool chunkIsQueued = loadingChunks.ContainsKey(toQueue);
                    // GD.Print("coord ", idx, chunkIsLoaded, chunkIsQueued);
                    // idx++;
                    if ((!chunkIsLoaded) && (!chunkIsQueued))
                    {
                        // queue only if the chunk is not there and it's not already queued
                        // _ = ReloadChunk(toQueue);
                        chunksToReload.Enqueue(toQueue);
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
                if (Math.Abs(disp[x]) > TerrainConsts.ChunkViewDistance + 1)
                {
                    TryUnloadChunk(cID);
                }
            }
        }
    }

    public override void _PhysicsProcess(double delta)
    {
        // GD.Print("l ", loadingChunks.Count);
        if (!loadedChunks.ContainsKey(currentPlayerChunk))
        {
            ReloadChunk(currentPlayerChunk).Wait();
        }

        var newPlayerChunk = ChunkID.GetNearestID(player.Position);

        if (currentPlayerChunk != newPlayerChunk)
        {
            // Update our current chunk
            currentPlayerChunk = newPlayerChunk;

            // if ((Time.GetTicksMsec() - lastRefreshMs) >= MinRefreshInterval)
            // {
            RefreshPlayerChunks();

            // the timer is so we don't trigger a ton of reloads
            // when near the edge of two chunks. Shouldn't cause a problem
            // because we already ensure the current player chunk is loaded.
            lastRefreshMs = Time.GetTicksMsec();
            // }
        }
    }

    // Called every frame. 'delta' is the elapsed time since the previous frame.
    public override void _Process(double delta)
    {
        // Reload a chunk if it's in the queue
        // if (!currentlyComputing)
        // {
        if (chunksToReload.TryDequeue(out var chunkID))
        {
            // If it somehow already got loaded,skip
            if (loadedChunks.ContainsKey(chunkID) || loadingChunks.ContainsKey(chunkID))
            {
                return;
            }
            _ = ReloadChunk(chunkID);
        }
        // }
    }

    readonly Vector3I[] offsetsToUpdate =
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

    /// <summary>
    /// Terraform a point on the terrain
    /// </summary>
    /// <param name="worldPoint">Point in world space to modify</param>
    /// <param name="strength">Strength should be a value from -1 to 1</param>
    /// <param name="blend">Should be a value from 0 to 1</param>
    public void TerraformPoint(Vector3 worldPoint, float strength, float blend)
    {
        terraformMarker.Position = worldPoint;

        var closestChunk = ChunkID.GetNearestID(worldPoint);
        var closestChunkPos = closestChunk.GetSampleVector3I();

        // Might as well hard code it since it's easier that way
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

            var chunkWorldPos = currentChunkID.GetSampleVector() * TerrainConsts.ChunkScale;

            var localTerraformPos = (worldPoint - chunkWorldPos) / TerrainConsts.ChunkScale;

            // Now the terraform pos is from 0 to 1
            localTerraformPos += new Vector3(0.5f, 0.5f, 0.5f);

            // Scale it to the number of sample points per chunk
            localTerraformPos *= TerrainConsts.VoxelsPerAxisMinusTwo;

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
                        || (curTerraformPos[element] > (TerrainConsts.VoxelsPerAxisMinusTwo))
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

                int modIndex = TerrainConsts.Coord3DToIndex(
                    (Vector3I)correctedCoord.Round(),
                    TerrainConsts.VoxelsPerAxis
                );

                if (modIndex >= 0 && modIndex < chunkData.Length)
                {
                    var thisTerraAmount = strength;
                    if (curTerraformPos != localTerraformPos)
                    {
                        thisTerraAmount *= blend;
                    }

                    // Whether to add or subtract
                    // Adding terrain "adds" negative values to the terrain
                    // because isolevel < 0 is "inside" terrain
                    // int sign = add ? -1 : 1;

                    chunkData[modIndex] = sbyte.CreateSaturating(
                        chunkData[modIndex] - (thisTerraAmount * sbyte.MaxValue)
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
