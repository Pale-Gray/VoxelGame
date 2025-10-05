using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using VoxelGame.Networking;

namespace VoxelGame;

public class World
{
    public ConcurrentDictionary<Vector2i, Chunk> Chunks = new();
    public WorldGenerator Generator;

    public World()
    {
        Generator = new WorldGenerator(this);
    }

    public void AddChunk(Vector2i position, Chunk chunk)
    {
        chunk.Position = position;
        Chunks.TryAdd(position, chunk);
    }

    public void Draw(Camera camera)
    {
        // draw solid
        Config.Gbuffer.Bind();
        Config.ChunkShader.Bind();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2d, Config.Atlas.Id);
        GL.UniformMatrix4f(Config.ChunkShader.GetUniformLocation("uProjection"), 1, true, ref camera.Projection);
        GL.UniformMatrix4f(Config.ChunkShader.GetUniformLocation("uView"), 1, true, ref camera.View);
        GL.Uniform1f(Config.ChunkShader.GetUniformLocation("uTexture"), 0);
        
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);
        foreach (Chunk chunk in Chunks.Values)
        {
            for (int i = 0; i < chunk.ChunkMeshes.Length; i++)
            {
                GL.Uniform1f(Config.ChunkShader.GetUniformLocation("uDrawTime"), chunk.ElapsedTime);
                GL.Uniform3f(Config.ChunkShader.GetUniformLocation("uChunkPosition"), chunk.Position.X, 0, chunk.Position.Y);
                if (chunk.ChunkMeshes[i].SolidVerticesLength > 0)
                {
                    chunk.ElapsedTime += Config.DeltaTime;
                    chunk.ChunkMeshes[i].DrawSolid(camera);
                }
            }
        }
        Config.Gbuffer.Unbind();
        Config.Gbuffer.Draw();
        
        // draw transparent
        Config.Gbuffer.Bind();
        Config.ChunkShader.Bind();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2d, Config.Atlas.Id);
        GL.UniformMatrix4f(Config.ChunkShader.GetUniformLocation("uProjection"), 1, true, ref camera.Projection);
        GL.UniformMatrix4f(Config.ChunkShader.GetUniformLocation("uView"), 1, true, ref camera.View);
        GL.Uniform1f(Config.ChunkShader.GetUniformLocation("uTexture"), 0);
        GL.ClearColor(0, 0, 0, 0);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        GL.Disable(EnableCap.CullFace);
        // GL.DepthMask(false);
        foreach (Chunk chunk in Chunks.Values)
        {
            for (int i = 0; i < chunk.ChunkMeshes.Length; i++)
            {
                GL.Uniform1f(Config.ChunkShader.GetUniformLocation("uDrawTime"), chunk.ElapsedTime);
                GL.Uniform3f(Config.ChunkShader.GetUniformLocation("uChunkPosition"), chunk.Position.X, 0, chunk.Position.Y);
                if (chunk.ChunkMeshes[i].TransparentVerticesLength > 0) chunk.ChunkMeshes[i].DrawTransparent(camera);
            }
        }
        GL.Enable(EnableCap.CullFace);
        // GL.DepthMask(true);
        Config.Gbuffer.Unbind();
        Config.Gbuffer.Draw(0.75f);
    }

    public bool GetBlockIsSolid(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (!Chunks.ContainsKey(chunkPosition.Xz) || chunkPosition.Y < 0 ||
            chunkPosition.Y >= Config.ColumnSize) return false;

        Vector3i l = ChunkMath.GlobalToLocal(globalBlockPosition);
        
        // return Chunks[chunkPosition.Xz].ChunkSections[chunkPosition.Y].GetSolid(l.X, l.Y, l.Z);
        return Chunks[chunkPosition.Xz].GetSolid(l);
    }

    public bool GetBlockIsTransparent(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (!Chunks.ContainsKey(chunkPosition.Xz) || chunkPosition.Y < 0 ||
            chunkPosition.Y >= Config.ColumnSize) return false;

        Vector3i l = ChunkMath.GlobalToLocal(globalBlockPosition);
        // return Chunks[chunkPosition.Xz].ChunkSections[chunkPosition.Y].GetTransparent(l.X, l.Y, l.Z);
        return Chunks[chunkPosition.Xz].GetTransparent(l);
    }

    public void AddLight(Vector3i globalBlockPosition, ushort r, ushort g, ushort b)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);

        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i localPosition = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            if (r != 0)
            {
                chunk.SetRedValue(localPosition, r);
                chunk.RedLightAdditionQueue.Enqueue(globalBlockPosition);
            }
            
            if (g != 0)
            {
                chunk.SetGreenValue(localPosition, g);
                chunk.GreenLightAdditionQueue.Enqueue(globalBlockPosition);
            }
            
            if (b != 0)
            {
                chunk.SetBlueValue(localPosition, b);
                chunk.BlueLightAdditionQueue.Enqueue(globalBlockPosition);
            }
        }
    }

    public void SetBlock(Vector3i globalBlockPosition, Block? block = null)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.ContainsKey(chunkPosition.Xz) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i pos = ChunkMath.GlobalToLocal(globalBlockPosition);
            Chunks[chunkPosition.Xz].SetBlock((pos.X, globalBlockPosition.Y, pos.Z), block);

            Chunks[chunkPosition.Xz].ChunkMeshes[chunkPosition.Y].ShouldUpdate = true;
        }
    }
    
    public string GetBlockId(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (!Chunks.ContainsKey(chunkPosition.Xz) || globalBlockPosition.Y < 0 || globalBlockPosition.Y >= Config.ChunkSize * Config.ColumnSize) return "air";
        
        // return Chunks[chunkPosition.Xz].ChunkSections[chunkPosition.Y].GetBlockId(ChunkMath.GlobalToLocal(globalBlockPosition)) ?? "air";
        return Chunks[chunkPosition.Xz].GetBlockId(ChunkMath.GlobalToLocal(globalBlockPosition));
    }

    public void SetSunlightValue(Vector3i globalBlockPosition, ushort value)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            chunk.SetSunlightValue((position.X, globalBlockPosition.Y, position.Z), value);
        }
    }
    
    public void SetRedLightValue(Vector3i globalBlockPosition, ushort value)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            chunk.SetRedValue((position.X, globalBlockPosition.Y, position.Z), value);
        }
    }
    
    public void SetGreenLightValue(Vector3i globalBlockPosition, ushort value)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            chunk.SetGreenValue((position.X, globalBlockPosition.Y, position.Z), value);
        }
    }
    
    public void SetBlueLightValue(Vector3i globalBlockPosition, ushort value)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            chunk.SetBlueValue((position.X, globalBlockPosition.Y, position.Z), value);
        }
    }

    public ushort SunlightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.SunlightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 15;
    }
    
    public ushort RedLightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.RedLightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 15;
    }
    
    public ushort GreenLightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.GreenLightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 15;
    }
    
    public ushort BlueLightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.BlueLightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 15;
    }

    public float NormalizedSunlightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.NormalizedSunlightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 1.0f;
    }
    
    public float NormalizedRedLightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.NormalizedRedLightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 1.0f;
    }
    
    public float NormalizedGreenLightightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.NormalizedGreenLightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 1.0f;
    }
    
    public float NormalizedBlueLightValueAt(Vector3i globalBlockPosition)
    {
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);
            
            return chunk.NormalizedBlueLightValueAt((position.X, globalBlockPosition.Y, position.Z));
        }

        return 1.0f;
    }
    
    public Vector4 NormalizedFullLightAt(Vector3i globalBlockPosition)
    {
        Vector4 value = Vector4.One;
        
        Vector3i chunkPosition = ChunkMath.GlobalToChunk(globalBlockPosition);
        if (Chunks.TryGetValue(chunkPosition.Xz, out Chunk chunk) && chunkPosition.Y >= 0 && chunkPosition.Y < Config.ColumnSize)
        {
            Vector3i position = ChunkMath.GlobalToLocal(globalBlockPosition);

            value.X = chunk.NormalizedRedLightValueAt(position);
            value.Y = chunk.NormalizedGreenLightValueAt(position);
            value.Z = chunk.NormalizedBlueLightValueAt(position);
            value.W = chunk.NormalizedSunlightValueAt(position);
        }
        
        return value;
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
                // Generator.UpdateChunk(chunk.Position, ChunkStatus.Mesh, true);
                // Generator.GeneratorQueue.Enqueue((chunkPosition.X, chunkPosition.Z, 0));
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
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY)) model.AddFace(vertexData, Direction.Top, localBlockPosition, NormalizedFullLightAt(globalBlockPosition + Vector3i.UnitY));
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY)) model.AddFace(vertexData, Direction.Bottom, localBlockPosition, NormalizedFullLightAt(globalBlockPosition - Vector3i.UnitY));
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX)) model.AddFace(vertexData, Direction.Right, localBlockPosition, NormalizedFullLightAt(globalBlockPosition + Vector3i.UnitX));
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX)) model.AddFace(vertexData, Direction.Left, localBlockPosition, NormalizedFullLightAt(globalBlockPosition - Vector3i.UnitX));
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Back, localBlockPosition, NormalizedFullLightAt(globalBlockPosition + Vector3i.UnitZ));
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Front, localBlockPosition, NormalizedFullLightAt(globalBlockPosition - Vector3i.UnitZ));
        }
        else
        {   
            List<ChunkVertex> vertexData = Chunks[chunkPosition.Xz].ChunkMeshes[chunkPosition.Y].SolidVertices;
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY) || GetBlockIsTransparent(globalBlockPosition + Vector3i.UnitY)) model.AddFace(vertexData, Direction.Top, localBlockPosition, NormalizedFullLightAt(globalBlockPosition + Vector3i.UnitY));
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY) || GetBlockIsTransparent(globalBlockPosition - Vector3i.UnitY)) model.AddFace(vertexData, Direction.Bottom, localBlockPosition, NormalizedFullLightAt(globalBlockPosition - Vector3i.UnitY));
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX) || GetBlockIsTransparent(globalBlockPosition + Vector3i.UnitX)) model.AddFace(vertexData, Direction.Right, localBlockPosition, NormalizedFullLightAt(globalBlockPosition + Vector3i.UnitX));
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX) || GetBlockIsTransparent(globalBlockPosition - Vector3i.UnitX)) model.AddFace(vertexData, Direction.Left, localBlockPosition, NormalizedFullLightAt(globalBlockPosition - Vector3i.UnitX));
            if (!GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ) || GetBlockIsTransparent(globalBlockPosition + Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Back, localBlockPosition, NormalizedFullLightAt(globalBlockPosition + Vector3i.UnitZ));
            if (!GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ) || GetBlockIsTransparent(globalBlockPosition - Vector3i.UnitZ)) model.AddFace(vertexData, Direction.Front, localBlockPosition, NormalizedFullLightAt(globalBlockPosition - Vector3i.UnitZ));
        }
    }
}