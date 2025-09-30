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
    private List<Thread> _generatorThreads = new();
    private Dictionary<int, AutoResetEvent> _generatorResetEvents = new();
    private AutoResetEvent _generatorResetEvent = new AutoResetEvent(true);
    public bool ShouldMesh = false;
    private bool _shouldRun = true;

    public ConcurrentQueue<Vector3i> GeneratorQueue = new();
    public ConcurrentQueue<Vector3i> HighPriorityGeneratorQueue = new(); 
    
    public ConcurrentQueue<Vector3i> MeshQueue = new();
    public ConcurrentQueue<Vector3i> HighPriorityMeshQueue = new();
    
    public ConcurrentQueue<Vector2i> UploadQueue = new();
    public ConcurrentQueue<Vector2i> HighPriorityUploadQueue = new();
    
    // TODO: separate threads per process? (gen, light, mesh)
    
    public WorldGenerator(World world, bool shouldMesh = true)
    {
        _world = world;
        ShouldMesh = shouldMesh;
    }

    public WorldGenerator Start()
    {
        for (int i = 0; i < 4; i++)
        {
            _generatorThreads.Add(new Thread(HandleGenerationQueue) { IsBackground = true });
            _generatorResetEvents.Add(_generatorThreads[i].ManagedThreadId, new AutoResetEvent(true));
            _generatorThreads[i].Name = "Generation Thread";
            _generatorThreads[i].Start();
            
            // _meshThreads.Add(new Thread(HandleMeshQueue) { IsBackground = true});
            // _meshThreads[i].Name = "Meshing Thread";
            // _meshThreads[i].Start();
        }
        
        return this;
    }

    public WorldGenerator Stop()
    {
        _shouldRun = false;
        // foreach (Thread thr in _generatorThreads)
        // {
        //     while (thr.IsAlive) _generatorResetEvent.Set();
        // }

        return this;
    }

    public void Poll()
    {
        if (GeneratorQueue.Count + HighPriorityGeneratorQueue.Count + MeshQueue.Count + HighPriorityMeshQueue.Count > 0)
        {
            foreach (AutoResetEvent resetEvent in _generatorResetEvents.Values)
            {
                resetEvent.Set();
            }
        }
        
        while (HighPriorityUploadQueue.TryDequeue(out Vector2i position) || UploadQueue.TryDequeue(out position))
        {
            // if (UploadQueue.Contains(position)) continue;
            
            // Monitor.Enter(_world.Chunks[position]);
            // _world.Chunks[position].Mutex.WaitOne();
            UploadMesh(_world.Chunks[position]);
            _world.Chunks[position].HasPriority = false;
            // _world.Chunks[position].Mutex.ReleaseMutex();
            // Monitor.Exit(_world.Chunks[position]);
        }
    }

    public void UpdateChunk(Vector2i chunkPosition, ChunkStatus status = ChunkStatus.Empty, bool enablePriority = false)
    {
        if (_world.Chunks.TryGetValue(chunkPosition, out Chunk chunk))
        {
            if (status != ChunkStatus.Empty) chunk.Status = status;
            if (enablePriority) chunk.HasPriority = true;
            HighPriorityGeneratorQueue.Enqueue((chunk.Position.X, chunk.Position.Y, 0));
        }
    }

    private void HandleGenerationQueue()
    {
        Vector2i position;
        int distance;
        
        while (_shouldRun)
        {
            // _generatorResetEvent.WaitOne();
            _generatorResetEvents[Thread.CurrentThread.ManagedThreadId].WaitOne();
            if (HighPriorityGeneratorQueue.TryDequeue(out Vector3i generationSample) || GeneratorQueue.TryDequeue(out generationSample))
            {
                position = generationSample.Xy;
                distance = generationSample.Z;
                
                if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                {
                    // Monitor.Enter(chunk);
                    switch (chunk.Status)
                    {
                        case ChunkStatus.Empty:
                            GenerateChunk(chunk);
                            chunk.Status = ChunkStatus.Mesh;
                            if (chunk.HasPriority)
                            {
                                HighPriorityMeshQueue.Enqueue(generationSample);
                            }
                            else
                            {
                                MeshQueue.Enqueue(generationSample);
                            }
                            break;
                        case ChunkStatus.Mesh:
                            if (chunk.HasPriority)
                            {
                                HighPriorityMeshQueue.Enqueue(generationSample);
                            }
                            else
                            {
                                MeshQueue.Enqueue(generationSample);
                            }
                            break;
                    }
                    // Monitor.Exit(chunk);
                }
            }

            if (HighPriorityMeshQueue.TryDequeue(out Vector3i meshSample) || MeshQueue.TryDequeue(out meshSample))
            {
                position = meshSample.Xy;
                distance = meshSample.Z;
                
                if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                {
                    switch (chunk.Status)
                    {
                        case ChunkStatus.Mesh:
                            if (distance < Config.Radius)
                            {
                                if (AreNeighborsTheSameStatus(position, ChunkStatus.Mesh))
                                {
                                    GenerateMesh(Config.World, chunk);
                                    chunk.Status = ChunkStatus.Upload;
                                    UploadQueue.Enqueue(position);
                                }
                                else
                                {
                                    MeshQueue.Enqueue(meshSample);
                                }
                            }
                            break;
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
                        chunk.SetBlock((x, y, z), Register.GetBlockFromId("stone"));
                    } else if (y <= 128)
                    {
                        chunk.SetBlock((x,y,z), Register.GetBlockFromId("water"));
                    }
                }
            }
        }
        
        for (int x = 0; x < Config.ChunkSize; x++)
        {
            for (int z = 0; z < Config.ChunkSize; z++)
            {
                globalBlockPosition.Xz = (x, z) + (Config.ChunkSize * chunk.Position);
                
                for (int y = (Config.ChunkSize * Config.ColumnSize) - 1; y >= 0; y--)
                {
                    globalBlockPosition.Y = y;

                    if (!chunk.GetTransparent((x,y,z)) && chunk.GetSolid((x,y,z)) && !chunk.GetSolid((x,y + 1,z)))
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            chunk.SetBlock((x, y - i, z), Register.GetBlockFromId("dirt"));
                        }
                        
                        chunk.SetBlock((x, y, z), Register.GetBlockFromId("grass"));
                    }
                }
            }
        }
    }

    float WeirdCurve(float value, float start, float end, float slope)
    {
        float val = -float.Cos((2.0f * float.Pi) * (float.Clamp(value, start, end) / end - start) - ((2.0f * float.Pi) / end - start));
        
        return value * val;
    }

    float ScaleClampNormalize(float value, float scale)
    {
        return (float.Clamp(value * scale, -1.0f, 1.0f) + 1.0f) * 0.5f;
    }

    void Line(Vector3i from, Vector3i to, Chunk chunk)
    {
        Vector3 direction = Vector3.Normalize(to - from);

        for (int i = 0; i < 256; i++) 
        {
            Vector3 current = Vector3.Lerp(from, to, i / 256.0f);
            
            chunk.SetBlock((Vector3i)current);
        }
    }

    float Remap(float a, float v1, float v2)
    {
        return (a - v1) * (1.0f / (v2 - v1));
    }
    
    public void GenerateMesh(World world, Chunk chunk)
    {
        for (int i = 0; i < Config.ColumnSize; i++)
        {
            ChunkSectionMesh mesh = chunk.ChunkMeshes[i];
            // if (!mesh.ShouldUpdate || chunk.ChunkSections[i].IsEmpty) continue;
            // if (!mesh.ShouldUpdate) continue;
            // if (!mesh.ShouldUpdate) continue;
            // mesh.ShouldUpdate = false;
            
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
                            Register.GetBlockFromId(id).OnBlockMesh(_world, globalBlockPosition);
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
            // if (!mesh.ShouldUpdate) continue;
            mesh.Update();
        }
        
        column.Status = ChunkStatus.Done;
    }
}