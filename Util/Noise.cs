using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using OpenTK.Mathematics;

namespace VoxelGame;

public class Noise
{
    public static uint Random2(int seed, Vector2i position)
    {
        uint s = Unsafe.BitCast<int, uint>(seed);
        uint x = Unsafe.BitCast<int, uint>(position.X);
        uint y = Unsafe.BitCast<int, uint>(position.Y);

        s ^= s << 17;
        s ^= s << 7;
        s ^= s << 11;
    
        x ^= x << 15;
        x ^= x << 7;
        x ^= x << 9;
    
        y ^= y << 15;
        y ^= y << 7;
        y ^= y << 11;

        uint result = s + y + x;
        result ^= result << 9;
        result ^= result << 7;
        result ^= result << 3;
        
        return result;
    }

    private static Dictionary<Vector3i, uint> _cache = new();
    
    public static uint Random3(int seed, Vector3i position)
    {
        uint s = Unsafe.BitCast<int, uint>(seed);
        uint x = Unsafe.BitCast<int, uint>(position.X);
        uint y = Unsafe.BitCast<int, uint>(position.Y);
        uint z = Unsafe.BitCast<int, uint>(position.Z);

        s ^= s << 17;
        s ^= s << 7;
        s ^= s << 11;
    
        x ^= x << 15;
        x ^= x << 7;
        x ^= x << 9;
    
        y ^= y << 15;
        y ^= y << 7;
        y ^= y << 11;

        z ^= z << 17;
        z ^= z << 7;
        z ^= z << 13;

        uint result = s + z + y + x;
        result ^= result << 9;
        result ^= result << 7;
        result ^= result << 3;
        // result ^= result << 11;

        // _cache.TryAdd(position, result);
        return result;
    }

    public static float FloatRandom2(int seed, Vector2i position) => (float) ((Random2(seed, position) / (double)uint.MaxValue) - 0.5f) * 2.0f;
    public static float FloatRandom3(int seed, Vector3i position) => (float)((Random3(seed, position) / (double)uint.MaxValue) - 0.5f) * 2.0f;

    // as defined in https://registry.khronos.org/OpenGL-Refpages/gl4/html/smoothstep.xhtml, except it's expected t is from 0 to 1
    public static float Smoothstep(float t) => t * t * (3.0f - 2.0f * t);
    
    public static float Value2(int seed, Vector2 position)
    {
        Vector2 interpolant = (Maths.EuclideanRemainder(position.X, 1), Maths.EuclideanRemainder(position.Y, 1));
        Vector2i samplePosition = new Vector2i((int)float.Floor(position.X), (int)float.Floor(position.Y));

        float bottomLeft = FloatRandom2(seed, samplePosition);
        float bottomRight = FloatRandom2(seed, samplePosition + Vector2i.UnitX);
        float topLeft = FloatRandom2(seed, samplePosition + Vector2i.UnitY);
        float topRight = FloatRandom2(seed, samplePosition + Vector2i.One);

        float top = float.Lerp(topLeft, topRight, Smoothstep(interpolant.X));
        float bottom = float.Lerp(bottomLeft, bottomRight, Smoothstep(interpolant.X));
        
        return float.Lerp(bottom, top, Smoothstep(interpolant.Y));
    }
    
    public static float Value2(int seed, Vector2 position, bool normalized, int octaves)
    {
        float val = 0.0f;
        float totalAmplitude = 0.0f;

        for (float i = 0; i < octaves; i++)
        {
            float v = float.Pow(2, i);
            val += Value2(seed, position * v) / v;
            totalAmplitude += 1.0f / v;
        }

        return normalized ? val / totalAmplitude : val;
    }

