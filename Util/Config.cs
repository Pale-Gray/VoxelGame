using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using OpenTK.Graphics.Vulkan;
using OpenTK.Platform;
using VoxelGame.Networking;
using VoxelGame.Rendering;
using VoxelGame.Util;

namespace VoxelGame;

public static class Config
{
    public const int ChunkSize = 32;
    public const int ColumnSize = 8;
    public const int Radius = 8;

    public static int Width = 960;
    public static int Height = 540;

    public static bool IsRunning = true;
    
    public static DynamicAtlas Atlas;
    public static Shader ChunkShader;
    public static Shader GbufferShader;
    public static DeferredFramebuffer Gbuffer;
    public static Random Random = new Random();
    public static Process CurrentProcess = Process.GetCurrentProcess();

    public static Server? Server;
    public static Client? Client;

    public static WindowHandle Window;
    public static OpenGLContextHandle OpenGLContext;

    public static int LastTicksPerSecond = 0;
    public static int TickSpeed = 30;
    public static float TickRate = 1.0f / TickSpeed;
    public static float DeltaTime;
    public static float ElapsedTime = 0;
    public static float TickInterpolant => (ElapsedTime % TickRate) / TickRate;
    public static Stopwatch Timer = new Stopwatch();
    public static TimeSpan LastGenTime;
    public static TimeSpan LastMeshTime;
    public static ConcurrentBag<TimeSpan> GenTimes = new();
    public static ConcurrentBag<TimeSpan> MeshTimes = new();

    public static List<float> FrameTimesOfLastSecond = new();
    public static float MinimumFps;
    public static float MaximumFps;
    public static float AverageFps;

    public static World World;
    public static Stopwatch StartTime;

    // public static int Seed = 2018766363;
    // public static int Seed = Random.Next();
    public static int Seed = 1639924240;
}