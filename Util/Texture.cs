using System;
using System.IO;
using OpenTK.Graphics.OpenGL;
using StbImageSharp;

namespace VoxelGame;

public class Texture
{
    public int Id;
    private string _path;

    public int Width, Height;
    private byte[] _pixel = new byte[4];
    
    public Texture(string path)
    {
        _path = Path.Combine(path);
    }

    public Texture Generate(int mipmapCount = 4)
    {
        Id = GL.GenTexture();

        GL.BindTexture(TextureTarget.Texture2d, Id);
        using (Stream image = File.OpenRead(_path))
        {
            ImageResult imageResult = ImageResult.FromStream(image, ColorComponents.RedGreenBlueAlpha);
            Width = imageResult.Width;
            Height = imageResult.Height;
            GL.TexImage2D(TextureTarget.Texture2d, 0, InternalFormat.Rgba, imageResult.Width, imageResult.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, imageResult.Data);
        }
        
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.NearestMipmapLinear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Nearest);
        
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMaxLevel, mipmapCount);
        GL.GenerateMipmap(TextureTarget.Texture2d);
        
        return this;
    }

    public bool HasZeroAlphaPixel(int x, int y)
    {
        GL.GetTextureSubImage(Id, 0, x, (Height - 1) - y, 0, 1, 1, 1, PixelFormat.Rgba, PixelType.UnsignedByte, 4, _pixel);
        return _pixel[3] == 0;
    }
}