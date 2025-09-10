using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VoxelGame.Networking;

namespace VoxelGame;

public class World
{
    public Dictionary<Vector2i, Chunk> Chunks = new();
    public WorldGenerator Generator;

    public World()
    {
        Generator = new WorldGenerator(this);
    }

    public void AddChunk(Vector2i position, Chunk chunk)
    {
        chunk.Position = position;
        for (int i = 0; i < Config.ColumnSize; i++)
        {
            chunk.ChunkSections[i].Position = (position.X, i, position.Y);
        }
        Chunks.TryAdd(position, chunk);
    }

    public void Draw(Camera camera)
    {
        Config.ChunkShader.Bind();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2d, Config.Atlas.Id);
        GL.UniformMatrix4f(Config.ChunkShader.GetUniformLocation("uProjection"), 1, true, ref camera.Projection);
        GL.UniformMatrix4f(Config.ChunkShader.GetUniformLocation("uView"), 1, true, ref camera.View);
        GL.Uniform1f(Config.ChunkShader.GetUniformLocation("uTexture"), 0);
        
        foreach (Chunk column in Chunks.Values)
        {
            bool drawable = false;
            for (int i = 0; i < column.ChunkMeshes.Length; i++)
            {
                if (column.ChunkMeshes[i].SolidVerticesLength > 0 || column.ChunkMeshes[i].TransparentVerticesLength > 0) drawable = true;
            }

            if (drawable) column.ElapsedTime += Config.DeltaTime;
            
            for (int i = 0; i < column.ChunkMeshes.Length; i++)
            {
                GL.Uniform1f(Config.ChunkShader.GetUniformLocation("uDrawTime"), column.ElapsedTime);
                GL.Uniform3f(Config.ChunkShader.GetUniformLocation("uChunkPosition"), column.ChunkSections[i].Position.X, column.ChunkSections[i].Position.Y, column.ChunkSections[i].Position.Z);
                if (column.ChunkMeshes[i].SolidVerticesLength > 0) column.ChunkMeshes[i].DrawSolid(camera);
                GL.Disable(EnableCap.CullFace);
                if (column.ChunkMeshes[i].TransparentVerticesLength > 0) column.ChunkMeshes[i].DrawTransparent(camera);
                GL.Enable(EnableCap.CullFace);
            }
        }
    }

    public string GetBlockId(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (!Chunks.ContainsKey(chunkPosition.Xz) || globalBlockPosition.Y < 0 || globalBlockPosition.Y >= Config.ChunkSize * Config.ColumnSize) return "air";
        
        return Chunks[chunkPosition.Xz].ChunkSections[chunkPosition.Y].GetBlockId(ChunkMath.GlobalToLocal(globalBlockPosition)) ?? "air";
    }

    public bool GetBlockIsSolid(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (!Chunks.ContainsKey(chunkPosition.Xz) || chunkPosition.Y < 0 ||
            chunkPosition.Y >= Config.ColumnSize) return false;

        Vector3i l = ChunkMath.GlobalToLocal(globalBlockPosition);
        return Chunks[chunkPosition.Xz].ChunkSections[chunkPosition.Y].GetSolid(l.X, l.Y, l.Z);
    }

    public bool GetBlockIsTransparent(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (!Chunks.ContainsKey(chunkPosition.Xz) || chunkPosition.Y < 0 ||
            chunkPosition.Y >= Config.ColumnSize) return false;

        Vector3i l = ChunkMath.GlobalToLocal(globalBlockPosition);
        return Chunks[chunkPosition.Xz].ChunkSections[chunkPosition.Y].GetTransparent(l.X, l.Y, l.Z);
    }

    public void SetBlock(Vector3i globalBlockPosition, Block? block = null)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.ContainsKey(chunkPosition.Xz) && chunkPosition.Y >= 0 &&
            chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i pos = ChunkMath.GlobalToLocal(globalBlockPosition);
            Chunks[chunkPosition.Xz].SetBlock((pos.X, globalBlockPosition.Y, pos.Z), block);
        }
    }

    public void EnqueueChunksFromBlockPosition(Vector3i blockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(blockPosition);
        {
            if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk))
            {
                for (int y = -1; y <= 1; y++)
                {
                    if (chunkPosition.Y + y >= 0 && chunkPosition.Y + y < Config.ColumnSize)
                    {
                        chunk.ChunkMeshes[chunkPosition.Y + y].ShouldUpdate = true;
                    }
                }

                chunk.Status = ChunkStatus.Mesh;
                // Generator.EnqueueChunk(chunkPosition.Xz, ChunkStatus.Mesh, true);
            }
        }
        
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (x != 0 || z != 0)
                {
                    if (Chunks.TryGetValue(chunkPosition.Xz + (x, z), out Chunk chunk))
                    {
                        for (int y = -1; y <= 1; y++)
                        {
                            if (chunkPosition.Y + y >= 0 && chunkPosition.Y + y < Config.ColumnSize)
                            {
                                chunk.ChunkMeshes[chunkPosition.Y + y].ShouldUpdate = true;
                            }
                        }

                        chunk.Status = ChunkStatus.Mesh;

                        // Generator.EnqueueChunk(chunkPosition.Xz + (x, z), ChunkStatus.Mesh, false);
                    }
                }
            }
        }
    }
    
    public void AddModel(Vector3i globalBlockPosition, BlockModel model)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        Vector3i localBlockPosition = ChunkMath.GlobalToLocal(globalBlockPosition);
        
        if (GetBlockIsTransparent(globalBlockPosition))
        {
            List<ChunkVertex> vertexData = Chunks[chunkPosition.Xz].ChunkMeshes[chunkPosition.Y].TransparentVertices;
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY)) model.AddFace(vertexData, Direction.Top, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY)) model.AddFace(vertexData, Direction.Bottom, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX)) model.AddFace(vertexData, Direction.Right, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX)) model.AddFace(vertexData, Direction.Left, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Back, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Front, localBlockPosition);
        }
        else
        {
            List<ChunkVertex> vertexData = Chunks[chunkPosition.Xz].ChunkMeshes[chunkPosition.Y].SolidVertices;
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY) || GetBlockIsTransparent(globalBlockPosition + Vector3i.UnitY)) model.AddFace(vertexData, Direction.Top, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY) || GetBlockIsTransparent(globalBlockPosition - Vector3i.UnitY)) model.AddFace(vertexData, Direction.Bottom, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX) || GetBlockIsTransparent(globalBlockPosition + Vector3i.UnitX)) model.AddFace(vertexData, Direction.Right, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX) || GetBlockIsTransparent(globalBlockPosition - Vector3i.UnitX)) model.AddFace(vertexData, Direction.Left, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ) || GetBlockIsTransparent(globalBlockPosition + Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Back, localBlockPosition);
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ) || GetBlockIsTransparent(globalBlockPosition - Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Front, localBlockPosition);
        }
    }
}