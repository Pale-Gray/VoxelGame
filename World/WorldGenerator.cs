using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using OpenTK.Mathematics;
using VoxelGame.Util;

namespace VoxelGame;

public class WorldGenerator
{
    private World _world;
    private List<Thread> _generatorThreads = new();
    private List<Thread> _meshThreads = new();
    private AutoResetEvent _generatorResetEvent = new AutoResetEvent(true);
    private AutoResetEvent _meshResetEvent = new AutoResetEvent(true);
    public bool ShouldMesh = false;
    private bool _shouldRun = true;

    public ConcurrentQueue<Vector3i> GeneratorQueue = new();
    public ConcurrentQueue<Vector3i> MeshQueue = new();
    
    public ConcurrentQueue<Vector2i> UploadQueue = new();
    
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
        // if (GenerationQueue.Count > 0 || MeshQueue.Count > 0) _generatorResetEvent.Set();
        if (GeneratorQueue.Count > 0) _generatorResetEvent.Set();
        if (MeshQueue.Count > 0) _generatorResetEvent.Set();
        
        while (UploadQueue.TryDequeue(out Vector2i position))
        {
            // if (UploadQueue.Contains(position)) continue;
            
            // Monitor.Enter(_world.Chunks[position]);
            // _world.Chunks[position].Mutex.WaitOne();
            UploadMesh(_world.Chunks[position]);
            // _world.Chunks[position].Mutex.ReleaseMutex();
            // Monitor.Exit(_world.Chunks[position]);
        }
    }

    private void HandleMeshQueue()
    {
        Logger.Info("Hello from mesh thread");
        
        while (_shouldRun)
        {
            _meshResetEvent.WaitOne();
            while (MeshQueue.TryDequeue(out Vector3i sample))
            {
                Vector2i position = sample.Xy;
                int distance = sample.Z;

                if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                {
                    Monitor.Enter(chunk);
                    switch (chunk.Status)
                    {
                        case ChunkStatus.Mesh:
                            if (distance < Config.Radius)
                            {
                                if (AreNeighborsTheSameStatus(position, ChunkStatus.Mesh))
                                {
                                    GenerateMesh(_world, chunk);
                                    chunk.Status = ChunkStatus.Upload;
                                    UploadQueue.Enqueue(position);
                                }
                                else
                                {
                                    MeshQueue.Enqueue(sample);
                                }
                            }
                            break;
                    }
                    Monitor.Exit(chunk);
                }
            }
        }
    }

    private void HandleGenerationQueue()
    {
        Vector2i position;
        int distance;
        
        while (_shouldRun)
        {
            _generatorResetEvent.WaitOne();
            if (GeneratorQueue.TryDequeue(out Vector3i generationSample))
            {
                position = generationSample.Xy;
                distance = generationSample.Z;

                if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                {
                    Monitor.Enter(chunk);
                    switch (chunk.Status)
                    {
                        case ChunkStatus.Empty:
                            GenerateChunk(chunk);
                            chunk.Status = ChunkStatus.Mesh;
                            MeshQueue.Enqueue(generationSample);
                            break;
                        case ChunkStatus.Mesh:
                            MeshQueue.Enqueue(generationSample);
                            break;
                    }
                    Monitor.Exit(chunk);
                }
            }

            if (MeshQueue.TryDequeue(out Vector3i meshSample))
            {
                position = meshSample.Xy;
                distance = meshSample.Z;
                
                if (_world.Chunks.TryGetValue(position, out Chunk chunk))
                {
                    Monitor.Enter(chunk);
                    switch (chunk.Status)
                    {
                        case ChunkStatus.Mesh:
                            if (distance < Config.Radius)
                            {
                                if (AreNeighborsTheSameStatus(position, ChunkStatus.Mesh))
                                {
                                    GenerateMesh(_world, chunk);
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
                    Monitor.Exit(chunk);
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
        
        float seaLevel = 256.0f;
        float maxAscent = 64.0f;

        Vector3i step = new Vector3i(32, 16, 32);
        
        Vector3i arraySize = Noise.NoiseSizeValue3(step, (Config.ChunkSize, Config.ChunkSize * Config.ColumnSize, Config.ChunkSize));
        Span<float> noiseOneArray = stackalloc float[arraySize.X * arraySize.Y * arraySize.Z];
        Noise.PregenerateValue3(noiseOneArray, 0, step, arraySize, new Vector3i(chunk.Position.X, 0, chunk.Position.Y) * Config.ChunkSize, new Vector3(64), true, 4);
        
        for (int x = 0; x < Config.ChunkSize; x++)
        {
            for (int z = 0; z < Config.ChunkSize; z++)
            {
                Vector3i globalPosition = new Vector3i(x, 0, z) + (new Vector3i(chunk.Position.X, 0, chunk.Position.Y) * Config.ChunkSize);

                float selector = Noise.Value2(1, (Vector2)globalPosition.Xz / 64.0f, true, 4);
                selector *= 10.0f;
                selector = (float.Clamp(selector, -1.0f, 1.0f) + 1.0f) * 0.5f;

                float continentality = Noise.Value2(5, (Vector2)globalPosition.Xz / 512.0f, true, 4);
                continentality *= 10.0f;
                continentality = (float.Clamp(continentality, -1.0f, 1.0f) + 1.0f) * 0.5f;
                
                float erosionOne = Noise.Value2(2, (Vector2)globalPosition.Xz / 128.0f, true, 2);
                float erosionTwo = Noise.Value2(3, (Vector2)globalPosition.Xz / 128.0f, true, 2);
                float erosion = (float.Lerp(erosionOne, erosionTwo, selector) + 1.0f) * 0.5f;

                erosion *= continentality;
                
                for (int y = Config.ChunkSize * Config.ColumnSize - 1; y >= 0; y--)
                {
                    globalPosition.Y = y;
                    float min = seaLevel - 128 * (1.0f - erosion);
                    float max = seaLevel + 256 - ((256 - 64) * (1.0f - erosion));
                    float height = Remap(globalPosition.Y, min, max);
                    height = (1.0f - height);

                    float density = Noise.Value3(noiseOneArray, new Vector3(x, y, z) / step, arraySize) * 0.5f;
                    
                    if (density + height >= 0.5f)
                    {
                        chunk.SetBlock((x, y, z), Config.Register.GetBlockFromId("stone"));
                    } else if (y <= seaLevel)
                    {
                        chunk.SetBlock((x, y, z), Config.Register.GetBlockFromId("water"));
                    }
                }
            }
        }
        
        for (int x = 0; x < Config.ChunkSize; x++)
        {
            for (int z = 0; z < Config.ChunkSize; z++)
            {
                Vector3i globalPosition = new Vector3i(x, 0, z) + (new Vector3i(chunk.Position.X, 0, chunk.Position.Y) * Config.ChunkSize);
                
                for (int y = Config.ChunkSize * Config.ColumnSize - 1; y >= 0; y--)
                {
                    globalPosition.Y = y;

                    if (chunk.GetBlockId((x, y, z)) == "stone" && !chunk.GetSolid((x, y + 1, z)))
                    {
                        for (int i = 0; i < 5; i++) if (chunk.GetSolid((x, y - i, z))) chunk.SetBlock((x, y - i, z), Config.Register.GetBlockFromId("dirt"));
                        chunk.SetBlock((x, y, z), Config.Register.GetBlockFromId("grass"));
                    }
                }
            }
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
            if (!mesh.ShouldUpdate || chunk.ChunkSections[i].IsEmpty) continue;
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
                        string? id = chunk.ChunkSections[i].GetBlockId((x, y, z));
                        if (id != null)
                        {
                            Config.Register.GetBlockFromId(id).OnBlockMesh(_world, globalBlockPosition);
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
            mesh.Update();
        }
        
        column.Status = ChunkStatus.Done;
    }

    public void EnqueueChunk(Vector2i position, ChunkStatus chunkStatus, bool hasPriority)
    {
        _world.Chunks[position].Mutex.WaitOne();
        _world.Chunks[position].HasPriority = hasPriority;
        _world.Chunks[position].Status = chunkStatus;
        _world.Chunks[position].Mutex.ReleaseMutex();
    }
}