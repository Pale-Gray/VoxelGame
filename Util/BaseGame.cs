namespace VoxelGame.Util;

public class BaseGame : IMod
{
    public static void OnLoad()
    {
        Register.RegisterBlock("air", new Block() { IsSolid = false });
        Register.RegisterBlock("grass", 
            new Block()
                .SetBlockModel(new BlockModel()
                    .AddCube(new Cube())
                    .SetTextureFace(0, Direction.Top, "grass_top")
                    .SetTextureSides(0, "grass_side")
                    .SetTextureFace(0, Direction.Bottom, "dirt")));
        Register.RegisterBlock("dirt", new Block().SetBlockModel(new BlockModel().AddCube(new Cube()).SetAllTextureFaces(0, "dirt")));
        Register.RegisterBlock("stone", new Block().SetBlockModel(new BlockModel().AddCube(new Cube()).SetAllTextureFaces(0, "stone")));
        Register.RegisterBlock("sand",
            new Block() { IsSolid = true }
                .SetBlockModel(new BlockModel()
                    .AddCube(new Cube((0, 0, 0), (1, 1, 1)))
                    .SetAllTextureFaces(0, "sand")));
        Register.RegisterBlock("pumpkin",
            new Block()
                .SetBlockModel(new BlockModel()
                    .AddCube(new Cube())
                    .SetTextureFace(0, Direction.Top, "pumpkin_top")
                    .SetTextureFace(0, Direction.Bottom, "pumpkin_bottom")
                    .SetTextureSides(0, "pumpkin_face")));
        Register.RegisterBlock("water",
            new Block() { IsTransparent = true }.SetBlockModel(new BlockModel().AddCube(new Cube()).SetAllTextureFaces(0, "water_2")));
        Register.RegisterBlock("lava", new Block() { IsTransparent = true }.SetBlockModel(new BlockModel().AddCube(new Cube()).SetAllTextureFaces(0, "lava")) );
    }
}