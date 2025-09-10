using System;

namespace VoxelGame;

public class Maths
{
    public static float EuclideanRemainder(float a, float b) =>  a - (float.Abs(b) * (int)Math.Floor(a / (double) float.Abs(b)));

}