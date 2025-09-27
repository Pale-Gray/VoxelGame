using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Runtime.InteropServices;
using System.Text;
using OpenTK.Graphics.OpenGLES2;
using OpenTK.Mathematics;
using OpenTK.Platform;

namespace VoxelGame.Util;

public struct GuiRectangle()
{
    public Vector3 Position = Vector3.Zero;
    public Vector2 Size = Vector2.One;
    public Vector2 TextureCoordinatePosition = Vector2.Zero;
    public Vector2 TextureCoordinateSize = Vector2.One;
    public Vector3 Color = Vector3.One;
}

public struct GuiVertex
{
    public Vector3 Position;
    public Vector2 TextureCoordinate;
    public Vector3 Color;

    public GuiVertex(Vector3 position, Vector2 textureCoordinate, Vector3 color)
    {
        Position = position;
        Color = color;
        TextureCoordinate = textureCoordinate;
    }
}

public static class Gui
{
    private static Shader _guiShader;
    private static Texture _fontTexture;
    private static Texture _buttonTexture;

    private static List<GuiRectangle> _rectangles = new();
    private static List<GuiVertex> _vertices = new();
    private static List<int> _indices = new();
    private static int Vbo, Vao, Ibo;
    private static Texture _activeTexture = _fontTexture;
    public static float PixelsPerUnit = 2.0f;

    private static Vector2 _editableContainerSize = Vector2.Zero;

    private static Vector2 _currentContainerSize => _editableContainerSize == Vector2.Zero ? (Config.Width, Config.Height) : _editableContainerSize;
    private static Vector2 _currentContainerPosition = Vector2.Zero;
    
    public static void Init()
    {
        _guiShader = new Shader("resources/shaders/gui.vert", "resources/shaders/gui.frag").Compile();
        _fontTexture = new Texture("resources/fonts/default.png").Generate(0);
        _buttonTexture = new Texture("resources/textures/ui/button.png").Generate();
    }

    public static Vector2 AsPixelPerfect(Vector2 coordinates) => coordinates * PixelsPerUnit;
    public static float AsPixelPerfect(float coordinate) => coordinate * PixelsPerUnit;
    public static Vector2 ToAbsolute(Vector2 relative) => relative * _currentContainerSize;

    public static void EnterContainer(Vector2 position, Vector2 size)
    {
        _currentContainerPosition = position;
        _editableContainerSize = size;
    }

    public static void ResetContainer()
    {
        _currentContainerPosition = Vector2.Zero;
        _editableContainerSize = Vector2.Zero;
    }

    public static void DrawRectangles()
    {
        foreach (GuiRectangle rectangle in _rectangles)
        {
            Vector2 top = (0, 1);
            
            _vertices.AddRange
            (
                new GuiVertex(rectangle.Position + (0, rectangle.Size.Y, 0), (rectangle.TextureCoordinatePosition.X, rectangle.TextureCoordinatePosition.Y + rectangle.TextureCoordinateSize.Y), rectangle.Color),
                new GuiVertex(rectangle.Position, rectangle.TextureCoordinatePosition, rectangle.Color),
                new GuiVertex(rectangle.Position + (rectangle.Size.X, 0, 0), (rectangle.TextureCoordinatePosition.X + rectangle.TextureCoordinateSize.X, rectangle.TextureCoordinatePosition.Y), rectangle.Color),
                new GuiVertex(rectangle.Position + (rectangle.Size.X, rectangle.Size.Y, 0), rectangle.TextureCoordinatePosition + rectangle.TextureCoordinateSize, rectangle.Color)
            );
        }

        for (int i = 0; i < _rectangles.Count; i++)
        {
            int idx = i * 4;
            
            _indices.AddRange
            (
                0 + idx, 1 + idx, 2 + idx,
                2 + idx, 3 + idx, 0 + idx
            );
        }

        for (int i = 0; i < _vertices.Count; i++)
        {
            GuiVertex vertex = _vertices[i];
            vertex.Position += (_currentContainerPosition.X, _currentContainerPosition.Y, 0);
            _vertices[i] = vertex;
        }
        
        Vao = GL.GenVertexArray();
        GL.BindVertexArray(Vao);

        Vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, Vbo);
        GL.BufferData<GuiVertex>(BufferTarget.ArrayBuffer, Marshal.SizeOf<GuiVertex>() * _vertices.Count, CollectionsMarshal.AsSpan(_vertices), BufferUsage.StaticDraw);

