using System;
using System.Collections.Generic;
using OpenTK.Mathematics;
using VoxelGame.Networking;

namespace VoxelGame;

public class Block
{
    public BlockModel Model = new BlockModel();
    public BoundingBox BoundingBox = new BoundingBox();
    public string Id = string.Empty;
    public bool IsSolid = true;
    public bool IsTransparent = false;
    
    public Block SetBlockModel(BlockModel model)
    {
        Model = model;
        return this;
    }

    public virtual void OnBlockPlace(World world, Vector3i blockPosition)
    {
        world.SetBlock(blockPosition, this);
    }

    public virtual void OnBlockDestroy(World world, Vector3i blockPosition)
    {
        world.SetBlock(blockPosition);
    }

    public virtual void OnBlockMesh(World world, Vector3i blockPosition)
    {
        world.AddModel(blockPosition, Model);
    }

    public override bool Equals(object? obj)
    {
        return ((Block)obj).Id == Id;
    }
}