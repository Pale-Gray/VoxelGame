using System;

namespace VoxelGame;

public class Maths
{
    public static float EuclideanRemainder(float a, float b) =>  a - (float.Abs(b) * (int)Math.Floor(a / (double) float.Abs(b)));
    public static float InverseLerp(float v, float a, float b) => (v - a) * (1.0f / (b - a));

    public static float Map(float from, float to, float start, float end, float t)
    {
        return float.Lerp(from, to, float.Clamp(InverseLerp(t, start, end), 0.0f, 1.0f));
    }

    public static float SmoothMap(float from, float to, float start, float end, float t)
    {
        return float.Lerp(from, to, Noise.Smoothstep(float.Clamp(InverseLerp(t, start, end), 0.0f, 1.0f)));
    }
}