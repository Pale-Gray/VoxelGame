using System.Collections.Generic;
using OpenTK.Mathematics;

namespace VoxelGame;

public class Entity
{
    public Vector3 PreviousPosition;

    public Vector3 Position;
    public Vector3 Velocity;
    public Vector3 Acceleration;
    
    public BoundingBox BoundingBox = new BoundingBox();
    public PriorityQueue<BoundingBox, float> CollidingBounds = new();
    public bool IsGravityEnabled = false;
    
    public Entity()
    {
        
    }

    public virtual void TickUpdate(World world)
    {
        PreviousPosition = Position;
        
        if (IsGravityEnabled)
        {
            Velocity += new Vector3(0, -9.8f, 0) * Config.TickRate;
            Position += Velocity * Config.TickRate;

            BoundingBox.Position = Position;

            while (CollidingBounds.TryDequeue(out BoundingBox bounds, out float priority))
            {
                
            }
        }
        else
        {
            Velocity = Vector3.Zero;
        }
    }

    public virtual void Update()
    {
        
    }
}