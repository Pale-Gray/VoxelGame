using System.Collections.Generic;
using OpenTK.Mathematics;

namespace VoxelGame;

public enum Direction
{
    Top,
    Bottom,
    Left,
    Right,
    Front,
    Back
}

public struct Rectangle
{
    public Vector2 Position;
    public Vector2 Size;

    public Vector2 TopLeft => Position + (0, Size.Y);
    public Vector2 TopRight => Position + Size;
    public Vector2 BottomLeft => Position;
    public Vector2 BottomRight => Position + (Size.X, 0);

    public Rectangle(Vector2 position, Vector2 size)
    {
        Position = position;
        Size = size;
    }
}

public struct Face
{
    public Vector3 Start;
    public Vector3 End;

    public Face(Vector3 min, Vector3 max)
    {
        Start = min;
        End = max;
    }
}

public struct Cube
{
    public Vector3 Start;
    public Vector3 End;
    public Vector3 Rotation;
    public string[] FaceResourceLocations = new string[6];

    public Rectangle TopFace => new Rectangle(Start.Xz, End.Xz - Start.Xz);
    public Rectangle BottomFace => new Rectangle((Start.X, 1.0f - End.Z), End.Xz - Start.Xz);
    public Rectangle FrontFace => new Rectangle(Start.Xy, End.Xy - Start.Xy);
    public Rectangle RightFace => new Rectangle(Start.Zy, End.Zy - Start.Zy);
    public Rectangle BackFace => new Rectangle((1.0f - End.X, Start.Y), End.Xy - Start.Xy);
    public Rectangle LeftFace => new Rectangle((1.0f - End.Z, Start.Y), End.Zy - Start.Zy);

    public Cube() : this(Vector3.Zero, Vector3.One)
    {
        
    }
    
    public Cube(Vector3 start, Vector3 end)
    {
        Start = start;
        End = end;
    }
}

public class BlockModel
{
    private List<Cube> _cubes = new();
    
    public BlockModel AddCube(Cube cube)
    {
        _cubes.Add(cube);
        return this;
    }

    public BlockModel SetTextureFace(int cubeIndex, Direction face, string textureName)
    {
        _cubes[cubeIndex].FaceResourceLocations[(int)face] = textureName;
        return this;
    }
    