    public static float Value3(int seed, Vector3 position)
    {
        Vector3 interpolant = (Maths.EuclideanRemainder(position.X, 1), Maths.EuclideanRemainder(position.Y, 1), Maths.EuclideanRemainder(position.Z, 1));
        Vector3i samplePosition = new Vector3i((int)float.Floor(position.X), (int)float.Floor(position.Y), (int)float.Floor(position.Z));

        float topBackLeft = FloatRandom3(seed, samplePosition + (0, 1, 1));
        float topBackRight = FloatRandom3(seed, samplePosition + (1, 1, 1));
        float topBack = float.Lerp(topBackLeft, topBackRight, Smoothstep(interpolant.X));
        float topFrontLeft = FloatRandom3(seed, samplePosition + (0, 1, 0));
        float topFrontRight = FloatRandom3(seed, samplePosition + (1, 1, 0));
        float topFront = float.Lerp(topFrontLeft, topFrontRight, Smoothstep(interpolant.X));
        float top = float.Lerp(topFront, topBack, Smoothstep(interpolant.Z));
        
        float bottomBackLeft = FloatRandom3(seed, samplePosition + (0, 0, 1));
        float bottomBackRight = FloatRandom3(seed, samplePosition + (1, 0, 1));
        float bottomBack = float.Lerp(bottomBackLeft, bottomBackRight, Smoothstep(interpolant.X));
        float bottomFrontLeft = FloatRandom3(seed, samplePosition + (0, 0, 0));
        float bottomFrontRight = FloatRandom3(seed, samplePosition + (1, 0, 0));
        float bottomFront = float.Lerp(bottomFrontLeft, bottomFrontRight, Smoothstep(interpolant.X));
        float bottom = float.Lerp(bottomFront, bottomBack, Smoothstep(interpolant.Z));
        
        return float.Lerp(bottom, top, Smoothstep(interpolant.Y));
    }

    public static float Value3(int seed, Vector3 position, bool normalized, int octaves)
    {
        float val = 0.0f;
        float totalAmplitude = 0.0f;

        for (float i = 0; i < octaves; i++)
        {
            float v = float.Pow(2, i);
            val += Value3(seed, position * v) / v;
            totalAmplitude += 1.0f / v;
        }

        return normalized ? val / totalAmplitude : val;
    }

    public static Vector3i NoiseSizeValue3(Vector3i step, Vector3i size)
    {
        return (size / step) + Vector3i.One;
    }

    public static void PregenerateValue3(Span<float> array, int seed, Vector3i step, Vector3i arraySize, Vector3 offset, Vector3 divisor, bool normalized, int octaves)
    {
        for (int x = 0; x < arraySize.X; x++)
        {
            for (int y = 0; y < arraySize.Y; y++)
            {
                for (int z = 0; z < arraySize.Z; z++)
                {
                    Vector3 pos = (offset + (new Vector3(x, y, z) * step)) / divisor;
                    array[ToIndex((x, y, z), arraySize)] = Value3(seed, pos, normalized, octaves);
                }
            }
        }
    }

    public static float Value3(Span<float> values, Vector3 position, Vector3i size)
    {
        // size += Vector3i.One;
        
        Vector3i indexPosition = (Vector3i) new Vector3(float.Floor(position.X), float.Floor(position.Y), float.Floor(position.Z));
        Vector3 interpolant = (Maths.EuclideanRemainder(position.X, 1), Maths.EuclideanRemainder(position.Y, 1), Maths.EuclideanRemainder(position.Z, 1));
        
        float topFrontLeft = values[ToIndex(indexPosition + (0, 1, 0), size)];
        float topFrontRight = values[ToIndex(indexPosition + (1, 1, 0), size)];
        float topFront = float.Lerp(topFrontLeft, topFrontRight, interpolant.X);
        float topBackLeft = values[ToIndex(indexPosition + (0, 1, 1), size)];
        float topBackRight = values[ToIndex(indexPosition + (1, 1, 1), size)];
        float topBack = float.Lerp(topBackLeft, topBackRight, Smoothstep(interpolant.X));

        float top = float.Lerp(topFront, topBack, Smoothstep(interpolant.Z));
        
        float bottomFrontLeft = values[ToIndex(indexPosition + (0, 0, 0), size)];
        float bottomFrontRight = values[ToIndex(indexPosition + (1, 0, 0), size)];
        float bottomFront = float.Lerp(bottomFrontLeft, bottomFrontRight, interpolant.X);
        float bottomBackLeft = values[ToIndex(indexPosition + (0, 0, 1), size)];
        float bottomBackRight = values[ToIndex(indexPosition + (1, 0, 1), size)];
        float bottomBack = float.Lerp(bottomBackLeft, bottomBackRight, interpolant.X);

        float bottom = float.Lerp(bottomFront, bottomBack, Smoothstep(interpolant.Z));
        
        return float.Lerp(bottom, top, Smoothstep(interpolant.Y));
    }

    private static int ToIndex(Vector3i position, Vector3i size)
    {
        return position.X + (position.Z * size.X) + (position.Y * size.X * size.Z);
    }
}