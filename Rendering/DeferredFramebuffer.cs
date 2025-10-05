using System;
using OpenTK.Graphics.OpenGL;
using OpenTK.Graphics.Vulkan;

namespace VoxelGame.Rendering;

public class DeferredFramebuffer
{
    private int _framebuffer;
    private int _depthTexture;
    private int _albedoTexture;
    private int _lightTexture;
    private int _normalTexture;
    private float[] _vertices =
    {
        0.0f, 1.0f,
        0.0f, 0.0f,
        1.0f, 0.0f,
        1.0f, 0.0f,
        1.0f, 1.0f,
        0.0f, 1.0f
    };

    public void Create()
    {
        _framebuffer = GL.GenFramebuffer();
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);

        _albedoTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, _albedoTexture);
        GL.TexStorage2D(TextureTarget.Texture2d, 1, SizedInternalFormat.Rgba8, Config.Width, Config.Height);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.ObjectLabel(ObjectIdentifier.Texture, (uint)_albedoTexture, -1, "albedo");

        _lightTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, _lightTexture);
        GL.TexStorage2D(TextureTarget.Texture2d, 1, SizedInternalFormat.Rgba8, Config.Width, Config.Height);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.ObjectLabel(ObjectIdentifier.Texture, (uint)_lightTexture, -1, "light");
        
        _normalTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, _normalTexture);
        GL.TexStorage2D(TextureTarget.Texture2d, 1, SizedInternalFormat.Rgba16f, Config.Width, Config.Height);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.ObjectLabel(ObjectIdentifier.Texture, (uint)_normalTexture, -1, "normal");
        
        _depthTexture = GL.GenTexture();
        GL.BindTexture(TextureTarget.Texture2d, _depthTexture);
        GL.TexStorage2D(TextureTarget.Texture2d, 1, SizedInternalFormat.Depth24Stencil8, Config.Width, Config.Height);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMinFilter, (int) TextureMinFilter.Linear);
        GL.TexParameteri(TextureTarget.Texture2d, TextureParameterName.TextureMagFilter, (int) TextureMagFilter.Linear);
        GL.ObjectLabel(ObjectIdentifier.Texture, (uint)_depthTexture, -1, "depth");
        
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment0, TextureTarget.Texture2d, _albedoTexture, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment1, TextureTarget.Texture2d, _normalTexture, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.ColorAttachment2, TextureTarget.Texture2d, _lightTexture, 0);
        GL.FramebufferTexture2D(FramebufferTarget.Framebuffer, FramebufferAttachment.DepthStencilAttachment, TextureTarget.Texture2d, _depthTexture, 0);

        GL.DrawBuffers(3, [DrawBufferMode.ColorAttachment0, DrawBufferMode.ColorAttachment1, DrawBufferMode.ColorAttachment2]);
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Bind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, _framebuffer);
    }

    public void BindTextures()
    {
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2d, _albedoTexture);
        GL.ActiveTexture(TextureUnit.Texture1);
        GL.BindTexture(TextureTarget.Texture2d, _normalTexture);
        GL.ActiveTexture(TextureUnit.Texture2);
        GL.BindTexture(TextureTarget.Texture2d, _depthTexture);
        GL.ActiveTexture(TextureUnit.Texture3);
        GL.BindTexture(TextureTarget.Texture2d, _lightTexture);
    }

    public void Unbind()
    {
        GL.BindFramebuffer(FramebufferTarget.Framebuffer, 0);
    }

    public void Draw(float opacity = 1.0f)
    {
        int vao, vbo;

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, _vertices.Length * sizeof(float), _vertices, BufferUsage.StaticDraw);

        GL.VertexAttribPointer(0, 2, VertexAttribPointerType.Float, false, 2 * sizeof(float), 0);
        GL.EnableVertexAttribArray(0);
        
        Config.GbufferShader.Bind();
        BindTextures();
        GL.Disable(EnableCap.DepthTest);
        GL.Enable(EnableCap.Blend);
        GL.Uniform1i(Config.GbufferShader.GetUniformLocation("uAlbedo"), 0);
        GL.Uniform1i(Config.GbufferShader.GetUniformLocation("uNormal"), 1);
        GL.Uniform1i(Config.GbufferShader.GetUniformLocation("uDepthStencil"), 2);
        GL.Uniform1i(Config.GbufferShader.GetUniformLocation("uLight"), 3);
        GL.Uniform1f(Config.GbufferShader.GetUniformLocation("uTime"), Config.ElapsedTime);
        GL.Uniform1f(Config.GbufferShader.GetUniformLocation("uOpacity"), opacity);
        GL.DrawArrays(PrimitiveType.Triangles, 0, _vertices.Length);
        GL.Enable(EnableCap.DepthTest);
        GL.Disable(EnableCap.Blend);
        
        GL.DeleteBuffer(vbo);
        GL.DeleteVertexArray(vao);
    }

    public void Destroy()
    {
        GL.DeleteTexture(_albedoTexture);
        GL.DeleteTexture(_depthTexture);
        GL.DeleteTexture(_normalTexture);
        GL.DeleteFramebuffer(_framebuffer);
    }

    public void Resize()
    {
        Destroy();
        Create();
    }
}