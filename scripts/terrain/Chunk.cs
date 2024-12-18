namespace Game.Terrain;

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Godot;

public partial class Chunk : MeshInstance3D
{
    //////////// Useful constants //////////////
    public const int IndicesPerTri = 3;

    const bool RecalcNormals = false; // this understandably messes up seamless chunking

    ///////////// Godot Node references ////////////
    [Export]
    StaticBody3D physicsBody;

    [Export]
    CollisionShape3D collider;

    [Export]
    Material chunkMaterial;

    [Export]
    MeshInstance3D testObj;

    //////////////  Actual Godot visual mesh and physics shape //////////////
    ArrayMesh visualMesh = new();
    ConcavePolygonShape3D physicsShape = new();

    ////////////////// Mesh and physics collider information for Godot interop ////////
    Godot.Collections.Array meshInfoArray = [];
    Godot.Collections.Dictionary collisionInfoDict = [];

    /////////// Output vertex arrays, normals, indices lists ///////////////
    readonly List<Godot.Vector3> verts = [];
    readonly List<Godot.Vector3> normals = [];
    int[] indices = new int[100];
    int numIndices;

    ////////// Physics shape vertices /////////
    Vector3[] collisionVertices = new Vector3[100];

    // readonly List<Godot.Vector3> collisionVertices = [];

    ////////// Cached Godot resource Ids ////////
    Rid physicsShapeRid;
    Rid visualMeshRid;
    Rid visualMaterialRid;

    // Cached chunk sample coordinate
    Vector3I chunkSampleCoord;
    sbyte[] terrainVolume;

    // Buffer to use during mesh generation
    int[] buffer = new int[
        (TerrainConsts.VoxelsPerAxis + 1) * (TerrainConsts.VoxelsPerAxis + 1) * 2
    ];

    public unsafe void ProcessChunk(Vector3I currentChunkID, sbyte[] terrainVolume) //, Stopwatch s)
    {
        Stopwatch s = Stopwatch.StartNew();

        this.chunkSampleCoord = currentChunkID;

        verts.Clear();
        normals.Clear();
        // Array.Clear(indices);
        numIndices = 0;
        // collisionVertices.Clear();

        this.terrainVolume = terrainVolume;

        s.Stop();
        var setup = s.Elapsed.TotalMilliseconds;
        s.Restart();

        // Generate vertices
        ProcessVoxels();

        s.Stop();
        var processed = s.Elapsed.TotalMilliseconds;
        s.Restart();

        if (RecalcNormals)
        {
            RecalculateNormals();
        }
        CreateMesh();

        s.Stop();
        var generated = s.Elapsed.TotalMilliseconds;
        s.Restart();

        CallDeferred(Chunk.MethodName.FinalizeInScene);

        s.Stop();

        // if (numIndices > IndicesPerTri)
        // {
        //     GD.Print($"setup {setup}");
        //     GD.Print($"processed voxels {processed}");
        //     GD.Print($"generated mesh {generated}");
        //     GD.Print($"end time {s.Elapsed.TotalMilliseconds}");
        // }
    }

