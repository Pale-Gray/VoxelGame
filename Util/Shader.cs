using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using OpenTK.Graphics.OpenGL;

namespace VoxelGame.Util;

public unsafe class Shader
{
    public int Id;
    private string _vertexPath;
    private string _fragmentPath;
    private Dictionary<string, int> _cachedUniforms = new();
    public Shader(string vertexPath, string fragmentPath)
    {
        _fragmentPath = Path.Combine(fragmentPath);
        _vertexPath = Path.Combine(vertexPath);
    }

    public Shader Compile()
    {
        int success = 1;
        
        string vertexData = File.ReadAllText(_vertexPath);
        int vertexShader = GL.CreateShader(ShaderType.VertexShader);
        GL.ShaderSource(vertexShader, vertexData);
        GL.CompileShader(vertexShader);
        
        GL.GetShaderiv(vertexShader, ShaderParameterName.CompileStatus, &success);
        if (success != 1)
        {
            GL.GetShaderInfoLog(vertexShader, out string vertexShaderInfo);
            throw new Exception($"Failed to compile vertex shader:\n{vertexShaderInfo}");
        }

        string fragmentData = File.ReadAllText(_fragmentPath);
        int fragmentShader = GL.CreateShader(ShaderType.FragmentShader);
        GL.ShaderSource(fragmentShader, fragmentData);
        GL.CompileShader(fragmentShader);
        
        GL.GetShaderiv(fragmentShader, ShaderParameterName.CompileStatus, &success);
        if (success != 1)
        {
            GL.GetShaderInfoLog(fragmentShader, out string fragmentShaderInfo);
            throw new Exception($"Failed to compile fragment shader:\n{fragmentShaderInfo}");
        }

        Id = GL.CreateProgram();
        GL.AttachShader(Id, vertexShader);
        GL.AttachShader(Id, fragmentShader);
        GL.LinkProgram(Id);
        
        GL.GetProgramiv(Id, ProgramProperty.LinkStatus, &success);
        if (success != 1)
        {
            GL.GetProgramInfoLog(Id, out string programInfo);
            throw new Exception($"Failed to link program:\n{programInfo}");
        }
        
        GL.DeleteShader(vertexShader);
        GL.DeleteShader(fragmentShader);
        return this;
    }

    public int GetUniformLocation(string uniformName)
    {
        if (_cachedUniforms.TryGetValue(uniformName, out int value)) return value;
        _cachedUniforms.Add(uniformName, GL.GetUniformLocation(Id, uniformName));
        return _cachedUniforms[uniformName];
    }

    public void Bind()
    {
        GL.UseProgram(Id);
    }

    public void Free()
    {
        GL.DeleteProgram(Id);
    }
}