        Ibo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, Ibo);
        GL.BufferData<int>(BufferTarget.ElementArrayBuffer, sizeof(uint) * _indices.Count, CollectionsMarshal.AsSpan(_indices), BufferUsage.StaticDraw);
        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<GuiVertex>(), Marshal.OffsetOf<GuiVertex>(nameof(GuiVertex.Position)));
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 2, VertexAttribPointerType.Float, false, Marshal.SizeOf<GuiVertex>(), Marshal.OffsetOf<GuiVertex>(nameof(GuiVertex.TextureCoordinate)));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<GuiVertex>(), Marshal.OffsetOf<GuiVertex>(nameof(GuiVertex.Color)));
        GL.EnableVertexAttribArray(2);

        GL.Disable(EnableCap.DepthTest);
        // GL.Disable(EnableCap.CullFace);
        _guiShader.Bind();
        Matrix4 projection = Matrix4.CreateOrthographicOffCenter(0, Config.Width, 0, Config.Height, 0.0f, 100.0f);
        GL.UniformMatrix4f(_guiShader.GetUniformLocation("uProjection"), 1, true, in projection);
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2d, _activeTexture.Id);
        GL.Uniform1i(_guiShader.GetUniformLocation("uTexture"), 0);
        GL.DrawElements(PrimitiveType.Triangles, _indices.Count, DrawElementsType.UnsignedInt, 0);
        GL.Enable(EnableCap.DepthTest);
        
        GL.DeleteVertexArray(Vao);
        GL.DeleteBuffer(Vbo);
        GL.DeleteBuffer(Ibo);
        _vertices.Clear();
        _indices.Clear();
        _rectangles.Clear();
    }

    private static Vector2 _glyphTextureSize = (8.0f / 512.0f, 16.0f / 256.0f);
    private static Vector2 _glyphSize = (8.0f, 16.0f);
    
    public static void Text(string text, Vector2 position, float size, Color3<Rgb> color, bool isCentered = false)
    {
        _activeTexture = _fontTexture;
        
        Vector3 linePosition = Vector3.Zero;
        Vector2 textureCoordinateIndex = Vector2.Zero;
        Vector2 advance = (size, (size / _glyphSize.X) * _glyphSize.Y);
        Vector2 dimensions = Vector2.Zero;
        Vector2 off = (0.0f, 0.0f);
        
        foreach (char character in text)
        {
            Vector2 glyphSize = GlyphBounds(character);
            
            if (character == '\n')
            {
                linePosition.X = 0;
                linePosition.Y -= advance.Y;
                continue;
            }

            if (character == '\t')
            {
                linePosition.X += advance.X * 4.0f;
                continue;
            }

            if (character == ' ')
            {
                linePosition.X += glyphSize.X * (size / 8.0f) + AsPixelPerfect(1.0f);
                continue;
            }
            
            textureCoordinateIndex = GlyphIndexToCoordinates(character);

            GuiRectangle glyphRectangle = new GuiRectangle();
            glyphRectangle.Position = (position.X, position.Y, 0) + (linePosition);
            glyphRectangle.Size = glyphSize * (size / 8.0f);
            glyphRectangle.TextureCoordinatePosition = (0 + (_glyphTextureSize * textureCoordinateIndex).X, 1.0f - _glyphTextureSize.Y - (_glyphTextureSize * textureCoordinateIndex).Y);
            glyphRectangle.TextureCoordinateSize = (glyphSize / _glyphSize) * _glyphTextureSize;
            glyphRectangle.Color = (Vector3) color;
                
            _rectangles.Add(glyphRectangle);
            linePosition.X += glyphSize.X * (size / 8.0f) + AsPixelPerfect(1.0f);
            dimensions.X = float.Max(dimensions.X, linePosition.X);
            dimensions.Y = float.Max(dimensions.Y, linePosition.Y + advance.Y);
        }

        if (isCentered)
        {
            for (int i = 0; i < _rectangles.Count; i++)
            {
                GuiRectangle rect = _rectangles[i];
                rect.Position -= new Vector3(dimensions.X, dimensions.Y, 0.0f) * 0.5f;
                _rectangles[i] = rect;
            }
        }
        DrawRectangles();
    }
    
    public static Vector2 GlyphIndexToCoordinates(int index)
    {
        return (index % 64, (int) (index / 64.0f));
    }

    private static Vector2 GlyphBounds(char glyph)
    {
        Vector2i glyphIndex = (Vector2i) GlyphIndexToCoordinates(glyph) * (8, 16);
        int width = 0;
        for (int x = 0; x < 8; x++)
        {
            bool hasSolidPixel = false;
            for (int y = 0; y < 16; y++)
            {
                if (!_fontTexture.HasZeroAlphaPixel(x + glyphIndex.X, y + glyphIndex.Y))
                {
                    hasSolidPixel = true;
                    break;
                }
            }

            if (hasSolidPixel)
            {
                width++;
            }
            else
            {
                break;
            }
        }
        
        return (width, 16.0f);
    }

    public static bool Button(string text, Vector2 position, Vector2 size, Vector2 borderSize)
    {
        Vector2 textureSize = new Vector2(1.0f / 8.0f);
        Vector2 textureOffset = new Vector2(0.0f, 0.0f);
        
        _activeTexture = _buttonTexture;
        
        GuiRectangle buttonRec = new GuiRectangle();
        buttonRec.Position = (position.X, position.Y, 0);
        buttonRec.Size = size;
        bool intersected = false;
        if (DoesIntersectRectangle(buttonRec))
        {
            intersected = true;
            if (Input.IsMouseButtonDown(MouseButton.Button1))
            {
                textureOffset.Y += 0.5f;
            }
            else
            {
                textureOffset.X += 0.5f;
            }
        }
        
        buttonRec.TextureCoordinatePosition = textureOffset + (0.125f, 0.125f);
        buttonRec.TextureCoordinateSize = textureSize;
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X - borderSize.X, position.Y - borderSize.Y, 0);
        buttonRec.Size = borderSize;
        buttonRec.TextureCoordinatePosition = textureOffset + (0.0f, 0.0f);
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X, position.Y - borderSize.Y, 0);
        buttonRec.Size = (size.X, borderSize.Y);
        buttonRec.TextureCoordinatePosition = textureOffset + (0.125f, 0.0f);
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X + size.X, position.Y - borderSize.Y, 0);
        buttonRec.Size = borderSize;
        buttonRec.TextureCoordinatePosition = textureOffset + (0.25f, 0.0f);
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X - borderSize.X, position.Y, 0);
        buttonRec.Size = (borderSize.Y, size.Y);
        buttonRec.TextureCoordinatePosition = textureOffset + (0.0f, 0.125f);
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X - borderSize.X, position.Y + size.Y, 0);
        buttonRec.Size = borderSize;
        buttonRec.TextureCoordinatePosition = textureOffset + (0.0f, 0.25f);
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X, position.Y + size.Y, 0);
        buttonRec.Size = (size.X, borderSize.Y);
        buttonRec.TextureCoordinatePosition = textureOffset + (0.125f, 0.25f);
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X + size.X, position.Y + size.Y, 0);
        buttonRec.Size = borderSize;
        buttonRec.TextureCoordinatePosition = textureOffset + (0.25f, 0.25f);
        _rectangles.Add(buttonRec);
        
        buttonRec.Position = (position.X + size.X, position.Y, 0);
        buttonRec.Size = (borderSize.X, size.Y);
        buttonRec.TextureCoordinatePosition = textureOffset + (0.25f, 0.125f);
        _rectangles.Add(buttonRec);

        DrawRectangles();
        
        EnterContainer(position, size);
        Text(text, ToAbsolute((0.5f, 0.5f)), AsPixelPerfect(8.0f), Color3.Black, true);
        ResetContainer();
        
        if (intersected)
        {
            return Input.IsMouseButtonReleased(MouseButton.Button1);
        }
        return false;
    }

    private static bool DoesIntersectRectangles()
    {
        foreach (GuiRectangle rectangle in _rectangles)
        {
            if (DoesIntersectRectangle(rectangle)) return true;
        }
        return false;
    }
    
    private static bool DoesIntersectRectangle(GuiRectangle rectangle)
    {
        Vector2 mousePosition = Input.MousePosition;
        mousePosition.Y = Config.Height - mousePosition.Y;
        
        return mousePosition.X > rectangle.Position.X && mousePosition.X < rectangle.Position.X + rectangle.Size.X && mousePosition.Y > rectangle.Position.Y && mousePosition.Y < rectangle.Position.Y + rectangle.Size.Y;
    }
}