    unsafe void ProcessVoxels()
    {
        // GD.Print("hi");
        // Samples 01 and 02 - those are local arrays of interleaved voxel data (rows of voxels along Z coordinate)
        //
        //   imagine a slice of voxel data (YX) :
        //
        //     Y
        //     |
        //     |
        //   [   ][   ][   ]
        //   [ 2 ][ 3 ][   ]
        //   [ 0 ][ 1 ][   ] ---- X
        //
        //   array samples01 are 'bottom' voxels - at current Y value
        //   array samples23 are 'top' voxels - at next Y value
        //   Only one array is filled per Y loop step, because we can reuse the other one.
        //
        //   More about interleaving voxel data in next steps.

        var samples01 = stackalloc sbyte[64];
        var samples23 = stackalloc sbyte[64];

        var samples = stackalloc float[8];

        var pos = stackalloc int[3];

        fixed (sbyte* volumePtr = &terrainVolume[0])
        {
            // Reusable masks with voxels sign bits
            uint mask0 = 0,
                mask1 = 0,
                mask2 = 0,
                mask3 = 0;

            for (int x = 0; x < TerrainConsts.VoxelsPerAxisMinusOne; x++)
            {
                // (0) Because some values are saved between loop iterations (along Y), so it is needed to precalc some values.
                //
                (mask2, mask3) = ExtractSignBitsAndSamples(volumePtr, samples23, x);

                for (int y = 0; y < TerrainConsts.VoxelsPerAxisMinusOne; y++)
                {
                    // Samples arrays are reused in Y loop, so swap those:
                    // samples01 should contain voxels at current Y coordinate.
                    // samples23 should contain voxels at Y + 1 coordinate.
                    // So they can be reused while iterating over Y.
                    //
                    var temp = samples01;
                    samples01 = samples23;
                    samples23 = temp;

                    // Previous masks are also reused:
                    //
                    mask0 = mask2;
                    mask1 = mask3;

                    (mask2, mask3) = ExtractSignBitsAndSamples(volumePtr, samples23, x, y);

                    // (6) Store all masks (4 voxels 'rows' each 32 voxels == 4x 32 bit masks) in masks simd vector variable.
                    //
                    var masks = Vector128.Create(mask0, mask1, mask2, mask3);

                    // (7) Early termination check - check if there is a mix of zeroes and ones in masks.
                    // (7) If not, it means whole column (32 x 2 x 2 voxels) is either under surface or above - so no need to mesh those, because meshing will not produce any triangles.
                    // (7) v128(UInt32.MaxValue) is a mask (all 1) controlling which bits should be tested.
                    //
                    // var zerosOnes = Sse41.TestNotZAndNotC(
                    //     masks.AsInt32(),
                    //     Vector128<int>.AllBitsSet
                    // );
                    if (masks == Vector128<uint>.AllBitsSet)
                    {
                        continue;
                    }
                    if (masks == Vector128<uint>.Zero)
                    {
                        continue;
                    }

                    // var zerosOnes =
                    //     (masks != Vector128<uint>.AllBitsSet) && (masks != Vector128<uint>.Zero);
                    // var ZF = (Vector128<uint>.AllBitsSet & masks) == Vector128<uint>.Zero;
                    // var CF = (Vector128<uint>.Zero & masks) == Vector128<uint>.Zero;
                    // var zerosOnes = (!ZF) && (!CF);

                    // if (!zerosOnes)
                    //     continue;
                    // (7) test_mix_ones_zeroes : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274,5153,5153,5153,5596,3343,3864,5903&techs=SSE4_1&cats=Logical&ig_expand=7214


                    // (8) Extract last bits from each of 4 masks and store them in upper bits 4-7 (leave bits 0-3 zeroed).
                    // (8) Because movemask extract sign bits (highests bits), reversing masks in step 4 & 5 was necessary.
                    //
                    uint cornerMask = Vector128.ExtractMostSignificantBits(masks) << 4;
                    // (8) movemask_ps : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274,5153,5153,5153,5596,3343,3864&cats=Miscellaneous&techs=SSE&ig_expand=4878

                    // samples[0] = 0;
                    // samples[1] = 0;
                    // samples[2] = 0;
                    // samples[3] = 0;
                    // samples[4] = 0;
                    // samples[5] = 0;
                    // samples[6] = 0;
                    // samples[7] = 0;

                    for (int z = 0; z < TerrainConsts.VoxelsPerAxisMinusOne; z++)
                    {
                        // (9) Thats why we shifted masks 4 bits to left in step (8)
                        // (9) In each iteration along Z value we can reuse masks extracted in previous step.
                        //
                        cornerMask >>= 4;

                        // (10) Previously we extracted highests bits in masks, to extract next bits we just need to left shift them.
                        // (10) slli_epi32 shift parameter mu be const.
                        //
                        // masks = Sse2.ShiftLeftLogical(masks, 1);
                        masks <<= 1;
                        // (10) slli_epi32 : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274&othertechs=BMI1,BMI2&techs=SSE,SSE2,SSE3,SSSE3,SSE4_1,SSE4_2&cats=Shift&ig_expand=6537

                        // (11) Extract next 4 bits from masks to build proper CornerMask for group (2x2x2) of voxels.
                        // (11) Corner mask is 8 bit where each bit tell if specific voxel from group (2x2x2) is negative or not.
                        // (11) In step (9) we reuse currently extracted 4 bits by right shifting.
                        //
                        // var movemask = Vector128.masks.AsSingle()
                        cornerMask |= Vector128.ExtractMostSignificantBits(masks) << 4;

                        // (12) Early termination,
                        // (12) If all bits are 0 or 1 no triangles will be produced (no edge crossing)
                        //
                        if (cornerMask == 0 || cornerMask == 255)
                            continue;

                        // (13) Extract edgemask from cornermask (edgeTable is precalculated array)
                        //
                        int edgeMask = SurfaceNetUtils.EdgeTable[cornerMask];

                        // (14) Collect 8 samples (voxel values) from interleaved sample arrays (01 23)
                        //
                        var zz = z + z;
                        samples[0] = samples01[zz + 0];
                        samples[1] = samples01[zz + 1];
                        samples[2] = samples23[zz + 0];
                        samples[3] = samples23[zz + 1];
                        samples[4] = samples01[zz + 2];
                        samples[5] = samples01[zz + 3];
                        samples[6] = samples23[zz + 2];
                        samples[7] = samples23[zz + 3];

                        // Indexer acces is required in next step (pos[variable]...) so cant use plain int values
                        // I could use int3 struct, but for some reasons those simple stackallocated arrays works faster. No idea why.
                        //
                        pos[0] = x;
                        pos[1] = y;
                        pos[2] = z;

                        // Flip Orientation Depending on Corner Sign
                        var flipTriangle = (cornerMask & 1) != 0;

                        MeshSamples(pos, samples, edgeMask, flipTriangle);
                    }
                }
            }
        }
    }

