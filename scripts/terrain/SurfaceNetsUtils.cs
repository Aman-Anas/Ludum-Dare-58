using System;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.Arm;
using System.Runtime.Intrinsics.X86;

namespace Game.Terrain;

public static class SurfaceNetUtils
{
    public static readonly ushort[] EdgeTable = new ushort[256];

    public const int xShift = 10;
    public const int yShift = 5;
    public const int zShift = 0;

    static SurfaceNetUtils()
    {
        PrecalculateEdgeTable();
    }

    static void PrecalculateEdgeTable()
    {
        // Edge table is a lookup array for obtaining edgemasks.
        // Cornermask should be used as an index to search proper edgemask.
        // Cornermasks are 8bit binary flags where each bit tells if specific 'corner' (voxel) has negative or positive value.
        // What edgemask is ?
        // Its a bit mask of 12 edges of a cube.
        // Specific bit is enabled, if there is a 'crossing' of corresponding edge (sign change between 2 voxels).
        // If there is a sign change, such edge can produce vertex (or at least).
        // Final vertex position is calculated as a mean position of all vertices from all 'crossed' edges.
        // Magic behind calculating that edge table is unknown to me.

        var cubeEdges = new int[24];
        int k = 0;
        for (int i = 0; i < 8; ++i)
        {
            for (int j = 1; j <= 4; j <<= 1)
            {
                int p = i ^ j;
                if (i <= p)
                {
                    cubeEdges[k++] = i;
                    cubeEdges[k++] = p;
                }
            }
        }

        for (int i = 0; i < 256; ++i)
        {
            int em = 0;
            for (int j = 0; j < 24; j += 2)
            {
                var a = Convert.ToBoolean(i & (1 << cubeEdges[j]));
                var b = Convert.ToBoolean(i & (1 << cubeEdges[j + 1]));
                em |= a != b ? (1 << (j >> 1)) : 0;
            }
            EdgeTable[i] = (ushort)em;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> UnpackLow(Vector128<sbyte> left, Vector128<sbyte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackLow(left, right);
        }
        else if (!AdvSimd.Arm64.IsSupported)
        {
            throw new NotSupportedException(
                "You aren't using an x86 or an ARM64 device? is this a toaster? (no offense)"
            );
        }
        return AdvSimd.Arm64.ZipLow(left, right);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<sbyte> UnpackHigh(Vector128<sbyte> left, Vector128<sbyte> right)
    {
        if (Sse2.IsSupported)
        {
            return Sse2.UnpackHigh(left, right);
        }
        else if (!AdvSimd.Arm64.IsSupported)
        {
            throw new NotSupportedException(
                "You aren't using an x86 or an ARM64 device? is this a toaster? (no offense)"
            );
        }
        return AdvSimd.Arm64.ZipHigh(left, right);
    }
}
