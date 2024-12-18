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
    readonly ConcurrentDictionary<Vector3I, Chunk> loadedChunks = [];

    readonly ConcurrentQueue<Vector3I> chunksToReload = new(); // If it doesn't exist, load it, otherwise reload
    readonly ConcurrentDictionary<Vector3I, byte> loadingChunks = [];

    readonly ConcurrentQueue<Chunk> freeChunks = new(); // For object pooling

    bool currentlyProcessing;

    // Minimum time before triggering a queue, so it doesnt happen too often at chunk borders
    const int MinRefreshInterval = 100; //ms

    ulong lastRefreshMs = Time.GetTicksMsec();

    Vector3I currentPlayerChunk;

    // Chunk generation
    // We'll have to clear this after some limit and on area changes
    readonly Dictionary<Vector3I, sbyte[]> knownChunkData = [];

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
    public void UpdateChunkData(Vector3I chunkID, sbyte[] chunkData)
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

    Task ReloadChunk(Vector3I chunkID)
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
            (
                (((Vector3)coordinate) / (TerrainConsts.VoxelsPerAxis - 2))
                - new Vector3(0.5f, 0.5f, 0.5f)
            ) + chunkCoord;

        const int OCTAVES = 5;
        const float PERSISTENCE = 0.5f;
        const float LACUNARITY = 2.0f;
        float amplitude = 1.0f;
        float frequency = 1f; //new(1.0f, 1.0f, 1.0f);

        // Total radius of the sphere
        const float SphereRadius = 428f;
        const float TerrainHeight = 60.0f;

        // the radius at which to get our samples from. (imagine a sphere)
        // the larger this is, the more surface area ("zoom") we get for our sample source
        const float SphereSampleRadius = 5;

        // constants in sample space
        const float C_SphereRadius = SphereRadius / TerrainConsts.ChunkScale;
        const float C_TerrainHeight = TerrainHeight / TerrainConsts.ChunkScale;

        Vector3 sphericalPos = samplePos.Normalized() * SphereSampleRadius;

        float noiseValue = 0;
        for (int i = 0; i < OCTAVES; i++)
        {
            var noisePoint = frequency * sphericalPos; //new Vector3(samplePos.X, 0, samplePos.Z); //sphericalPos
            noiseValue +=
                amplitude * IcariaNoise.GradientNoise3D(noisePoint.X, noisePoint.Y, noisePoint.Z);
            amplitude *= PERSISTENCE;
            frequency *= LACUNARITY;
        }

        float density = SDFUtils.SdSphere(
            samplePos,
            // 1
            // C_SphereRadius + noiseValue * C_TerrainHeight
            C_SphereRadius + ((noiseValue * 0.5f) + 2.0f) * C_TerrainHeight
        );
        // float density = samplePos.Y;

        sbyte output = sbyte.CreateSaturating(density * sbyte.MaxValue * 20);

        return output;
    }

    static unsafe sbyte[] PopulateNewSampleData(Vector3I chunkCoordinate)
    {
        // var s = Stopwatch.StartNew();
        // var newData = new sbyte[];
        var newData = new sbyte[TerrainConsts.VoxelArrayLength];

        Parallel.For(
            0,
            TerrainConsts.VoxelArrayLength,
            (int index) =>
            {
                // for (int index = 0; index < TerrainConsts.VoxelArrayLength; index++)
                // {
                var currentCoord = TerrainConsts.IndexToCoord3D(index, TerrainConsts.VoxelsPerAxis);
                newData[index] = EvaluateNoise(currentCoord, chunkCoordinate);
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

    void TryUnloadChunk(Vector3I cID)
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
                    Vector3I toQueue =
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
        var playerPos = currentPlayerChunk;

        foreach (Vector3I cID in loadedChunks.Keys)
        {
            var disp = playerPos - cID;
            for (int x = 0; x < 3; x++)
            {
                if (
                    Math.Abs(disp[x])
                    > ((TerrainConsts.ChunkViewDistance + 1) * TerrainConsts.ChunkScale)
                )
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

        var newPlayerChunk = TerrainConsts.GetNearestChunkID(player.Position);

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

    // Might as well hard code it since it's easier that way
    readonly Vector3I[] offsetsToUpdate =
    [
        new(-1, -1, -1),
        new(-1, -1, 0),
        new(-1, -1, 1),
        new(-1, 0, -1),
        new(-1, 0, 0),
        new(-1, 0, 1),
        new(-1, 1, -1),
        new(-1, 1, 0),
        new(-1, 1, 1),
        new(0, -1, -1),
        new(0, -1, 0),
        new(0, -1, 1),
        new(0, 0, -1),
        new(0, 0, 0),
        new(0, 0, 1),
        new(0, 1, -1),
        new(0, 1, 0),
        new(0, 1, 1),
        new(1, -1, -1),
        new(1, -1, 0),
        new(1, -1, 1),
        new(1, 0, -1),
        new(1, 0, 0),
        new(1, 0, 1),
        new(1, 1, -1),
        new(1, 1, 0),
        new(1, 1, 1)
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

        var nearestChunk = TerrainConsts.GetNearestChunkID(worldPoint);
        var chunkSpacePoint = worldPoint / TerrainConsts.ChunkScale;
        // GD.Print(nearestChunk, chunkSpacePoint);

        foreach (var chunkOffset in offsetsToUpdate)
        {
            // To track whether this chunk should be reloaded
            bool isModified = false;

            // The position of this chunk
            var thisChunk = nearestChunk + chunkOffset;

            // TODO: If the chunk data doesn't exist, we should wait for the chunk to load
            if (!knownChunkData.TryGetValue(thisChunk, out var chunkData))
            {
                continue;
            }

            // find the voxel nearest to our target point
            // local vector relative to chunk center
            var localTerraformPos = chunkSpacePoint - thisChunk;
            // Convert from -0.5 to 0.5 => 0 to 1
            localTerraformPos += new Vector3(0.5f, 0.5f, 0.5f);
            // Scale it to the number of sample points per chunk => 0 to 30
            localTerraformPos *= TerrainConsts.VoxelsPerAxisMinusTwo;
            // Shims for chunking => 1 to 31 (indices 0 and 32 are for the next chunk over)
            localTerraformPos = localTerraformPos + Vector3.One;
            localTerraformPos = localTerraformPos.Round();

            foreach (var offset in offsetsToUpdate)
            {
                // If this point isn't in range, we should ignore it
                bool valid = true;

                var offsetPosition = localTerraformPos + offset;

                for (int x = 0; x < 3; x++)
                {
                    if (
                        (offsetPosition[x] < 0)
                        || (offsetPosition[x] > TerrainConsts.VoxelsPerAxisMinusOne)
                    )
                    {
                        valid = false;
                    }
                }
                if (!valid)
                {
                    continue;
                }

                int modIndex = TerrainConsts.Coord3DToIndex(
                    (Vector3I)offsetPosition.Round(),
                    TerrainConsts.VoxelsPerAxis
                );

                sbyte prev = chunkData[modIndex];

                var currentStrength = strength;

                if (offset.Length() != 0)
                {
                    currentStrength *= blend;
                }

                var output = sbyte.CreateSaturating(prev - (currentStrength * sbyte.MaxValue));

                chunkData[modIndex] = output;
                isModified = true;
            }

            // If we modified the chunk, update its data and reload it
            if (isModified)
            {
                UpdateChunkData(thisChunk, chunkData);
            }
        }
    }
}
