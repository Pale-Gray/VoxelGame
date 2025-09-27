using System;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using LiteNetLib;
using OpenTK.Graphics;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Platform;
using StbImageSharp;
using VoxelGame.Rendering;
using VoxelGame.Util;

namespace VoxelGame.Networking;

public class Client : Networked
{
    public NetPeer ClientPeer;
    
    ChunkVertex[] vertices = new ChunkVertex[6];
    private int vbo, vao = 0;
    private Shader shad;
    public bool IsGravityEnabled = false;

    private Player _player = new Player("asdfkj");
    public Client() : base()
    {}

    public override void Start()
    {
        StbImage.stbi_set_flip_vertically_on_load(1);
        
        ToolkitOptions options = new ToolkitOptions();
        options.ApplicationName = "Voxel Game";
        options.Logger = null;
        
        Toolkit.Init(options);

        OpenGLGraphicsApiHints contextSettings = new OpenGLGraphicsApiHints();
        contextSettings.Version = new Version(4, 1);
        contextSettings.Profile = OpenGLProfile.Core;
        contextSettings.DebugFlag = true;
        contextSettings.DepthBits = ContextDepthBits.Depth24;
        contextSettings.StencilBits = ContextStencilBits.Stencil8;
        
        Config.Window = Toolkit.Window.Create(contextSettings);
        Config.OpenGLContext = Toolkit.OpenGL.CreateFromWindow(Config.Window);

        DisplayHandle display = Toolkit.Display.OpenPrimary();
        Toolkit.Display.GetResolution(display, out Config.Width, out Config.Height);
        Config.Width /= 2;
        Config.Height /= 2;
        
        Toolkit.Window.SetTitle(Config.Window, "Voxel Game");
        Toolkit.Window.SetSize(Config.Window, (Config.Width, Config.Height));
        Toolkit.Window.SetMode(Config.Window, WindowMode.Normal);
        Toolkit.Window.SetCursor(Config.Window, null);
        Toolkit.Window.SetCursorCaptureMode(Config.Window, CursorCaptureMode.Locked);
        Toolkit.Window.SetTransparencyMode(Config.Window, WindowTransparencyMode.TransparentFramebuffer);
        
        EventQueue.EventRaised += EventRaised;

        Toolkit.OpenGL.SetCurrentContext(Config.OpenGLContext);
        GLLoader.LoadBindings(Toolkit.OpenGL.GetBindingsContext(Config.OpenGLContext));
        
        Config.Atlas = new DynamicAtlas("resources/textures/blocks").Generate();
        BaseGame.OnLoad();

        Config.Gbuffer = new DeferredFramebuffer();
        Config.Gbuffer.Create();
        GL.Viewport(0, 0, Config.Width, Config.Height);
        GL.Enable(EnableCap.DepthTest);
        // GL.DepthFunc(DepthFunction.Less);
        GL.Enable(EnableCap.CullFace);
        GL.CullFace(TriangleFace.Back);
        GL.FrontFace(FrontFaceDirection.Ccw);
        GL.Enable(EnableCap.Blend);
        GL.BlendFunc(BlendingFactor.SrcAlpha, BlendingFactor.OneMinusSrcAlpha);
        GL.Enable(EnableCap.PolygonOffsetFill);
        GL.Enable(EnableCap.PolygonOffsetLine);
        // Console.WriteLine($"Max image texture size: {GL.GetInteger(GetPName.MaxTextureSize)}");
        
        Config.ChunkShader = new Shader("resources/shaders/shad.vert", "resources/shaders/shad.frag").Compile();
        Config.GbufferShader = new Shader("resources/shaders/framebuffer.vert", "resources/shaders/framebuffer.frag").Compile();
        shad = new Shader("resources/shaders/vshad.vert", "resources/shaders/vshad.frag").Compile();

        Input.Init();
        
        vertices = new ChunkVertex[]
        {
            // top
            new ChunkVertex((0, 1, 1), (0, 1, 0), (0, 0)),
            new ChunkVertex((0, 1, 0), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((0, 1, 0), (0, 1, 0), (0, 0)),
            new ChunkVertex((1, 1, 0), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((1, 1, 0), (0, 1, 0), (0, 0)),
            new ChunkVertex((1, 1, 1), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((1, 1, 1), (0, 1, 0), (0, 0)),
            new ChunkVertex((0, 1, 1), (0, 1, 0), (0, 0)),
            
            // sides
            new ChunkVertex((0, 1, 1), (0, 1, 0), (0, 0)),
            new ChunkVertex((0, 0, 1), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((0, 1, 0), (0, 1, 0), (0, 0)),
            new ChunkVertex((0, 0, 0), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((1, 1, 0), (0, 1, 0), (0, 0)),
            new ChunkVertex((1, 0, 0), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((1, 1, 1), (0, 1, 0), (0, 0)),
            new ChunkVertex((1, 0, 1), (0, 1, 0), (0, 0)),
            
            // bottom
            new ChunkVertex((0, 0, 1), (0, 1, 0), (0, 0)),
            new ChunkVertex((0, 0, 0), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((0, 0, 0), (0, 1, 0), (0, 0)),
            new ChunkVertex((1, 0, 0), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((1, 0, 0), (0, 1, 0), (0, 0)),
            new ChunkVertex((1, 0, 1), (0, 1, 0), (0, 0)),
            
            new ChunkVertex((1, 0, 1), (0, 1, 0), (0, 0)),
            new ChunkVertex((0, 0, 1), (0, 1, 0), (0, 0)),
        };

        vao = GL.GenVertexArray();
        GL.BindVertexArray(vao);

        vbo = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, vbo);
        GL.BufferData(BufferTarget.ArrayBuffer, Marshal.SizeOf<ChunkVertex>() * vertices.Length, vertices, BufferUsage.StaticDraw);
        
        GL.VertexAttribPointer(0, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Position)));
        GL.EnableVertexAttribArray(0);
        GL.VertexAttribPointer(1, 3, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.Normal)));
        GL.EnableVertexAttribArray(1);
        GL.VertexAttribPointer(2, 2, VertexAttribPointerType.Float, false, Marshal.SizeOf<ChunkVertex>(), Marshal.OffsetOf<ChunkVertex>(nameof(ChunkVertex.TextureCoordinate)));
        GL.EnableVertexAttribArray(2);
        
        Gui.Init();
    }

    public override void Stop()
    {
        Config.World?.Generator.Stop();
    }

    public override void TickUpdate()
    {
        _player.TickUpdate(Config.World);
        PlayerMovePacket packet = new PlayerMovePacket();
        packet.Position = _player.Position;
        SendPacket(packet);
    }

    public override void Join(bool isInternal = false)
    {
        Manager.Start();
        ClientPeer = Manager.Connect(HostOrIp, Port, "hello");

        if (!isInternal)
        {
            Config.World = new World();
            Config.World.Generator.ShouldMesh = true;
            Config.World.Generator.Start();
        }

        Listener.NetworkReceiveEvent += (fromPeer, dataReader, channel, deliveryMethod) =>
        {
            DataReader reader = new DataReader(dataReader.GetRemainingBytes());
            PacketType type = (PacketType)reader.ReadInt32();
            switch (type)
            {
                case PacketType.ChunkData:
                    ChunkDataPacket chunkData = (ChunkDataPacket) new ChunkDataPacket().Deserialize(reader);
                    chunkData.Column.Status = ChunkStatus.Mesh;
                    if (Config.World.Chunks.TryAdd(chunkData.Position, chunkData.Column))
                    {
                        Config.World.Chunks[chunkData.Position].Status = ChunkStatus.Mesh;
                        // Config.World.Generator.GenerationQueue.Enqueue(chunkData.Position);
                    }
                    break;
                case PacketType.BlockDestroy:
                    BlockDestroyPacket blockDestroy = (BlockDestroyPacket)new BlockDestroyPacket().Deserialize(reader);
                    Register.GetBlockFromId(blockDestroy.Id).OnBlockDestroy(Config.World, blockDestroy.GlobalBlockPosition);
                    break;
                case PacketType.BlockPlace:
                    BlockPlacePacket blockPlace = (BlockPlacePacket)new BlockPlacePacket().Deserialize(reader);
                    Register.GetBlockFromId(blockPlace.Id).OnBlockPlace(Config.World, blockPlace.GlobalBlockPosition);
                    break;
            }
            
            dataReader.Recycle();
        };

        Listener.PeerConnectedEvent += peer =>
        {
            PlayerJoinPacket packet = new PlayerJoinPacket();
            packet.Id = Guid.NewGuid();
            SendPacket(packet);
        };

        Listener.PeerDisconnectedEvent += (peer, info) =>
        {
            Console.WriteLine($"Connection disconnected. Reason: {info.Reason}");
        };
    }

    public override void Update()
    {
        Manager.PollEvents();
        Input.Poll();
        Toolkit.Window.ProcessEvents(false);
        // if (Input.MouseDelta != Vector2.Zero) Console.WriteLine(Input.MouseDelta);
        
        GL.ClearColor(Color4.Cornflowerblue);
        GL.Clear(ClearBufferMask.ColorBufferBit | ClearBufferMask.DepthBufferBit | ClearBufferMask.StencilBufferBit);

        if (Toolkit.Window.IsWindowDestroyed(Config.Window))
        {
            Config.IsRunning = false;
            return;
        }

        if (Input.IsKeyPressed(Key.Escape))
        {
            if (Toolkit.Window.GetCursorCaptureMode(Config.Window) == CursorCaptureMode.Locked)
            {
                Toolkit.Window.SetCursor(Config.Window, Toolkit.Cursor.Create(SystemCursorType.Default));
                Toolkit.Window.SetCursorCaptureMode(Config.Window, CursorCaptureMode.Normal);   
            }
            else
            {
                Toolkit.Window.SetCursor(Config.Window, null);
                Toolkit.Window.SetCursorCaptureMode(Config.Window, CursorCaptureMode.Locked);
            }
        }

        if (Input.IsKeyPressed(Key.R))
        {
            // Config.Server.ConnectedPlayers.First().Value.VisitedChunks.Clear();
            // Config.Server.ConnectedPlayers.First().Value.LoadQueue.Enqueue(Config.Server.ConnectedPlayers.First().Value.ChunkPosition.Value);
            foreach (Vector2i position in Config.World.Chunks.Keys)
            {
                Chunk chunk = Config.World.Chunks[position];
                chunk.ElapsedTime = 0.0f;
                for (int i = 0; i < Config.ColumnSize; i++)
                {
                    ChunkSectionMesh mesh = chunk.ChunkMeshes[i];
                    mesh.SolidIndicesLength = 0;
                    mesh.SolidVerticesLength = 0;
                    mesh.SolidIndices.Clear();
                    mesh.SolidVertices.Clear();
                    
                    mesh.TransparentIndicesLength = 0;
                    mesh.TransparentVerticesLength = 0;
                    mesh.TransparentIndices.Clear();
                    mesh.TransparentVertices.Clear();
                    mesh.ShouldUpdate = true;
                }

                chunk.Status = ChunkStatus.Mesh;

                // Config.World.Generator.EnqueueChunk(position, ChunkStatus.Mesh, false);
            }
        }

        if (Input.IsKeyPressed(Key.Keypad1)) Gui.PixelsPerUnit--;
        if (Input.IsKeyPressed(Key.Keypad2)) Gui.PixelsPerUnit++;
            
        if (Input.IsKeyPressed(Key.F))
        {
            if (Toolkit.Window.GetMode(Config.Window) != WindowMode.WindowedFullscreen)
            {
                Toolkit.Window.SetMode(Config.Window, WindowMode.WindowedFullscreen);
            }
            else
            {
                Toolkit.Window.SetMode(Config.Window, WindowMode.Normal);
            }
        }

        if (Input.IsKeyPressed(Key.V))
        {
            _player.IsGravityEnabled = !_player.IsGravityEnabled;
        }
        
        _player.Update();
        Config.World.Draw(_player.Camera);
        Config.World.Generator.Poll();

        Ray ray = new Ray();
        ray.Origin = _player.Camera.Position;
        ray.Direction = (Matrix3.CreateRotationY(float.DegreesToRadians(_player.Camera.Rotation.Y)) * Matrix3.CreateRotationX(float.DegreesToRadians(-_player.Camera.Rotation.X))).Column2 * (-1, 1, 1);
        
        if (ray.TryHit(Config.World, 10))
        {
            // GL.Disable(EnableCap.CullFace);
            // shad.Bind();
            // GL.Uniform3f(shad.GetUniformLocation("uPosition"), 1, ray.HitBlockPosition);
            // GL.UniformMatrix4f(shad.GetUniformLocation("uProjection"), 1, true, ref _player.Camera.Projection);
            // GL.UniformMatrix4f(shad.GetUniformLocation("uView"), 1, true, ref _player.Camera.View);
            // GL.BindVertexArray(vao);
            // 
            // GL.Disable(EnableCap.DepthTest);
            // GL.DrawArrays(PrimitiveType.Lines, 0, vertices.Length);
            // 
            // GL.Enable(EnableCap.DepthTest);
            // GL.Enable(EnableCap.CullFace);

            if (Input.IsMouseFocused)
            {
                if (Input.IsMouseButtonPressed(MouseButton.Button2))
                {
                    Register.GetBlockFromId("sand").OnBlockPlace(Config.World, ray.PreviousHitBlockPosition);
                    BlockPlacePacket packet = new BlockPlacePacket();
                    packet.Id = "sand";
                    packet.GlobalBlockPosition = ray.PreviousHitBlockPosition;
                
                    SendPacket(packet);
                }
            
                if (Input.IsMouseButtonPressed(MouseButton.Button1))
                {
                    Register.GetBlockFromId(Config.World.GetBlockId(ray.HitBlockPosition)).OnBlockDestroy(Config.World, ray.HitBlockPosition);
                    BlockDestroyPacket packet = new BlockDestroyPacket();
                    packet.GlobalBlockPosition = ray.HitBlockPosition;
                    packet.Id = Config.World.GetBlockId(ray.HitBlockPosition);
                
                    SendPacket(packet);
                }
            }
        }
        
        double lastGenTime = double.Round(Config.LastGenTime.TotalMilliseconds, 2);
        double minGenTime = 0.0;
        double maxGenTime = 0.0;
        double avgGenTime = 0.0;
        if (Config.GenTimes.Count > 0)
        {
            minGenTime = double.Round(Config.GenTimes.Min().TotalMilliseconds, 2);
            maxGenTime = double.Round(Config.GenTimes.Max().TotalMilliseconds, 2);
            avgGenTime = double.Round(Config.GenTimes.Average(val => val.TotalMilliseconds), 2);
        }
        double lastMeshTime = double.Round(Config.LastMeshTime.TotalMilliseconds, 2);
        double minMeshTime = 0.0;
        double maxMeshTime = 0.0;
        double avgMeshTime = 0.0;
        if (Config.MeshTimes.Count > 0)
        {
            minMeshTime = double.Round(Config.MeshTimes.Min().TotalMilliseconds, 2);
            maxMeshTime = double.Round(Config.MeshTimes.Max().TotalMilliseconds, 2);
            avgMeshTime = double.Round(Config.MeshTimes.Average(val => val.TotalMilliseconds), 2);
        }
        Gui.Text($"""
                  Memory usage: {GC.GetTotalMemory(true) / 1024 / 1024}MB
                  Heap size: {GC.GetGCMemoryInfo().HeapSizeBytes / 1024 / 1024}MB
                  Last gen time: {lastGenTime}ms, Max gen time: {maxGenTime}ms, Min gen time: {minGenTime}ms, Avg gen time: {avgGenTime}ms
                  Last mesh time: {lastMeshTime}ms, Max mesh time: {maxMeshTime}ms, Min mesh time: {minMeshTime}ms, Avg mesh time: {avgMeshTime}ms
                  Position: {ChunkMath.PositionToBlockPosition(_player.Position)}
                  Seed: {Config.Seed}
                  """, Gui.ToAbsolute(Vector2.UnitY), Gui.AsPixelPerfect(8.0f), Color3.White);
        if (Gui.Button("I am a button", Gui.AsPixelPerfect((100, 100)), Gui.AsPixelPerfect((80 + float.Floor(float.Abs(float.Sin(Config.ElapsedTime) * 10)), 20 + float.Floor(float.Abs(float.Cos(Config.ElapsedTime) * 20)))), Gui.AsPixelPerfect((4, 4)))) Environment.Exit(0);
        Toolkit.OpenGL.SwapBuffers(Config.OpenGLContext);
        Toolkit.Window.SetTitle(Config.Window, $"position: {ChunkMath.PositionToBlockPosition(_player.Position)} size: ({Config.Width}, {Config.Height}), avg fps: {Config.AverageFps}, min fps: {Config.MinimumFps}, max fps: {Config.MaximumFps}");
    }

    public override void Disconnect()
    {
        Manager.Stop();
    }

    void EventRaised(PalHandle? handle, PlatformEventType eventType, EventArgs args)
    {
        if (args is CloseEventArgs closeEvent)
        {
            Toolkit.Window.Destroy(closeEvent.Window);
            Config.IsRunning = false;
        }

        if (args is MouseMoveEventArgs mouseMove)
        {
            Input.OnMouseMove(mouseMove.ClientPosition);
        }

        if (args is MouseButtonDownEventArgs mouseButtonDown)
        {
            Input.OnMouseButtonDown(mouseButtonDown.Button);
        }

        if (args is MouseButtonUpEventArgs mouseButtonUp)
        {
            Input.OnMouseButtonUp(mouseButtonUp.Button);
        }

        if (args is KeyDownEventArgs keyDownEvent)
        {
            Input.OnKeyDown(keyDownEvent.Key);
        }

        if (args is KeyUpEventArgs keyUpEvent)
        {
            Input.OnKeyUp(keyUpEvent.Key);
        }

        if (args is WindowFramebufferResizeEventArgs windowFramebufferResize)
        {
            Config.Width = windowFramebufferResize.NewFramebufferSize.X;
            Config.Height = windowFramebufferResize.NewFramebufferSize.Y;

            _player.Camera.Width = Config.Width;
            _player.Camera.Height = Config.Height;
            
            GL.Viewport(0, 0, Config.Width, Config.Height);
            Config.Gbuffer.Resize();
        }
    }
}