    public BlockModel SetTextureSides(int cubeIndex, string textureName)
    {
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Back] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Front] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Left] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Right] = textureName;
        return this;
    }
    
    public BlockModel SetAllTextureFaces(int cubeIndex, string textureName)
    {
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Top] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Bottom] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Back] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Front] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Left] = textureName;
        _cubes[cubeIndex].FaceResourceLocations[(int)Direction.Right] = textureName;
        return this;
    }

    public void AddFace(List<ChunkVertex> data, Direction faceDirection, Vector3i offset, Vector4 lightValues)
    {
        foreach (Cube cube in _cubes)
        {
            Vector3 topLeft;
            Vector3 topRight;
            Vector3 bottomLeft;
            Vector3 bottomRight;

            Rectangle portion;

            switch (faceDirection)
            {
                case Direction.Top:
                    topLeft = (cube.Start.X, cube.End.Y, cube.End.Z);
                    topRight = cube.End;
                    bottomLeft = (cube.Start.X, cube.End.Y, cube.Start.Z);
                    bottomRight = (cube.End.X, cube.End.Y, cube.Start.Z);

                    portion = cube.TopFace;
                    
                    data.AddRange(
                        new ChunkVertex(topLeft + offset, Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Top], portion).TopLeft, lightValues),
                        new ChunkVertex(bottomLeft + offset, Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Top], portion).BottomLeft, lightValues),
                        new ChunkVertex(bottomRight + offset, Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Top], portion).BottomRight, lightValues),
                        new ChunkVertex(topRight + offset, Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Top], portion).TopRight, lightValues)
                    );
                    break;
                case Direction.Bottom:
                    topLeft = cube.Start;
                    topRight = (cube.End.X, cube.Start.Y, cube.Start.Z);
                    bottomLeft = (cube.Start.X, cube.Start.Y, cube.End.Z);
                    bottomRight = (cube.End.X, cube.Start.Y, cube.End.Z);

                    portion = cube.BottomFace;
                    
                    data.AddRange(
                        new ChunkVertex(topLeft + offset, -Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Bottom], portion).TopLeft, lightValues),
                        new ChunkVertex(bottomLeft + offset, -Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Bottom], portion).BottomLeft, lightValues),
                        new ChunkVertex(bottomRight + offset, -Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Bottom], portion).BottomRight, lightValues),
                        new ChunkVertex(topRight + offset, -Vector3.UnitY, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Bottom], portion).TopRight, lightValues)    
                    );
                    break;
                case Direction.Front:
                    topLeft = (cube.Start.X, cube.End.Y, cube.Start.Z);
                    topRight = (cube.End.X, cube.End.Y, cube.Start.Z);
                    bottomLeft = cube.Start;
                    bottomRight = (cube.End.X, cube.Start.Y, cube.Start.Z);

                    portion = cube.FrontFace;
                    
                    data.AddRange(  
                        new ChunkVertex(topLeft + offset, -Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Front], portion).TopLeft, lightValues),
                        new ChunkVertex(bottomLeft + offset, -Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Front], portion).BottomLeft, lightValues),
                        new ChunkVertex(bottomRight + offset, -Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Front], portion).BottomRight, lightValues),
                        new ChunkVertex(topRight + offset, -Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Front], portion).TopRight, lightValues)
                    );
                    break;
                case Direction.Back:
                    topLeft = cube.End;
                    topRight = (cube.Start.X, cube.End.Y, cube.End.Z);
                    bottomLeft = (cube.End.X, cube.Start.Y, cube.End.Z);
                    bottomRight = (cube.Start.X, cube.Start.Y, cube.End.Z);

                    portion = cube.BackFace;
                    
                    data.AddRange(  
                        new ChunkVertex(topLeft + offset, Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Back], portion).TopLeft, lightValues),
                        new ChunkVertex(bottomLeft + offset, Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Back], portion).BottomLeft, lightValues),
                        new ChunkVertex(bottomRight + offset, Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Back], portion).BottomRight, lightValues),
                        new ChunkVertex(topRight + offset, Vector3.UnitZ, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Back], portion).TopRight, lightValues)
                    );
                    break;
                case Direction.Left:
                    topLeft = (cube.Start.X, cube.End.Y, cube.End.Z);
                    topRight = (cube.Start.X, cube.End.Y, cube.Start.Z);
                    bottomLeft = (cube.Start.X, cube.Start.Y, cube.End.Z);
                    bottomRight = (cube.Start.X, cube.Start.Y, cube.Start.Z);

                    portion = cube.LeftFace;
                    
                    data.AddRange(  
                        new ChunkVertex(topLeft + offset, -Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Left], portion).TopLeft, lightValues),
                        new ChunkVertex(bottomLeft + offset, -Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Left], portion).BottomLeft, lightValues),
                        new ChunkVertex(bottomRight + offset, -Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Left], portion).BottomRight, lightValues),
                        new ChunkVertex(topRight + offset, -Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Left], portion).TopRight, lightValues)
                    );
                    break;
                case Direction.Right:
                    topLeft = (cube.End.X, cube.End.Y, cube.Start.Z);
                    topRight = (cube.End.X, cube.End.Y, cube.End.Z);
                    bottomLeft = (cube.End.X, cube.Start.Y, cube.Start.Z);
                    bottomRight = (cube.End.X, cube.Start.Y, cube.End.Z);

                    portion = cube.RightFace;
                    
                    data.AddRange(  
                        new ChunkVertex(topLeft + offset, Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Right], portion).TopLeft, lightValues),
                        new ChunkVertex(bottomLeft + offset, Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Right], portion).BottomLeft, lightValues),
                        new ChunkVertex(bottomRight + offset, Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Right], portion).BottomRight, lightValues),
                        new ChunkVertex(topRight + offset, Vector3.UnitX, Config.Atlas.GetTextureCoordinates(cube.FaceResourceLocations[(int)Direction.Right], portion).TopRight, lightValues)
                    );
                    break;
            }
        }
    }
}