    static unsafe (uint, uint) ExtractSignBitsAndSamples(
        sbyte* volumePtr,
        sbyte* samples23,
        int x,
        int y = -1 /* first case, outside Y loop */
    )
    {
        // Used for reversing voxels in simd vector in step (4)

        var shuffleReverseByteOrder = Vector128.Create(
            15,
            14,
            13,
            12,
            11,
            10,
            9,
            8,
            7,
            6,
            5,
            4,
            3,
            2,
            1,
            0
        );

        // (1) Pointer is needed for SSE instructions :
        //
        // GD.Print(this.terrainVolume.Length);
        var ptr = volumePtr + (x << SurfaceNetUtils.xShift) + ((y + 1) << SurfaceNetUtils.yShift);

        // (2) Load voxel data in parts of 16.
        // (2) 'lo' and 'hi' - SSE buffers are 16 bytes in size, but chunk is in size 32, so those values refer to first 16 voxels and second 16 voxels in a row along Z coordinate.
        //
        // MemoryMarshal.Cast<sbyte, Vector128<sbyte>>(ptr);
        // var lo2 = Vector128.Load(ptr); //Sse2.LoadVector128(ptr + 0); /* load first 16 voxels */
        // var hi2 = Vector128.Load(ptr + 16); /* load next  16 voxels */
        // var lo3 = Vector128.Load(ptr + 1024); /* load first 16 voxels on X + 1 */
        // var hi3 = Vector128.Load(ptr + 1040); /* load next  16 voxels on X + 1 */
        var lo2 = Vector128.Load(ptr + 0); /* load first 16 voxels */
        var hi2 = Vector128.Load(ptr + 16); /* load next  16 voxels */
        var lo3 = Vector128.Load(ptr + 1024); /* load first 16 voxels on X + 1 */
        var hi3 = Vector128.Load(ptr + 1040); /* load next  16 voxels on X + 1 */

        // (2) load_si128 : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274,5153,5153,5153,5596,3343&techs=SSE2&ig_expand=4294,6942,6952,6942,6942,4294&cats=Load
        //
        // todo: use Sse4_1.stream_load_si128 instead ?
        // todo: use loadu_si128 for unaligned access ??


        // (3) Save voxel data in local samplesArrays.
        // (3) But, instead of storing them one by one like in volume array, we interleave voxels with their neighbours on X + 1 coordinate
        // (3) The unpacklo/hi intrinsics are used to interleave provided data.
        // (3) Imagine a voxel slice (XZ) at Y == 0 :
        //
        //		  Z
        //		  |
        //		  |
        //		 [ 31 ][ 1055 ]   []          =>  [ hi2 ][ hi3 ]
        //		 ...
        //		 [  2 ][ 1026 ]   []		  =>  [ lo2 ][ lo3 ]
        //		 [  1 ][ 1025 ]   []
        //		 [  0 ][ 1024 ]...[] ----- X
        //
        //		Voxels 0-15 are loaded into lo2, voxels 16-31 into hi2.
        //		Same for voxels at X + 1 (lo3/hi3).
        //
        //		Result of unpack intrinsics looks like:
        //
        //		[ 1055 ]  (64 element array - samples23)
        //		[   31 ]
        //		...
        //		[ 1025 ]
        //		[    1 ]
        //		[ 1024 ]
        //		[    0 ]
        //
        //	(3) Such way of storing voxels makes future steps faster,
        //		because we need to access neighbouring voxels (at X + 1) while iterating along Z coordinate,
        //		so instead of accessing 2 'arrays' we access only 1.
        //
        //  (3) Second samples array (samples01) is used in same way. More about this inside loop Y
        //
        // Vector128.shu
        // var (lo2_lo3_low, lo2_lo3_high) = Vector128.Widen(lo2, lo3);
        // lo2.interleave(lo3);
        // var (hi2_hi3_low, hi2_hi3_high) = hi2.interleave(hi3);

        // Vector128.Shuffle(lo2, lo3);
        // Vector.Narrow(lo2, lo3);
        // // lo2.StoreAlignedNonTemporal()

        // Vector128.StoreAligned(Vector128.WithLower(lo2, lo3.GetLower()), samples23 + 00);
        // Vector128.StoreAligned(Vector128.WithUpper(lo2, lo3.GetUpper()), samples23 + 16);
        // Vector128.StoreAligned(Vector128.WithLower(hi2, hi3.GetLower()), samples23 + 32);
        // Vector128.StoreAligned(Vector128.WithUpper(hi2, hi3.GetUpper()), samples23 + 48);

        Vector128.Store(SurfaceNetUtils.UnpackLow(lo2, lo3), samples23 + 00);
        Vector128.Store(SurfaceNetUtils.UnpackHigh(lo2, lo3), samples23 + 16);
        Vector128.Store(SurfaceNetUtils.UnpackLow(hi2, hi3), samples23 + 32);
        Vector128.Store(SurfaceNetUtils.UnpackHigh(hi2, hi3), samples23 + 48);
        // Vector128.Store()

        // var (lo2_lower, lo2_upper) = Vector128.Widen(lo2);
        // var (lo2_lower, lo2_upper) = Vector128.Widen(lo3);
        // var (lo2_lower, lo2_upper) = Vector128.Widen(lo2);
        // var (lo2_lower, lo2_upper) = Vector128.Widen(lo2);
        // Vector256.Create(lo2, lo3);
        // Vector128.LoadAligned

        // Vector128.StoreAligned(Sse2.UnpackLow(lo2, lo3), samples23 + 00);
        // Vector128.StoreAligned(Sse2.UnpackHigh(lo2, lo3), samples23 + 16);
        // Vector128.StoreAligned(Sse2.UnpackLow(hi2, hi3), samples23 + 32);
        // Vector128.StoreAligned(Sse2.UnpackHigh(hi2, hi3), samples23 + 48);
        // (3) store_si128 : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274,5153,5153,5153,5596&techs=SSE2&cats=Store&ig_expand=6872
        // (3) unpack(lo/hi)_epi8 : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274,5153,5153,6090,6033&othertechs=BMI1,BMI2&techs=SSE2&cats=Swizzle&ig_expand=7355


        // (4) Shuffle bytes:
        // (4) shuffleReverseByteOrder its a SSE vector with indices controlling shuffle operation (what and where to put)
        //

        // lo2 = Ssse3.Shuffle(lo2, shuffleReverseByteOrder);
        // lo3 = Ssse3.Shuffle(lo3, shuffleReverseByteOrder);
        // hi2 = Ssse3.Shuffle(hi2, shuffleReverseByteOrder);
        // hi3 = Ssse3.Shuffle(hi3, shuffleReverseByteOrder);

        lo2 = Vector128.Shuffle(lo2, shuffleReverseByteOrder);
        lo3 = Vector128.Shuffle(lo3, shuffleReverseByteOrder);
        hi2 = Vector128.Shuffle(hi2, shuffleReverseByteOrder);
        hi3 = Vector128.Shuffle(hi3, shuffleReverseByteOrder);
        // (4) shuffle_epi8 : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274,5153,5153,5153&techs=SSSE3&cats=Swizzle&ig_expand=6386


        // (5) Extract sign bits from 8 bit values.
        // (5) movemask intrinsics do that, 16 voxels at a time per instruction
        // (5) Result with (4) is that, the masks are reversed (first voxel sign bit is now last)
        // (5) Each mask stores bitsigns of one voxel 'row' along Z coordinate.
        // (5) Whats important, is that the bitsigns are nagated (~)
        //
        // var mask2 = Vector128.mask
        // Vector.
        // Vector128.
        // var mask2 = Vector128.AsInt32(Vector128.Create(lo2.GetLower(), hi2.GetLower())).ToScalar();
        // var mask3 = Vector128.AsInt32(Vector128.Create(lo3.GetLower(), hi3.GetLower())).ToScalar();

        // var mask2 = Sse2.MoveMask(lo2) << 16 | (Sse2.MoveMask(hi2));
        // var mask3 = Sse2.MoveMask(lo3) << 16 | (Sse2.MoveMask(hi3));

        var mask2 =
            Vector128.ExtractMostSignificantBits(lo2) << 16
            | (Vector128.ExtractMostSignificantBits(hi2));
        var mask3 =
            Vector128.ExtractMostSignificantBits(lo3) << 16
            | (Vector128.ExtractMostSignificantBits(hi3));
        // (5) movemask_epi8 : https://www.intel.com/content/www/us/en/docs/intrinsics-guide/index.html#expand=2528,951,4482,391,832,1717,291,338,5486,5304,5274,5153,5153,5153,5596,3343,3864&cats=Miscellaneous&techs=SSE2&ig_expand=4873


        // those weird mask reversing operation (4 & 5) is used later in step (8).
        return (mask2, mask3);
    }

