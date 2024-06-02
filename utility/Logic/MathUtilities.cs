namespace Utilities;

using System;
using Godot;

// Credit to GodotUtilities! https://github.com/firebelley/GodotUtilities/
// Modified to use MathF
public static class MathUtil
{
    public static RandomNumberGenerator RNG { get; private set; } = new();

    static MathUtil()
    {
        RNG.Randomize();
    }

    // This isn't really a lerp so replaced with ExpDecay() function
    // since a lerp every frame isn't linear anymore :P
    // public static float DeltaLerp(float from, float to, float deltaTime, float smoothing) =>
    //     Mathf.Lerp(from, to, 1f - MathF.Exp(-deltaTime * smoothing));

    public static float ExpDecay(float current, float target, float decayAmount, float deltaTime) =>
        target + ((current - target) * MathF.Exp(-decayAmount * deltaTime));

    public static void SeedRandomNumberGenerator(ulong seed) =>
        RNG = new RandomNumberGenerator { Seed = seed };
}
