using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Threading;
using OpenTK.Mathematics;
using VoxelGame.Util;

namespace VoxelGame;

public enum ChunkStatus
{
    Empty,
    Light,
    Mesh,
    Upload,
    Done
}

public class Chunk
{
    public Palette<string> Data = new Palette<string>(Config.ChunkSize * Config.ChunkSize * Config.ChunkSize * Config.ColumnSize);
    public ushort[] LightData = new ushort[Config.ChunkSize * Config.ChunkSize * Config.ChunkSize * Config.ColumnSize];
    public ChunkSectionMesh[] ChunkMeshes = new ChunkSectionMesh[Config.ColumnSize];
    public Vector2i Position;
    public ChunkStatus Status = ChunkStatus.Empty;
    public ConcurrentQueue<Vector3i> SunlightAdditionQueue = new();
    public ConcurrentQueue<Vector3i> RedLightAdditionQueue = new();
    public ConcurrentQueue<Vector3i> GreenLightAdditionQueue = new();
    public ConcurrentQueue<Vector3i> BlueLightAdditionQueue = new();
    public float ElapsedTime = 0.0f;
    public bool HasPriority = true;
    
    public Chunk(Vector2i position)
    {
        Position = position;
        for (int i = 0; i < Config.ColumnSize; i++)
        {
            ChunkMeshes[i] = new ChunkSectionMesh() { ChunkPosition = (position.X, i, position.Y) };
        }
    }
    
    public void SetRedValue(Vector3i localPosition, ushort value)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        data &= 0x0FFF;
        data |= (ushort) (value << 12);
        
        LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] = data;
    }
    
    public void SetGreenValue(Vector3i localPosition, ushort value)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        data &= 0xF0FF;
        data |= (ushort) (value << 8);
        
        LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] = data;
    }
    
    public void SetBlueValue(Vector3i localPosition, ushort value)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        data &= 0xFF0F;
        data |= (ushort) (value << 4);
        
        LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] = data;
    }

    public void SetSunlightValue(Vector3i localPosition, ushort value)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        data &= 0xFFF0;
        data |= value;
        
        LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] = data;
    }

    public ushort SunlightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 15;

        return (ushort) (LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] & 0x000F);
    }
    
    public ushort RedLightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 15;

        return (ushort) (LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] >> 12 & 0x000F);
    }
    
    public ushort GreenLightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 15;

        return (ushort) (LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] >> 8 & 0x000F);
    }
    
    public ushort BlueLightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 15;

        return (ushort) (LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)] >> 4 & 0x000F);
    }

    public float NormalizedSunlightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 1.0f;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        
        return (data & 0x000F) / 15.0f;
    }
    
    public float NormalizedRedLightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 1.0f;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        
        return (data >> 12 & 0x000F) / 15.0f;
    }
    
    public float NormalizedGreenLightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 1.0f;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        
        return (data >> 8 & 0x000F) / 15.0f;
    }
    
    public float NormalizedBlueLightValueAt(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return 1.0f;

        ushort data = LightData[VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)];
        
        return (data >> 4 & 0x000F) / 15.0f;
    }

    public void SetBlock(Vector3i localPosition, Block? block = null)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return;
        
        if (block != null)
        {
            Data.Insert(VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize), block.Id);
        }
        else
        {
            Data.Insert(VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize), "air");
        }
    }

    public string GetBlockId(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return "air";
        return Data.Get(VectorMath.Flatten(localPosition, Config.ChunkSize, Config.ChunkSize)) ?? "air";
    }

    public bool GetTransparent(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return false;
        return Register.GetBlockFromId(GetBlockId(localPosition)).IsTransparent;
    }

    public bool GetSolid(Vector3i localPosition)
    {
        if (localPosition.X < 0 || localPosition.X >= Config.ChunkSize || localPosition.Y < 0 || localPosition.Y >= Config.ChunkSize * Config.ColumnSize || localPosition.Z < 0 || localPosition.Z >= Config.ChunkSize) return false;
        return Register.GetBlockFromId(GetBlockId(localPosition)).IsSolid;
    }
}