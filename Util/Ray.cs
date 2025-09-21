using System;
using System.Numerics;
using OpenTK.Mathematics;
using Vector3 = OpenTK.Mathematics.Vector3;

namespace VoxelGame.Util;

public struct Ray
{
    public Vector3 Origin;
    public Vector3 Direction;

    public Vector3i HitBlockPosition;
    public Vector3i PreviousHitBlockPosition;

    // as specified at https://www.shadertoy.com/view/4dX3zl
    public bool TryHit(World world, int maxDistance)
    {
        if (Direction == Vector3.Zero) Direction = Vector3.UnitZ;
        
        Vector3i RayBlockPosition = VectorMath.ComponentFloor(Origin);
        Vector3i RayPreviousBlockPosition = RayBlockPosition;
        Vector3 RayDeltaDistance = Vector3.Abs(new Vector3(Direction.Length) / Direction);
        if (float.IsPositiveInfinity(RayDeltaDistance.X)) RayDeltaDistance.X = float.MaxValue;
        if (float.IsPositiveInfinity(RayDeltaDistance.Y)) RayDeltaDistance.Y = float.MaxValue;
        if (float.IsPositiveInfinity(RayDeltaDistance.Z)) RayDeltaDistance.Z = float.MaxValue;
        if (float.IsNegativeInfinity(RayDeltaDistance.X)) RayDeltaDistance.X = float.MinValue;
        if (float.IsNegativeInfinity(RayDeltaDistance.Y)) RayDeltaDistance.Y = float.MinValue;
        if (float.IsNegativeInfinity(RayDeltaDistance.Z)) RayDeltaDistance.Z = float.MinValue;
        
        Vector3i RayStep = VectorMath.ComponentSign(Direction);
        Vector3 SideDistance = (VectorMath.ComponentSign(Direction) * (RayBlockPosition - Origin) + ((Vector3)VectorMath.ComponentSign(Direction) * 0.5f) + 0.5f) * RayDeltaDistance;
        
        while (Vector3.Distance(VectorMath.ComponentFloor(Origin), RayBlockPosition) <= maxDistance)
        {
            if (world.GetBlockId(RayBlockPosition) != "air")
            {
                HitBlockPosition = RayBlockPosition;
                PreviousHitBlockPosition = RayPreviousBlockPosition;
                return true;
            }

            Vector3b mask = VectorMath.ComponentLessThanEqual(SideDistance, Vector3.ComponentMin(SideDistance.Yzx, SideDistance.Zxy));
            Vector3 maskFloat = (mask.X ? 1 : 0, mask.Y ? 1 : 0, mask.Z ? 1 : 0);

            SideDistance += maskFloat * RayDeltaDistance;
            RayPreviousBlockPosition = RayBlockPosition;
            RayBlockPosition += (Vector3i)maskFloat * RayStep;    
        }
        
        return false;
    }
}

public class VectorMath
{
    public static Vector3i ComponentFloor(Vector3 vector)
    {
        return new Vector3i((int)Math.Floor(vector.X), (int)Math.Floor(vector.Y), (int)Math.Floor(vector.Z));
    }

    public static Vector3i ComponentSign(Vector3 vector)
    {
        return new Vector3i(float.Sign(vector.X), float.Sign(vector.Y), float.Sign(vector.Z));
    }

    public static Vector3b ComponentLessThanEqual(Vector3 a, Vector3 b)
    {
        return (a.X <= b.X, a.Y <= b.Y, a.Z <= b.Z);
    }

    public static int Flatten(Vector3i vector, int width, int height)
    {
        // yHeight is Config.ColumnSize * Config.ChunkSize.
        return vector.Y + (vector.X * Config.ChunkSize * Config.ColumnSize) + (vector.Z * Config.ChunkSize * Config.ChunkSize * Config.ColumnSize);
    }
}