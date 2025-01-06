using System.Runtime.CompilerServices;

namespace Game.Terrain.Noise
{
    public static partial class IcariaNoise
    {
        /// <summary>High-quality version of GradientNoise() that returns a rotated
        /// slice of 3D gradient noise to remove grid alignment artifacts.</summary>
        [MethodImpl(512)] // aggressive optimization on supported runtimes
        public static float GradientNoiseHQ(float x, float y, int seed = 0)
        {
            // rotation from https://noiseposti.ng/posts/2022-01-16-The-Perlin-Problem-Breaking-The-Cycle.html
            float xy = x + y;
            float s2 = xy * -0.2113248f;
            float z = xy * -0.5773502f;
            x += s2;
            y += s2;

            // GradientNoise3D() won't get inlined automatically so its manually inlined here.
            // seems to improve preformance by around 5 to 10%
            int ix = x > 0 ? (int)x : (int)x - 1;
            int iy = y > 0 ? (int)y : (int)y - 1;
            int iz = z > 0 ? (int)z : (int)z - 1;
            float fx = x - ix;
            float fy = y - iy;
            float fz = z - iz;

            ix += seed * IcariaConstants.SeedPrime;

            ix += IcariaConstants.Offset;
            iy += IcariaConstants.Offset;
            iz += IcariaConstants.Offset;
            int p1 =
                ix * IcariaConstants.XPrime1
                + iy * IcariaConstants.YPrime1
                + iz * IcariaConstants.ZPrime1;
            int p2 =
                ix * IcariaConstants.XPrime2
                + iy * IcariaConstants.YPrime2
                + iz * IcariaConstants.ZPrime2;
            int llHash = p1 * p2;
            int lrHash = (p1 + IcariaConstants.XPrime1) * (p2 + IcariaConstants.XPrime2);
            int ulHash = (p1 + IcariaConstants.YPrime1) * (p2 + IcariaConstants.YPrime2);
            int urHash = (p1 + IcariaConstants.XPlusYPrime1) * (p2 + IcariaConstants.XPlusYPrime2);
            float zLowBlend = InterpolateGradients3D(llHash, lrHash, ulHash, urHash, fx, fy, fz);
            llHash = (p1 + IcariaConstants.ZPrime1) * (p2 + IcariaConstants.ZPrime2);
            lrHash = (p1 + IcariaConstants.XPlusZPrime1) * (p2 + IcariaConstants.XPlusZPrime2);
            ulHash = (p1 + IcariaConstants.YPlusZPrime1) * (p2 + IcariaConstants.YPlusZPrime2);
            urHash =
                (p1 + IcariaConstants.XPlusYPlusZPrime1) * (p2 + IcariaConstants.XPlusYPlusZPrime2);
            float zHighBlend = InterpolateGradients3D(
                llHash,
                lrHash,
                ulHash,
                urHash,
                fx,
                fy,
                fz - 1
            );
            float sz = fz * fz * (3 - 2 * fz);
            return zLowBlend + (zHighBlend - zLowBlend) * sz;
        }

        /// <summary> 3D -1 to 1 gradient noise function. Analagous to Perlin Noise. </summary>
        [MethodImpl(512)] // aggressive optimization on supported runtimes
        public static float GradientNoise3D(float x, float y, float z, int seed = 0)
        {
            // see comments in GradientNoise()
            int ix = x > 0 ? (int)x : (int)x - 1;
            int iy = y > 0 ? (int)y : (int)y - 1;
            int iz = z > 0 ? (int)z : (int)z - 1;
            float fx = x - ix;
            float fy = y - iy;
            float fz = z - iz;

            ix += seed * IcariaConstants.SeedPrime;

            ix += IcariaConstants.Offset;
            iy += IcariaConstants.Offset;
            iz += IcariaConstants.Offset;
            int p1 =
                ix * IcariaConstants.XPrime1
                + iy * IcariaConstants.YPrime1
                + iz * IcariaConstants.ZPrime1;
            int p2 =
                ix * IcariaConstants.XPrime2
                + iy * IcariaConstants.YPrime2
                + iz * IcariaConstants.ZPrime2;
            int llHash = p1 * p2;
            int lrHash = (p1 + IcariaConstants.XPrime1) * (p2 + IcariaConstants.XPrime2);
            int ulHash = (p1 + IcariaConstants.YPrime1) * (p2 + IcariaConstants.YPrime2);
            int urHash = (p1 + IcariaConstants.XPlusYPrime1) * (p2 + IcariaConstants.XPlusYPrime2);
            float zLowBlend = InterpolateGradients3D(llHash, lrHash, ulHash, urHash, fx, fy, fz);
            llHash = (p1 + IcariaConstants.ZPrime1) * (p2 + IcariaConstants.ZPrime2);
            lrHash = (p1 + IcariaConstants.XPlusZPrime1) * (p2 + IcariaConstants.XPlusZPrime2);
            ulHash = (p1 + IcariaConstants.YPlusZPrime1) * (p2 + IcariaConstants.YPlusZPrime2);
            urHash =
                (p1 + IcariaConstants.XPlusYPlusZPrime1) * (p2 + IcariaConstants.XPlusYPlusZPrime2);
            float zHighBlend = InterpolateGradients3D(
                llHash,
                lrHash,
                ulHash,
                urHash,
                fx,
                fy,
                fz - 1
            );
            float sz = fz * fz * (3 - 2 * fz);
            return zLowBlend + (zHighBlend - zLowBlend) * sz;
        }

        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        static unsafe float InterpolateGradients3D(
            int llHash,
            int lrHash,
            int ulHash,
            int urHash,
            float fx,
            float fy,
            float fz
        )
        {
            // see comments in InterpolateGradients2D()
            int xHash,
                yHash,
                zHash;
            xHash = (llHash & IcariaConstants.GradAndMask) | IcariaConstants.GradOrMask;
            yHash = xHash << IcariaConstants.GradShift1;
            zHash = xHash << IcariaConstants.GradShift2;
            float llGrad = fx * *(float*)&xHash + fy * *(float*)&yHash + fz * *(float*)&zHash; // dot-product
            xHash = (lrHash & IcariaConstants.GradAndMask) | IcariaConstants.GradOrMask;
            yHash = xHash << IcariaConstants.GradShift1;
            zHash = xHash << IcariaConstants.GradShift2;
            float lrGrad = (fx - 1) * *(float*)&xHash + fy * *(float*)&yHash + fz * *(float*)&zHash;
            xHash = (ulHash & IcariaConstants.GradAndMask) | IcariaConstants.GradOrMask;
            yHash = xHash << IcariaConstants.GradShift1;
            zHash = xHash << IcariaConstants.GradShift2;
            float ulGrad = fx * *(float*)&xHash + (fy - 1) * *(float*)&yHash + fz * *(float*)&zHash; // dot-product
            xHash = (urHash & IcariaConstants.GradAndMask) | IcariaConstants.GradOrMask;
            yHash = xHash << IcariaConstants.GradShift1;
            zHash = xHash << IcariaConstants.GradShift2;
            float urGrad =
                (fx - 1) * *(float*)&xHash + (fy - 1) * *(float*)&yHash + fz * *(float*)&zHash;
            float sx = fx * fx * (3 - 2 * fx);
            float sy = fy * fy * (3 - 2 * fy);
            float lowerBlend = llGrad + (lrGrad - llGrad) * sx;
            float upperBlend = ulGrad + (urGrad - ulGrad) * sx;
            return lowerBlend + (upperBlend - lowerBlend) * sy;
        }
    }
}
