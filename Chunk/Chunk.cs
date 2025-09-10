using System.Threading;
using OpenTK.Mathematics;

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
    public ChunkSection[] ChunkSections = new ChunkSection[Config.ColumnSize];
    public ChunkSectionMesh[] ChunkMeshes = new ChunkSectionMesh[Config.ColumnSize];
    public Vector2i Position;
    public ChunkStatus Status = ChunkStatus.Empty;
    public bool IsMeshIncomplete = false;
    public Mutex Mutex = new Mutex();
    public float ElapsedTime = 0.0f;
    public bool HasPriority = true;
    public bool IsUpdating = false;
    public Chunk(Vector2i position)
    {
        Position = position;
        for (int i = 0; i < Config.ColumnSize; i++)
        {
            ChunkSections[i] = new ChunkSection() {Position = (position.X, i, position.Y)};
            ChunkMeshes[i] = new ChunkSectionMesh();
        }
    }

    public void SetBlock(Vector3i localPosition, Block? block = null)
    {
        int index = localPosition.Y / Config.ChunkSize;
        if (block == null)
        {
            ChunkSections[index].SetBlock("air", localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
            ChunkSections[index].SetTransparent(false, localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
            ChunkSections[index].SetSolid(false, localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
        }
        else
        {
            ChunkSections[index].SetBlock(block.Id, localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
            ChunkSections[index].SetTransparent(block.IsTransparent, localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
            ChunkSections[index].SetSolid(block.IsSolid, localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
        }
    }

    public string GetBlockId(Vector3i localPosition)
    {
        int index = localPosition.Y / Config.ChunkSize;
        return ChunkSections[index].GetBlockId((localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z)) ?? "air";
    }

    public bool GetTransparent(Vector3i localPosition)
    {
        int index = localPosition.Y / Config.ChunkSize;
        return ChunkSections[index].GetTransparent(localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
    }

    public bool GetSolid(Vector3i localPosition)
    {
        int index = localPosition.Y / Config.ChunkSize;
        return ChunkSections[index].GetSolid(localPosition.X, localPosition.Y % Config.ChunkSize, localPosition.Z);
    }
}