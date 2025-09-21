using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using LiteNetLib;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using VoxelGame.Util;
using Monitor = System.Threading.Monitor;

namespace VoxelGame.Networking;

public class Server : Networked
{
    public bool IsInternal = false;
    public NetPeer? InternalServerPeer = null;
    
    public Server(string ip, int port) : base(ip, port)
    {
        
    }

    public Server(string settingsFile) : base(settingsFile)
    {
        
    }

    public override void Start()
    {
        if (!IsInternal) BaseGame.OnLoad();
        
        Manager.Start(HostOrIp, string.Empty, Port);
        Console.WriteLine($"Started server at {HostOrIp}:{Port}");

        Config.World = new World();
        Config.World.Generator.Start();
        
        Listener.ConnectionRequestEvent += request =>
        {
            if (Manager.ConnectedPeersCount >= 20)
            {
                request.Reject();
            }
            else
            {
                request.AcceptIfKey("hello");
            }
        };

        Listener.PeerConnectedEvent += peer =>
        {
            if (IsInternal && InternalServerPeer == null)
            {
                Console.WriteLine("internal peer connected");
                InternalServerPeer = peer;
            }
        };

        Listener.PeerDisconnectedEvent += (peer, info) =>
        {
            Console.WriteLine($"Player {ConnectedPlayers[peer].Name} disconnected, reason: {info.Reason}");
            ConnectedPlayers.Remove(peer);
        };
        
        Listener.NetworkReceiveEvent += (fromPeer, dataReader, channel, deliveryMethod) =>
        {
            DataReader reader = new DataReader(dataReader.GetRemainingBytes());
            int t = reader.ReadInt32();
            PacketType type = (PacketType)t;
            switch (type)
            {
                case PacketType.PlayerMove:
                    PlayerMovePacket playerMove = (PlayerMovePacket)new PlayerMovePacket().Deserialize(reader);
                    if (ConnectedPlayers.ContainsKey(fromPeer)) ConnectedPlayers[fromPeer].Position = playerMove.Position;
                    break;
                case PacketType.PlayerJoin:
                    PlayerJoinPacket playerJoin = (PlayerJoinPacket)new PlayerJoinPacket().Deserialize(reader);
                    ConnectedPlayers.Add(fromPeer, new Player(playerJoin.Id.ToString(), playerJoin.Id));
                    Console.WriteLine($"Player {playerJoin.Id} joined");
                    break;
                case PacketType.BlockDestroy:
                    BlockDestroyPacket blockDestroy = (BlockDestroyPacket) new BlockDestroyPacket().Deserialize(reader);
                    Register.GetBlockFromId(blockDestroy.Id).OnBlockDestroy(Config.World, blockDestroy.GlobalBlockPosition);
                    
                    SendPacket(blockDestroy, fromPeer);
                    break;
                case PacketType.BlockPlace:
                    BlockPlacePacket packet = (BlockPlacePacket)new BlockPlacePacket().Deserialize(reader);
                    Register.GetBlockFromId(packet.Id).OnBlockPlace(Config.World, packet.GlobalBlockPosition);
                    
                    SendPacket(packet, fromPeer);
                    break;
            }
            
            dataReader.Recycle();
        };
    }

    public override void TickUpdate()
    {
        
    }

    public override void Join(bool isInternal = false)
    {
        throw new NotImplementedException();
    }

    public override void Disconnect()
    {
        throw new NotImplementedException();
    }

    public override void Update()
    {
        Manager.PollEvents();
        // Config.World.Generator.Poll();
        
        foreach (KeyValuePair<NetPeer, Player> playerPair in ConnectedPlayers)
        {
            Player player = playerPair.Value;

            if (ChunkMath.GlobalToChunk(ChunkMath.PositionToBlockPosition(player.Position)).Xz != player.ChunkPosition)
            {
                Config.World.Generator.GeneratorQueue.Clear();
                Config.World.Generator.MeshQueue.Clear();
                foreach (Player pl in ConnectedPlayers.Values) pl.NeedsToUpdateLoad = true;
            }
            
            if (player.NeedsToUpdateLoad)
            {
                player.NeedsToUpdateLoad = false;
                
                Console.WriteLine("move");
                player.ChunkPosition = ChunkMath.GlobalToChunk(ChunkMath.PositionToBlockPosition(player.Position)).Xz;
                // player.LoadQueue.Clear();
                player.VisitedChunks.Clear();
                // player.LoadQueue.Enqueue(player.ChunkPosition.Value);
                for (int x = -Config.Radius; x <= Config.Radius; x++)
                {
                    for (int z = -Config.Radius; z <= Config.Radius; z++)
                    {
                        player.LoadingQueue.Enqueue(player.ChunkPosition + (x, z), Vector2.Distance((x, z), Vector2i.Zero));
                    }
                }
            }

            while (player.LoadingQueue.TryDequeue(out Vector2i chunkPosition, out float priority))
            {
                if (!Config.World.Chunks.ContainsKey(chunkPosition)) Config.World.Chunks.TryAdd(chunkPosition, new Chunk(chunkPosition));
                if (Config.World.Chunks[chunkPosition].Status != ChunkStatus.Done) Config.World.Generator.GeneratorQueue.Enqueue((chunkPosition.X, chunkPosition.Y, ChunkMath.ChebyshevDistance(chunkPosition, player.ChunkPosition)));
            }
        }
    }

    public override void Stop()
    {
        Manager.Stop();
        Config.World.Generator.Stop();
    }
}