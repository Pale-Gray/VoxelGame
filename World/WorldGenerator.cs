using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Microsoft.Win32.SafeHandles;
using OpenTK.Mathematics;
using VoxelGame.Util;

namespace VoxelGame;

public class WorldGenerator
{
    private World _world;
    private bool _isRunning = true;

    private AutoResetEvent _resetEvent = new AutoResetEvent(true);
    private List<Thread> _threads = new();

    public ConcurrentQueue<Vector3i> GeneratorQueue = new();
    public ConcurrentQueue<Vector3i> HighPriorityGeneratorQueue = new();

    public ConcurrentQueue<Vector3i> LightQueue = new();
    public ConcurrentQueue<Vector3i> HighPriorityLightQueue = new();
    
    public ConcurrentQueue<Vector3i> MeshQueue = new();
    public ConcurrentQueue<Vector3i> HighPriorityMeshQueue = new();
    
    public ConcurrentQueue<Vector2i> UploadQueue = new();
    public ConcurrentQueue<Vector2i> HighPriorityUploadQueue = new();
    
    public WorldGenerator(World world, bool shouldMesh = true)
    {
        _world = world;
    }

    public void Start()
    {
        for (int i = 0; i < 4; i++)
        {
            _threads.Add(new Thread(Process) { Name = "Chunk Process Thread", IsBackground = true});
            _threads[i].Start();
        }
    }

    public void Stop()
    {
        _isRunning = false;
    }

    public void Poll()
    {
        if (HighPriorityMeshQueue.Count + MeshQueue.Count + HighPriorityGeneratorQueue.Count + GeneratorQueue.Count > 0) _resetEvent.Set();
        
        while (HighPriorityUploadQueue.TryDequeue(out Vector2i position) || UploadQueue.TryDequeue(out position))
        {
            UploadMesh(_world.Chunks[position]);
            _world.Chunks[position].HasPriority = false;
        }
    }

    public void UpdateChunk(Vector2i chunkPosition, int distance, ChunkStatus status = ChunkStatus.Empty, bool enablePriority = false)
    {
        if (_world.Chunks.TryGetValue(chunkPosition, out Chunk chunk))
        {
            if (status != ChunkStatus.Empty) chunk.Status = status;
            chunk.HasPriority = enablePriority;
            switch (chunk.Status)
            {
                case ChunkStatus.Empty:
                    if (chunk.HasPriority)
                    {
                        HighPriorityGeneratorQueue.Enqueue((chunkPosition.X, chunkPosition.Y, distance));
                    }
                    else
                    {
                        GeneratorQueue.Enqueue((chunkPosition.X, chunkPosition.Y, distance));
                    }
                    break;
                case ChunkStatus.Mesh:
                    if (chunk.HasPriority)
                    {
                        HighPriorityMeshQueue.Enqueue((chunkPosition.X, chunkPosition.Y, distance));
                    }
                    else
                    {
                        MeshQueue.Enqueue((chunkPosition.X, chunkPosition.Y, distance));
                    }
                    break;
            }
        }
    }

    private void Process()
    {
        Vector3i sample;
        Vector2i position;
        int distance;
        
        while (_isRunning)
        {
            _resetEvent.WaitOne();
            if (HighPriorityGeneratorQueue.TryDequeue(out sample) || GeneratorQueue.TryDequeue(out sample))
            {
                position = sample.Xy;
                distance = sample.Z;

                if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                {
                    GenerateChunk(chunk);
                    chunk.Status = ChunkStatus.Light;
                    if (chunk.HasPriority)
                    {
                        HighPriorityLightQueue.Enqueue(sample);
                    }
                    else
                    {
                        LightQueue.Enqueue(sample);
                    }
                }
            }

            if (HighPriorityLightQueue.TryDequeue(out sample) || LightQueue.TryDequeue(out sample))
            {
                position = sample.Xy;
                distance = sample.Z;

                if (distance < Config.Radius)
                {
                    if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                    {
                        if (AreNeighborsTheSameStatus(position, ChunkStatus.Light))
                        {
                            ProcessLights(_world, chunk);
                            chunk.Status = ChunkStatus.Mesh;
                            if (chunk.HasPriority)
                            {
                                HighPriorityMeshQueue.Enqueue(sample);
                            }
                            else
                            {
                                MeshQueue.Enqueue(sample);
                            }
                        }
                        else
                        {
                            LightQueue.Enqueue(sample);
                        }
                    }
                }
            }

            if (HighPriorityMeshQueue.TryDequeue(out sample) || MeshQueue.TryDequeue(out sample))
            {
                position = sample.Xy;
                distance = sample.Z;

                if (distance < Config.Radius - 1)
                {
                    if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                    {
                        if (AreNeighborsTheSameStatus(position, ChunkStatus.Mesh))
                        {
                            GenerateMesh(_world, chunk);
                            chunk.Status = ChunkStatus.Upload;
                            if (chunk.HasPriority)
                            {
                                HighPriorityUploadQueue.Enqueue(position);                            
                            }
                            else
                            {
                                UploadQueue.Enqueue(position);
                            }
                        }
                        else
                        {
                            MeshQueue.Enqueue(sample);
                        }
                    }
                }
            }
        }
    }

