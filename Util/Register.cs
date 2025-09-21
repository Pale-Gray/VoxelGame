using System.Collections.Generic;
using VoxelGame.Util;

namespace VoxelGame;

public static class Register
{
    private static Dictionary<string, Block> _blocks = new();
    public static int BlockCount => _blocks.Count;
    
    public static void RegisterBlock(string id, Block block)
    {
        block.Id = id;
        if (!_blocks.TryAdd(id, block))
        {
            Logger.Warning($"A block with the id \"{id}\" already exists. skipping");
        }
        else
        {
            Logger.Info($"Added block \"{id}\"");
        }
    }

    public static Block GetBlockFromId(string name)
    {
        return _blocks[name];
    }
}