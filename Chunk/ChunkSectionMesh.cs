using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using OpenTK.Mathematics;
using OpenTK.Graphics.OpenGL;

namespace VoxelGame;

public struct ChunkVertex
{
    public Vector3 Position;
    public Vector3 Normal;
    public Vector2 TextureCoordinate;
    public Vector4 Tint;

    public ChunkVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate)
    {
        Position = position;
        Normal = normal;
        TextureCoordinate = textureCoordinate;
    }

    public ChunkVertex(Vector3 position, Vector3 normal, Vector2 textureCoordinate, Vector4 tint) : this(position, normal, textureCoordinate)
    {
        Tint = tint;
    }
}
public class ChunkSectionMesh
{
    public List<ChunkVertex> SolidVertices = new();
    public List<int> SolidIndices = new();
    public int SolidVerticesLength;
    public int SolidIndicesLength;
    public int SolidVbo, SolidVao, SolidIbo;
    public List<ChunkVertex> TransparentVertices = new();
    public List<int> TransparentIndices = new();
    public int TransparentVerticesLength;
    public int TransparentIndicesLength;
    public int TransparentVbo, TransparentVao, TransparentIbo;
    public bool ShouldUpdate = true;
    public Vector3i ChunkPosition;

    public void DrawSolid(Camera camera)
    {
        GL.BindVertexArray(SolidVao);
        GL.DrawElements(PrimitiveType.Triangles, SolidIndicesLength, DrawElementsType.UnsignedInt, 0);
    }

    public void DrawTransparent(Camera camera)
    {
        GL.BindVertexArray(TransparentVao);
        GL.DrawElements(PrimitiveType.Triangles, TransparentIndicesLength, DrawElementsType.UnsignedInt, 0);
    }

    public void Update()
    {
        GL.DeleteBuffer(SolidVbo);
        GL.DeleteBuffer(SolidIbo);
        GL.DeleteVertexArray(SolidVao);

        SolidVao = GL.GenVertexArray();
        GL.BindVertexArray(SolidVao);

        SolidVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, SolidVbo);
        GL.BufferData<ChunkVertex>(BufferTarget.ArrayBuffer, Marshal.SizeOf<ChunkVertex>() * SolidVertices.Count, CollectionsMarshal.AsSpan(SolidVertices), BufferUsage.DynamicDraw);

        SolidIbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, SolidIbo);
        GL.BufferData<int>(BufferTarget.ElementArrayBuffer, sizeof(int) * SolidIndices.Count, CollectionsMarshal.AsSpan(SolidIndices), BufferUsage.DynamicDraw);
        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Position)));
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Normal)));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.TextureCoordinate)));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Tint)));
        GL.EnableVertexAttribArray(3);

        SolidVerticesLength = SolidVertices.Count;
        SolidIndicesLength = SolidIndices.Count;
        
        SolidVertices.Clear();
        SolidIndices.Clear();
        
        GL.DeleteBuffer(TransparentVbo);
        GL.DeleteBuffer(TransparentIbo);
        GL.DeleteVertexArray(TransparentVao);

        TransparentVao = GL.GenVertexArray();
        GL.BindVertexArray(TransparentVao);

        TransparentVbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, TransparentVbo);
        GL.BufferData<ChunkVertex>(BufferTarget.ArrayBuffer, Marshal.SizeOf<ChunkVertex>() * TransparentVertices.Count, CollectionsMarshal.AsSpan(TransparentVertices), BufferUsage.DynamicDraw);

        TransparentIbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, TransparentIbo);
        GL.BufferData<int>(BufferTarget.ElementArrayBuffer, sizeof(int) * TransparentIndices.Count, CollectionsMarshal.AsSpan(TransparentIndices), BufferUsage.DynamicDraw);
        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Position)));
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Normal)));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.TextureCoordinate)));
        GL.EnableVertexAttribArray(2);
        GL.VertexAttribPointer(3, 4, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Tint)));
        GL.EnableVertexAttribArray(3);

        TransparentVerticesLength = TransparentVertices.Count;
        TransparentIndicesLength = TransparentIndices.Count;
        
        TransparentVertices.Clear();
        TransparentIndices.Clear();
    }
}