    const float ChunkScalingFactor =
        TerrainConsts.ChunkScale
        * (((float)TerrainConsts.VoxelsPerAxis) / ((float)TerrainConsts.VoxelsPerAxis - 2));

    unsafe void MeshSamples(int* pos, float* samples, int edgeMask, bool flipTriangle)
    {
        const int R0 = (TerrainConsts.VoxelsPerAxis + 1) * (TerrainConsts.VoxelsPerAxis + 1);

        int* R = stackalloc int[3] { R0, TerrainConsts.VoxelsPerAxis + 1, 1 };
        int bufferIndex = pos[2] + (TerrainConsts.VoxelsPerAxis + 1) * pos[1];

        if (pos[0] % 2 == 0)
        {
            bufferIndex +=
                1 + (TerrainConsts.VoxelsPerAxis + 1) * (TerrainConsts.VoxelsPerAxis + 2);
        }
        else
        {
            R[0] = -R[0];
            bufferIndex += TerrainConsts.VoxelsPerAxis + 2;
        }

        // Buffer array is used to store vertex indices from previous loop steps.
        // We are using it to obtain indices for triangle.
        buffer[bufferIndex] = verts.Count;

        var chunkSpacePos = (
            new Godot.Vector3(pos[0], pos[1], pos[2])
            + GetVertexPositionFromSamples(samples, edgeMask)
        );

        var position =
            (chunkSpacePos - TerrainConsts.HalfVoxelAxisLengths) / TerrainConsts.VoxelsPerAxis;

        position *= ChunkScalingFactor;
        // GD.Print(GetVertexPositionFromSamples(samples, edgeMask));
        // / TerrainConsts.VoxelsPerAxisMinusOne
        // * TerrainConsts.ChunkScale;

        verts.Add(position);

        normals.Add(RecalcNormals ? Godot.Vector3.Zero : GetVertexNormalFromSamples(samples));

        // bounds.item.Encapsulate(position);

        // This buffer indexing stuff (buffer array, bufferIndex, R) and triangulation comes from:
        // https://github.com/TomaszFoster/NaiveSurfaceNets/blob/bec66c7a93c5b8ad4e52adf4f3091134c4c11c74/NaiveSurfaceNets.cs#L486



        // Add Faces (Loop Over 3 Base Components)
        for (var i = 0; i < 3; i++)
        {
            // First 3 Entries Indicate Crossings on Edge
            if ((edgeMask & (1 << i)) == 0)
                continue;

            var iu = (i + 1) % 3;
            var iv = (i + 2) % 3;

            if (pos[iu] == 0 || pos[iv] == 0)
                continue;

            var du = R[iu];
            var dv = R[iv];

            if (indices.Length < (numIndices + 6))
            {
                Array.Resize(ref indices, indices.Length * 2);
                Array.Resize(ref collisionVertices, collisionVertices.Length * 2);
            }

            fixed (int* indicesPtr = &indices[numIndices])
            {
                // this resizing gives a lot perf (from around ~0.420 to ~0.355 [ms])
                if (flipTriangle)
                {
                    indicesPtr[0] = buffer[bufferIndex]; //indices.Add(buffer[bufferIndex]);
                    indicesPtr[1] = buffer[bufferIndex - du - dv]; //indices.Add(buffer[bufferIndex - du - dv]);
                    indicesPtr[2] = buffer[bufferIndex - du]; //indices.Add(buffer[bufferIndex - du]);
                    indicesPtr[3] = buffer[bufferIndex]; //indices.Add(buffer[bufferIndex]);
                    indicesPtr[4] = buffer[bufferIndex - dv]; //indices.Add(buffer[bufferIndex - dv]);
                    indicesPtr[5] = buffer[bufferIndex - du - dv]; //indices.Add(buffer[bufferIndex - du - dv]);
                }
                else
                {
                    indicesPtr[0] = buffer[bufferIndex]; //indices.Add(buffer[bufferIndex]);
                    indicesPtr[1] = buffer[bufferIndex - du - dv]; //indices.Add(buffer[bufferIndex - du - dv]);
                    indicesPtr[2] = buffer[bufferIndex - dv]; //indices.Add(buffer[bufferIndex - dv]);
                    indicesPtr[3] = buffer[bufferIndex]; //indices.Add(buffer[bufferIndex]);
                    indicesPtr[4] = buffer[bufferIndex - du]; //indices.Add(buffer[bufferIndex - du]);
                    indicesPtr[5] = buffer[bufferIndex - du - dv]; //indices.Add(buffer[bufferIndex - du - dv]);
                }

                fixed (Godot.Vector3* colPtr = &collisionVertices[numIndices])
                {
                    for (int x = 0; x < 6; x++)
                    {
                        colPtr[x] = verts[indicesPtr[x]];
                    }
                }
                numIndices += 6;
            }
        }
    }

