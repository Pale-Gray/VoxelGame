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
        Vector3i offset = new Vector3i(chunk.Position.X, 0, chunk.Position.Y) * Config.ChunkSize;

        Vector3i step = new Vector3i(16, 16, 16);
        Vector3i arraySize = Noise.NoiseSizeValue3(step, (Config.ChunkSize, Config.ChunkSize * Config.ColumnSize, Config.ChunkSize));
        Span<float> densityOneNoise = stackalloc float[arraySize.X * arraySize.Y * arraySize.Z];
        Noise.PregenerateValue3(densityOneNoise, Config.Seed + 0, step, arraySize, offset, new Vector3(64.0f), true, 4);
        Span<float> densityTwoNoise = stackalloc float[arraySize.X * arraySize.Y * arraySize.Z];
        Noise.PregenerateValue3(densityTwoNoise, Config.Seed + 1, step, arraySize, offset, new Vector3(64.0f), true, 4);
        Span<float> densitySelectorNoise = stackalloc float[arraySize.X * arraySize.Y * arraySize.Z];
        Noise.PregenerateValue3(densitySelectorNoise, Config.Seed + 2, step, arraySize, offset, new Vector3(64.0f), false, 4);
        Vector3i globalBlockPosition = Vector3i.Zero;
        
        for (int x = 0; x < Config.ChunkSize; x++)
        {
            for (int z = 0; z < Config.ChunkSize; z++)
            {
                globalBlockPosition.Xz = (x, z) + (Config.ChunkSize * chunk.Position);

                float continentalityOneNoise = Noise.Value2(Config.Seed + 3, (Vector2)globalBlockPosition.Xz / 256.0f, false, 4);
                float continentalityTwoNoise = Noise.Value2(Config.Seed + 4, (Vector2)globalBlockPosition.Xz / 128.0f, false, 4);
                float continentalitySelector = Noise.Value2(Config.Seed + 5, (Vector2)globalBlockPosition.Xz / 256.0f, false, 8);
                continentalitySelector = ScaleClampNormalize(continentalitySelector, 5.0f);

                float continentality = float.Lerp(continentalityOneNoise, continentalityTwoNoise, continentalitySelector);
                continentality = ScaleClampNormalize(continentality, 2.0f);

                float largeContinentality = Noise.Value2(Config.Seed + 9, (Vector2)globalBlockPosition.Xz / 512.0f, false, 8);
                largeContinentality = ScaleClampNormalize(largeContinentality, 2.0f);

                continentality *= largeContinentality;

                float flatnessOneNoise = Noise.Value2(Config.Seed + 6, (Vector2)globalBlockPosition.Xz / 256.0f, false, 2);
                float flatnessTwoNoise = Noise.Value2(Config.Seed + 7, (Vector2)globalBlockPosition.Xz / 256.0f, false, 2);
                float flatnessSelector = Noise.Value2(Config.Seed + 5, (Vector2)globalBlockPosition.Xz / 128.0f, false, 2);
                flatnessSelector = ScaleClampNormalize(flatnessSelector, 5.0f);

                float flatness = float.Lerp(flatnessOneNoise, flatnessTwoNoise, flatnessSelector);
                flatness = ScaleClampNormalize(flatness, 2.0f);

                continentality *= (1.0f - flatness);
                
                for (int y = (Config.ChunkSize * Config.ColumnSize) - 1; y >= 0; y--)
                {
                    globalBlockPosition.Y = y;

                    float densitySelector = Noise.Value3(densitySelectorNoise, (x,y,z) / (Vector3)step, arraySize);
                    densitySelector *= 10.0f;
                    densitySelector = float.Clamp(densitySelector, -1.0f, 1.0f);
                    densitySelector = (densitySelector + 1.0f) * 0.5f;

                    float density = float.Lerp(Noise.Value3(densityOneNoise, (x, y, z) / (Vector3)step, arraySize), Noise.Value3(densityTwoNoise, (x, y, z) / (Vector3)step, arraySize), densitySelector);
                    density = (density + 1.0f) * 0.5f;
                    density *= float.Lerp(0.1f, 1.0f, continentality);

                    float yHeight = Remap(y, 256 - float.Lerp(0, 128, flatness), 128 - float.Lerp(32, 0, continentality));

                    if (density + yHeight > 1.0)
                    {
                        if (continentality > 0.5)
                        {
                            chunk.SetBlock((x,y,z), Register.GetBlockFromId("stone"));
                        }
                        else
                        {
                            chunk.SetBlock((x,y,z), Register.GetBlockFromId("sand"));
                        }
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

                    Vector3i localBlockPosition = (x, y, z);

                    if (chunk.GetSolid(localBlockPosition) && !chunk.GetSolid(localBlockPosition + Vector3i.UnitY) && chunk.GetBlockId(localBlockPosition) != "sand")
                    {
                        for (int i = 0; i < 5; i++)
                        {
                            if (!chunk.GetSolid(localBlockPosition - Vector3i.UnitY * i)) break;
                            chunk.SetBlock(localBlockPosition - Vector3i.UnitY * i, Register.GetBlockFromId("dirt"));
                        }
                        
                        chunk.SetBlock(localBlockPosition, Register.GetBlockFromId("grass"));
                    } else if (y < 128 && !chunk.GetSolid(localBlockPosition))
                    {
                        chunk.SetBlock(localBlockPosition, Register.GetBlockFromId("water"));
                    }
                }
            }
        }
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