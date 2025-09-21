using System.Threading;
using OpenTK.Mathematics;
using VoxelGame.Util;

namespace VoxelGame;

public enum ChunkStatus
{
    Empty,
    Mesh,
    Upload,
    Done
}

public class Chunk
{
    public Palette<string> Data = new Palette<string>(Config.ChunkSize * Config.ChunkSize * Config.ChunkSize * Config.ColumnSize);
    public ChunkSectionMesh[] ChunkMeshes = new ChunkSectionMesh[Config.ColumnSize];
    public Vector2i Position;
    public ChunkStatus Status = ChunkStatus.Empty;
    public bool IsMeshIncomplete = false;
    public float ElapsedTime = 0.0f;
    public bool HasPriority = true;
    public bool IsUpdating = false;
    public Chunk(Vector2i position)
    {
        Position = position;
        for (int i = 0; i < Config.ColumnSize; i++)
        {
            ChunkMeshes[i] = new ChunkSectionMesh() { ChunkPosition = (position.X, i, position.Y) };
        }
    }

    public void SetBlock(Vector3i localPosition, Block? block = null)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return;
        
        if (block != null)
        {
            Data.Insert(VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize), block.Id);
        }
        else
        {
            Data.Insert(VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize), "air");
        }
    }

    public string GetBlockId(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return "air";
        return Data.Get(VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)) ?? "air";
    }

    public bool GetTransparent(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return false;
        return Register.GetBlockFromId(GetBlockId(localPosition)).IsTransparent;
    }

    public bool GetSolid(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return false;
        return Register.GetBlockFromId(GetBlockId(localPosition)).IsSolid;
    }
}