    static unsafe Godot.Vector3 GetVertexPositionFromSamples(float* samples, int edgeMask)
    {
        // Check each of 12 edges for edge crossing (different voxel signs).
        // Edge mask bits tells if there is edge crossing
        // If it is, compute crossing position as linear interpolation between 2 corner position.

        var vertPos = Godot.Vector3.Zero;
        int edgeCrossings = 0;

        if ((edgeMask & 1) != 0)
        {
            float s0 = samples[0];
            float s1 = samples[1];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(t, 0, 0);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 1) != 0)
        {
            float s0 = samples[0];
            float s1 = samples[2];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(0, t, 0);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 2) != 0)
        {
            float s0 = samples[0];
            float s1 = samples[4];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(0, 0, t);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 3) != 0)
        {
            float s0 = samples[1];
            float s1 = samples[3];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(1, t, 0);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 4) != 0)
        {
            float s0 = samples[1];
            float s1 = samples[5];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(1, 0, t);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 5) != 0)
        {
            float s0 = samples[2];
            float s1 = samples[3];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(t, 1, 0);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 6) != 0)
        {
            float s0 = samples[2];
            float s1 = samples[6];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(0, 1, t);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 7) != 0)
        {
            float s0 = samples[3];
            float s1 = samples[7];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(1, 1, t);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 8) != 0)
        {
            float s0 = samples[4];
            float s1 = samples[5];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(t, 0, 1);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 9) != 0)
        {
            float s0 = samples[4];
            float s1 = samples[6];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(0, t, 1);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 10) != 0)
        {
            float s0 = samples[5];
            float s1 = samples[7];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(1, t, 1);
            ++edgeCrossings;
        }
        if ((edgeMask & 1 << 11) != 0)
        {
            float s0 = samples[6];
            float s1 = samples[7];
            float t = s0 / (s0 - s1);
            vertPos += new Godot.Vector3(t, 1, 1);
            ++edgeCrossings;
        }

        // calculate mean position inside 1x1x1 box
        return vertPos / edgeCrossings;
    }

    static unsafe Godot.Vector3 GetVertexNormalFromSamples(float* samples)
    {
        // return Godot.Vector3.One;
        // Estimate normal vector from voxel values
        Godot.Vector3 normal =
            new(
                0
                    + (samples[1] - samples[0])
                    + (samples[3] - samples[2])
                    + (samples[5] - samples[4])
                    + (samples[7] - samples[6]),
                0
                    + (samples[2] - samples[0])
                    + (samples[3] - samples[1])
                    + (samples[6] - samples[4])
                    + (samples[7] - samples[5]),
                0
                    + (samples[4] - samples[0])
                    + (samples[5] - samples[1])
                    + (samples[6] - samples[2])
                    + (samples[7] - samples[3])
            );
        return normal * 0.002f; // scale normal because sampels are in range -127 127
    }

    unsafe void RecalculateNormals()
    {
        var verticesPtr = CollectionsMarshal.AsSpan(verts);
        var indicesPtr = indices; //CollectionsMarshal.AsSpan(indices);
        var normalPtr = CollectionsMarshal.AsSpan(normals);
        // var verticesPtr = (Vertex*)vertices.GetUnsafePtr();
        // var indicesPtr = (int*)indices.GetUnsafePtr();

        // var indicesLength = indices.Count;

        for (int i = 0; i < numIndices; i += 6)
        {
            // Each 2 consecutive triangles share one edge, so we need only 4 vertices
            var idx0 = indicesPtr[i + 0];
            var idx1 = indicesPtr[i + 1];
            var idx2 = indicesPtr[i + 2];
            var idx3 = indicesPtr[i + 4];

            var vert0 = verticesPtr[idx0];
            var vert1 = verticesPtr[idx1];
            var vert2 = verticesPtr[idx2];
            var vert3 = verticesPtr[idx3];

            var tangent0 = vert1 - vert0;
            var tangent1 = vert2 - vert0;
            var tangent2 = vert3 - vert0;

            var triangleNormal0 = tangent1.Cross(tangent0);
            var triangleNormal1 = tangent0.Cross(tangent2);

            if (float.IsNaN(triangleNormal0.X))
            {
                triangleNormal0 = Godot.Vector3.Zero;
            }
            if (float.IsNaN(triangleNormal1.X))
            {
                triangleNormal1 = Godot.Vector3.Zero;
            }

            normalPtr[idx0] = normalPtr[idx0] + triangleNormal0 + triangleNormal1;
            normalPtr[idx1] = normalPtr[idx1] + triangleNormal0 + triangleNormal1;
            normalPtr[idx2] = normalPtr[idx2] + triangleNormal0;
            normalPtr[idx3] = normalPtr[idx3] + triangleNormal1;
        }
    }

    void CreateMesh()
    {
        // visualMesh.ClearSurfaces();
        RenderingServer.MeshClear(visualMeshRid);
        if (numIndices >= IndicesPerTri)
        {
            // GD.Print(numIndices);
            // Make our Godot array and throw the data in

            meshInfoArray[(int)Mesh.ArrayType.Vertex] = CollectionsMarshal.AsSpan(verts);
            meshInfoArray[(int)Mesh.ArrayType.Normal] = CollectionsMarshal.AsSpan(normals);
            meshInfoArray[(int)Mesh.ArrayType.Index] = indices.AsSpan(0, numIndices);

            collisionInfoDict["faces"] = collisionVertices.AsSpan(0, numIndices); //CollectionsMarshal.AsSpan();
            // physicsShape.Data = collisionVertices.AsSpan(0, numIndices).ToArray();
            RenderingServer.MeshAddSurfaceFromArrays(
                visualMeshRid,
                RenderingServer.PrimitiveType.Triangles,
                meshInfoArray
            );

            RenderingServer.MeshSurfaceSetMaterial(visualMeshRid, 0, visualMaterialRid);

            PhysicsServer3D.ShapeSetData(physicsShapeRid, collisionInfoDict);
        }
    }

    public void FinalizeInScene()
    {
        Position = (Godot.Vector3)chunkSampleCoord * TerrainConsts.ChunkScale;

        // Sometimes we'll have not enough vertices for a triangle
        if (numIndices < IndicesPerTri)
        {
            collider.Disabled = true;
            return;
        }

        // physicsShape.Data = collisionVertices.AsSpan(0, numIndices).ToArray();
        collider.Disabled = false;
    }

    public void HibernateChunk()
    {
        collider.Disabled = true;
    }

    // Called when the node enters the scene tree for the first time.
    public override void _Ready()
    {
        // Ensure this chunk has the ArrayMesh
        this.Mesh = visualMesh;
        collider.Shape = physicsShape;
        meshInfoArray.Resize((int)Mesh.ArrayType.Max);

        physicsShapeRid = physicsShape.GetRid();
        visualMeshRid = visualMesh.GetRid();
        visualMaterialRid = chunkMaterial.GetRid();

        collisionInfoDict["backface_collision"] = false;
    }

    // Not needed
    // // Called every frame. 'delta' is the elapsed time since the previous frame.
    // public override void _Process(double delta) { }
}
