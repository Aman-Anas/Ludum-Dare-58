namespace Game.Terrain.Utils;

using System;
using Godot;

public static class SDFUtils
{
    public static float SdSphere(Vector3 p, float s)
    {
        return p.Length() - s;
    }

    public static float SdBox(Vector3 p, Vector3 b)
    {
        Vector3 q = p.Abs() - b;
        return new Vector3(
                Mathf.Max(q.X, 0.0f),
                Mathf.Max(q.Y, 0.0f),
                Mathf.Max(q.Z, 0.0f)
            ).Length() + MathF.Min(MathF.Max(q.X, MathF.Max(q.Y, q.Z)), 0.0f);
    }
}