    public bool HasAllNeighbors(Vector2i position)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if (!_world.Chunks.ContainsKey(position + (x, z))) return false;
            }
        }
        return true;
    }
    
    public bool AreNeighborsTheSameStatus(Vector2i position, ChunkStatus status)
    {
        for (int x = -1; x <= 1; x++)
        {
            for (int z = -1; z <= 1; z++)
            {
                if ((x, z) != Vector2i.Zero)
                {
                    if (!_world.Chunks.ContainsKey(position + (x, z)) ||
                        _world.Chunks[position + (x, z)].Status < status)
                    {
                        return false;
                    }
                }
            }
        }
        return true;
    }
    
    public void GenerateChunk(Chunk chunk)
    {
        Vector3i offset = new Vector3i(chunk.Position.X, 0, chunk.Position.Y) * Config.ChunkSize;

        Vector3i step = new Vector3i(8);
        Vector3i arraySize = Noise.NoiseSizeValue3(step, new Vector3i(Config.ChunkSize, Config.ChunkSize * Config.ColumnSize, Config.ChunkSize));
        Span<float> densityOneArray = stackalloc float[arraySize.X * arraySize.Y * arraySize.Z];
        Span<float> densityTwoArray = stackalloc float[arraySize.X * arraySize.Y * arraySize.Z];
        Span<float> densitySelectorNoise = stackalloc float[arraySize.X * arraySize.Y * arraySize.Z];
        
        Noise.PregenerateValue3(densityOneArray, Config.Seed, step, arraySize, offset, new Vector3(128), true, 8);
        Noise.PregenerateValue3(densityTwoArray, Config.Seed + 1, step, arraySize, offset, new Vector3(128), true, 8);
        Noise.PregenerateValue3(densitySelectorNoise, Config.Seed + 2, step, arraySize, offset, new Vector3(32), false, 4);
        
        Vector3i globalBlockPosition = Vector3i.Zero;
        
        for (int x = 0; x < Config.ChunkSize; x++)
        {
            for (int z = 0; z < Config.ChunkSize; z++)
            {
                globalBlockPosition.Xz = (x, z) + (Config.ChunkSize * chunk.Position);

                float flatness = Noise.Value2(Config.Seed + 2, (Vector2)globalBlockPosition.Xz / 128.0f, true, 4);
                // flatness = ScaleClampNormalize(flatness, 5.0f);
                flatness = (flatness + 1.0f) * 0.5f;
                flatness = Maths.SmoothMap(0.0f, 1.0f, 0.25f, 0.75f, flatness);
                
                for (int y = (Config.ChunkSize * Config.ColumnSize) - 1; y >= 0; y--)
                {
                    globalBlockPosition.Y = y;

                    Vector3 pos = (x, y, z) / (Vector3) step;

                    float densityOne = Noise.Value3(densityOneArray, pos, arraySize);
                    float densityTwo = Noise.Value3(densityTwoArray, pos, arraySize);
                    float densitySelector = Noise.Value3(densitySelectorNoise, pos, arraySize);
                    densitySelector = ScaleClampNormalize(densitySelector, 10.0f);
                    
                    float density = float.Lerp(densityOne, densityTwo, densitySelector);
                    float yHeight = Maths.InverseLerp(y, 256, 128);
                    density *= Maths.Map(1.0f, 0.1f, 0.75f, 1.0f, flatness);
                    
                    if (density + yHeight >= 1.0f)
                    {
                        Register.GetBlockFromId("stone").OnBlockPlace(_world, globalBlockPosition);
                        // chunk.SetBlock((x, y, z), Register.GetBlockFromId("stone"));
                    } else if (y <= 128)
                    {
                        Register.GetBlockFromId("water").OnBlockPlace(_world, globalBlockPosition);
                        // chunk.SetBlock((x,y,z), Register.GetBlockFromId("water"));
                    }
                }
            }
        }
        
        for (int x = 0; x < Config.ChunkSize; x++)
        {
            for (int z = 0; z < Config.ChunkSize; z++)
            {
                globalBlockPosition.Xz = (x, z) + (Config.ChunkSize * chunk.Position);
                float rand = Noise.FloatRandom2(123, globalBlockPosition.Xz);
                
                for (int y = (Config.ChunkSize * Config.ColumnSize) - 1; y >= 0; y--)
                {
                    globalBlockPosition.Y = y;

                    if (!chunk.GetTransparent((x,y,z)) && chunk.GetSolid((x,y,z)) && !chunk.GetSolid((x,y + 1,z)))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (!chunk.GetSolid((x, y - i, z))) break;
                            Register.GetBlockFromId("dirt").OnBlockPlace(_world, globalBlockPosition - Vector3i.UnitY * i);
                            // chunk.SetBlock((x, y - i, z), Register.GetBlockFromId("dirt"));
                        }
                        
                        Register.GetBlockFromId(rand > 0.95 ? "sand" : "grass").OnBlockPlace(_world, globalBlockPosition);
                        // chunk.SetBlock((x, y, z), Register.GetBlockFromId("grass"));
                    }
                }
            }
        }

        for (int x = 0; x < Config.ChunkSize; x++)
        {
            for (int z = 0; z < Config.ChunkSize; z++)
            {
                globalBlockPosition.Xz = (x, z) + (Config.ChunkSize * chunk.Position);
                
                if (!chunk.GetSolid((x, (Config.ChunkSize * Config.ColumnSize) - 1, z)))
                {
                    chunk.SetSunlightValue((x, (Config.ChunkSize * Config.ColumnSize) - 1, z), 15);
                    chunk.SunlightAdditionQueue.Enqueue((globalBlockPosition.X, (Config.ChunkSize * Config.ColumnSize) - 1, globalBlockPosition.Z));
                }
            }
        }
    }

    private void ProcessLights(World world, Chunk chunk)
    {
        while (chunk.SunlightAdditionQueue.TryDequeue(out Vector3i globalBlockPosition))
        {
            ushort currentCell = world.SunlightValueAt(globalBlockPosition);
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY))
            {
                if (currentCell == 15)
                {
                    world.SetSunlightValue(globalBlockPosition - Vector3i.UnitY, currentCell);
                    chunk.SunlightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitY);
                    // world.AddSunlight(globalBlockPosition - Vector3i.UnitY, currentCell);
                }
                else if (world.SunlightValueAt(globalBlockPosition - Vector3i.UnitY) + 2 <= currentCell)
                {
                    world.SetSunlightValue(globalBlockPosition - Vector3i.UnitY, (ushort) (currentCell - 1));
                    chunk.SunlightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitY);
                    // world.AddSunlight(globalBlockPosition - Vector3i.UnitY, (ushort) (currentCell - 1));
                }
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY) && world.SunlightValueAt(globalBlockPosition + Vector3i.UnitY) + 2 <= currentCell)
            {
                world.SetSunlightValue(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
                chunk.SunlightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitY);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX) && world.SunlightValueAt(globalBlockPosition + Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetSunlightValue(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.SunlightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX) && world.SunlightValueAt(globalBlockPosition - Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetSunlightValue(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.SunlightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ) && world.SunlightValueAt(globalBlockPosition + Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetSunlightValue(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.SunlightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ) && world.SunlightValueAt(globalBlockPosition - Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetSunlightValue(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.SunlightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
        }
        
        while (chunk.RedLightAdditionQueue.TryDequeue(out Vector3i globalBlockPosition))
        {
            ushort currentCell = world.RedLightValueAt(globalBlockPosition);
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY) && world.RedLightValueAt(globalBlockPosition - Vector3i.UnitY) + 2 <= currentCell)
            {
                world.SetRedLightValue(globalBlockPosition - Vector3i.UnitY, (ushort) (currentCell - 1));
                chunk.RedLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitY);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));RedL
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY) && world.RedLightValueAt(globalBlockPosition + Vector3i.UnitY) + 2 <= currentCell)
            {
                world.SetRedLightValue(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
                chunk.RedLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitY);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX) && world.RedLightValueAt(globalBlockPosition + Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetRedLightValue(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.RedLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX) && world.RedLightValueAt(globalBlockPosition - Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetRedLightValue(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.RedLightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ) && world.RedLightValueAt(globalBlockPosition + Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetRedLightValue(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.RedLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ) && world.RedLightValueAt(globalBlockPosition - Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetRedLightValue(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.RedLightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
        }
        
        while (chunk.GreenLightAdditionQueue.TryDequeue(out Vector3i globalBlockPosition))
        {
            ushort currentCell = world.GreenLightValueAt(globalBlockPosition);
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY) && world.GreenLightValueAt(globalBlockPosition - Vector3i.UnitY) + 2 <= currentCell)
            {
                world.SetGreenLightValue(globalBlockPosition - Vector3i.UnitY, (ushort) (currentCell - 1));
                chunk.GreenLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitY);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));GreenL
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY) && world.GreenLightValueAt(globalBlockPosition + Vector3i.UnitY) + 2 <= currentCell)
            {
                world.SetGreenLightValue(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
                chunk.GreenLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitY);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX) && world.GreenLightValueAt(globalBlockPosition + Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetGreenLightValue(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.GreenLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX) && world.GreenLightValueAt(globalBlockPosition - Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetGreenLightValue(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.GreenLightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ) && world.GreenLightValueAt(globalBlockPosition + Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetGreenLightValue(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.GreenLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ) && world.GreenLightValueAt(globalBlockPosition - Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetGreenLightValue(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.GreenLightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
        }
        
        while (chunk.BlueLightAdditionQueue.TryDequeue(out Vector3i globalBlockPosition))
        {
            ushort currentCell = world.BlueLightValueAt(globalBlockPosition);
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitY) && world.BlueLightValueAt(globalBlockPosition - Vector3i.UnitY) + 2 <= currentCell)
            {
                world.SetBlueLightValue(globalBlockPosition - Vector3i.UnitY, (ushort) (currentCell - 1));
                chunk.BlueLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitY);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));BlueL
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitY) && world.BlueLightValueAt(globalBlockPosition + Vector3i.UnitY) + 2 <= currentCell)
            {
                world.SetBlueLightValue(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
                chunk.BlueLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitY);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitY, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitX) && world.BlueLightValueAt(globalBlockPosition + Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetBlueLightValue(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.BlueLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitX) && world.BlueLightValueAt(globalBlockPosition - Vector3i.UnitX) + 2 <= currentCell)
            {
                world.SetBlueLightValue(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
                chunk.BlueLightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitX);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitX, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition + Vector3i.UnitZ) && world.BlueLightValueAt(globalBlockPosition + Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetBlueLightValue(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.BlueLightAdditionQueue.Enqueue(globalBlockPosition + Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition + Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
            
            if (!world.GetBlockIsSolid(globalBlockPosition - Vector3i.UnitZ) && world.BlueLightValueAt(globalBlockPosition - Vector3i.UnitZ) + 2 <= currentCell)
            {
                world.SetBlueLightValue(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
                chunk.BlueLightAdditionQueue.Enqueue(globalBlockPosition - Vector3i.UnitZ);
                // world.AddSunlight(globalBlockPosition - Vector3i.UnitZ, (ushort) (currentCell - 1));
            }
        }
    }

    float ScaleClampNormalize(float value, float scale)
    {
        return (float.Clamp(value * scale, -1.0f, 1.0f) + 1.0f) * 0.5f;
    }
    
    private void GenerateMesh(World world, Chunk chunk)
    {
        for (int i = 0; i < Config.ColumnSize; i++)
        {
            ChunkSectionMesh mesh = chunk.ChunkMeshes[i];
            // if (!mesh.ShouldUpdate || chunk.ChunkSections[i].IsEmpty) continue;
            // if (!mesh.ShouldUpdate) continue;
            // if (!mesh.ShouldUpdate) continue;
            // mesh.ShouldUpdate = false;
            
            if (!mesh.ShouldUpdate) continue;
            
            mesh.SolidVertices.Clear();
            mesh.SolidIndices.Clear();
            mesh.TransparentVertices.Clear();
            mesh.TransparentIndices.Clear();
            
            Stopwatch sw = Stopwatch.StartNew();
            for (int x = 0; x < Config.ChunkSize; x++)
            {
                for (int y = 0; y < Config.ChunkSize; y++)
                {
                    for (int z = 0; z < Config.ChunkSize; z++)
                    {
                        Vector3i globalBlockPosition = (x, y, z) + new Vector3i(chunk.Position.X, i, chunk.Position.Y) * Config.ChunkSize;
                        // string? id = chunk.ChunkSections[i].GetBlockId((x, y, z));
                        string id = chunk.GetBlockId((x, y + (i * Config.ChunkSize), z));
                        if (id != "air")
                        {
                            Register.GetBlockFromId(id).OnBlockMesh(world, globalBlockPosition);
                        }
                    }
                }
            }

            for (int m = 0; m < mesh.SolidVertices.Count; m += 4)
            {
                mesh.SolidIndices.AddRange(0 + m, 1 + m, 2 + m, 2 + m, 3 + m, 0 + m);
            }

            for (int m = 0; m < mesh.TransparentVertices.Count; m += 4)
            {
                mesh.TransparentIndices.AddRange(0 + m, 1 + m, 2 + m, 2 + m, 3 + m, 0 + m);
            }
            sw.Stop();
            Config.LastMeshTime = sw.Elapsed;
            Config.MeshTimes.Add(sw.Elapsed);
        }
    }
    
    public void UploadMesh(Chunk column)
    {
        for (int i = 0; i < Config.ColumnSize; i++)
        {
            ChunkSectionMesh mesh = column.ChunkMeshes[i];
            if (!mesh.ShouldUpdate) continue;
            mesh.ShouldUpdate = false;
            mesh.Update();
        }
        
        column.Status = ChunkStatus.Done;
    }
}