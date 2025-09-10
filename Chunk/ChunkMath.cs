using System;
using OpenTK.Mathematics;

namespace VoxelGame;

public class ChunkMath
{
    public static int ChebyshevDistance(Vector2i a, Vector2i b)
    {
        return int.Max(int.Abs(b.X - a.X), int.Abs(b.Y - a.Y));
    }

    public static int ManhattanDistance(Vector2i a, Vector2i b)
    {
        return int.Abs(a.X - b.X) + int.Abs(a.Y - b.Y);
    }

    public static Vector3i PositionToBlockPosition(Vector3 position)
    {
        return ((int) Math.Floor(position.X), (int) Math.Floor(position.Y), (int) Math.Floor(position.Z));
    }

    public static Vector3i GlobalToLocal(Vector3i global)
    {
        return ((int)Maths.EuclideanRemainder(global.X, Config.ChunkSize), (int)Maths.EuclideanRemainder(global.Y, Config.ChunkSize), (int)Maths.EuclideanRemainder(global.Z, Config.ChunkSize));
    }

    public static Vector3i GlobalToChunk(Vector3i global)
    {
        return Floor((Vector3) global / Config.ChunkSize);
    }

    public static Vector3i Floor(Vector3 vector)
    {
        return ((int)Math.Floor(vector.X), (int)Math.Floor(vector.Y), (int)Math.Floor(vector.Z));
    }
}