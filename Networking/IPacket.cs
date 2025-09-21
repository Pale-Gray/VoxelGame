using System;
using OpenTK.Mathematics;
using VoxelGame.Util;

namespace VoxelGame.Networking;

public interface IPacket
{
    public PacketType Type { get; set; }
    public void Serialize(DataWriter writer);
    public IPacket Deserialize(DataReader reader);
}

public enum PacketType
{
    ChunkData,
    PlayerJoin,
    PlayerMove,
    BlockDestroy,
    BlockPlace
}

public struct PlayerJoinPacket() : IPacket
{
    public PacketType Type { get; set; } = PacketType.PlayerJoin;
    public Guid Id;
    public void Serialize(DataWriter writer)
    {
        writer.Write(Id.ToString());
    }

    public IPacket Deserialize(DataReader reader)
    {
        Id = Guid.Parse(reader.ReadString());
        return this;
    }
}

public struct PlayerMovePacket() : IPacket
{
    public PacketType Type { get; set; } = PacketType.PlayerMove;
    public Vector3 Position;
    public void Serialize(DataWriter writer)
    {
        writer.Write(Position.X);
        writer.Write(Position.Y);
        writer.Write(Position.Z);
    }

    public IPacket Deserialize(DataReader reader)
    {
        Position = (reader.ReadFloat(), reader.ReadFloat(), reader.ReadFloat());
        return this;
    }
}

public struct BlockPlacePacket() : IPacket
{
    public PacketType Type { get; set; } = PacketType.BlockPlace;
    public string Id;
    public Vector3i GlobalBlockPosition;

    public void Serialize(DataWriter writer)
    {
        writer.Write(Id);
        writer.Write(GlobalBlockPosition.X);
        writer.Write(GlobalBlockPosition.Y);
        writer.Write(GlobalBlockPosition.Z);
    }

    public IPacket Deserialize(DataReader reader)
    {
        Id = reader.ReadString();
        GlobalBlockPosition = (reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        
        return this;
    }
}

public struct BlockDestroyPacket() : IPacket
{
    public PacketType Type { get; set; } = PacketType.BlockDestroy;
    public string Id;
    public Vector3i GlobalBlockPosition;
    
    public void Serialize(DataWriter writer)
    {
        writer.Write(Id);
        writer.Write(GlobalBlockPosition.X);
        writer.Write(GlobalBlockPosition.Y);
        writer.Write(GlobalBlockPosition.Z);
    }

    public IPacket Deserialize(DataReader reader)
    {
        Id = reader.ReadString();
        GlobalBlockPosition = (reader.ReadInt32(), reader.ReadInt32(), reader.ReadInt32());
        
        return this;
    }
}

public struct ChunkDataPacket() : IPacket
{
    public PacketType Type { get; set; } = PacketType.ChunkData;
    public Vector2i Position;
    public Chunk Column;
    
    public void Serialize(DataWriter writer)
    {
        writer.Write(Position.X);
        writer.Write(Position.Y);
        writer.WriteValues(Column.Data.PaletteEntries);
        writer.Write(Column.Data.Data.Capacity);
        writer.Write(Column.Data.Data.BitSize);
        writer.WriteValues(Column.Data.Data.Data);
    }

    public IPacket Deserialize(DataReader reader)
    {
        Position = (reader.ReadInt32(), reader.ReadInt32());
        Column = new Chunk(Position);
        Column.Data = new Palette<string>(reader.ReadStringValues(), new BitArray(reader.ReadInt32(), reader.ReadInt32(), reader.ReadUInt32Values()));
        
        return this;
    }
}

