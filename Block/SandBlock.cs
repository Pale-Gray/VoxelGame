using OpenTK.Mathematics;

namespace VoxelGame;

public class SandBlock : Block
{
    public override void OnBlockPlace(World world, Vector3i blockPosition)
    {
        base.OnBlockPlace(world, blockPosition);
        world.AddLight(blockPosition, 15, 0, 15);
    }
}