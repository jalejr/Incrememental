using System.Buffers;
using Godot;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.grids.spatial;
using System.Collections.Generic;
using System.Threading.Tasks;
using static System.Array;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Handles unit pathfinding, navigation, and movement.
/// </summary>
internal class UnitMovementSystem
{
    private class GridUpdateBuffer
    {
        public GridUpdate[] Array;
        public int Count;
        
        public GridUpdateBuffer(int initialCapacity = 500)
        {
            Array = new GridUpdate[initialCapacity];
            Count = 0;
        }

        public void Add(GridUpdate update)
        {
            if (Count < Array.Length)
            {
                Array[Count++] = update;
            }
            else
            {
                GD.PrintErr($"GridUpdateBuffer overflow! Count: {Count}, Capacity: {Array.Length}");
            }
        }

        public void Clear()
        {
            Count = 0;
        }
    }
    
    private struct GridUpdate
    {
        public Unit Unit;
        public Vector3 NewPosition;

        public GridUpdate(Unit unit, Vector3 newPosition)
        {
            Unit = unit;
            NewPosition = newPosition;
        }
    }
    
    private readonly Rid _navMap;
    private readonly UnitManager _manager;
    private readonly SpatialGridManager _gridManager;
    private readonly ArrayPool<Vector3> _pathPool;
    private readonly System.Threading.ThreadLocal<GridUpdateBuffer> _threadLocalGridUpdates;
    private readonly System.Collections.Concurrent.ConcurrentBag<GridUpdateBuffer> _allGridUpdateBuffers;
    
    public UnitMovementSystem(UnitManager manager, Rid navMap, SpatialGridManager gridManager)
    {
        _manager = manager;
        _navMap = navMap;
        _gridManager = gridManager;
        _pathPool = ArrayPool<Vector3>.Shared;
        
        // Thread-local grid update buffers (no locking needed)
        _allGridUpdateBuffers = new System.Collections.Concurrent.ConcurrentBag<GridUpdateBuffer>();
        _threadLocalGridUpdates = new System.Threading.ThreadLocal<GridUpdateBuffer>(() =>
        {
            var buffer = new GridUpdateBuffer(100000); // Pre-sized
            _allGridUpdateBuffers.Add(buffer);
            return buffer;
        });
    }

    /// <summary>
    /// Sets the navigation path for a unit.
    /// </summary>
    public void SetUnitPath(Unit unit, Vector3 target)
    {
        var godotPath = NavigationServer3D.MapGetPath(_navMap, unit.Position, target, true);
        var pathLength = godotPath.Length;
        
        if (pathLength > 0)
        {
            if (unit.NavPath != null && unit.NavPath.Length > 0 && unit.NavPath != Empty<Vector3>())
            {
                _pathPool.Return(unit.NavPath, clearArray: false);
            }

            var nativePath = _pathPool.Rent(pathLength);
            
            for (int i = 0; i < pathLength; i++)
            {
                nativePath[i] = godotPath[i];
            }
            
            unit.NavPath = nativePath;
            unit.PathLength = pathLength;
        }
        else
        {
            if (unit.NavPath != null && unit.NavPath.Length > 0 && unit.NavPath != Empty<Vector3>())
            {
                _pathPool.Return(unit.NavPath, clearArray: false);
            }
            
            unit.NavPath = Empty<Vector3>();
            unit.PathLength = 0;
        }
        
        unit.PathIndex = 0;
        unit.CachedTargetPosition = target;
        unit.PathAge = 0.0f;
    }

    /// <summary>
    /// Syncs unit positions and velocities with navigation server for avoidance.
    /// </summary>
    public void UpdateNavigationSync(List<Unit> allUnits)
    {
        for (int i = 0; i < allUnits.Count; i++)
        {
            var unit = allUnits[i];
            if (!unit.IsAlive || unit.IsDying)
                continue;

            // Cache to reduce property getter overhead
            var rid = unit.AgentRid;
            var pos = unit.Position;
            var vel = unit.Velocity;
            
            NavigationServer3D.AgentSetPosition(rid, pos);
            NavigationServer3D.AgentSetVelocity(rid, vel);
        }
    }

    /// <summary>
    /// Updates unit movement along paths and updates grid positions.
    /// Uses parallel processing with thread-local grid update buffers (lock-free).
    /// </summary>
    public void UpdateMovement(List<Unit> allUnits, float delta)
    {
        foreach (var buffer in _allGridUpdateBuffers)
        {
            buffer.Clear();
        }
        
        // Parallel movement computation - NO LOCKS
        Parallel.For(0, allUnits.Count, new ParallelOptions 
        { 
            MaxDegreeOfParallelism = _manager.ThreadCount 
        }, i =>
        {
            var unit = allUnits[i];
            if (!unit.IsAlive || unit.IsDying)
                return;

            var safeVelocity = NavigationServer3D.AgentGetVelocity(unit.AgentRid);
            var navPath = unit.NavPath;
            var pathIndex = unit.PathIndex;

            if (navPath.Length > 0 && pathIndex < unit.PathLength)
            {
                var target = navPath[pathIndex];
                var currentPos = unit.Position;
                var distance = currentPos.DistanceTo(target);

                if (distance < 0.5f)
                {
                    pathIndex++;
                    unit.PathIndex = pathIndex;
                    
                    if (pathIndex >= unit.PathLength)
                    {
                        unit.Velocity = Vector3.Zero;
                        return;
                    }
                    else
                    {
                        target = navPath[pathIndex];
                    }
                }

                var direction = (target - currentPos).Normalized();
                var moveSpeed = unit.Stats.MoveSpeed;
                unit.Velocity = direction * moveSpeed;

                safeVelocity.Y = unit.Velocity.Y;
                var newPos = currentPos + safeVelocity * delta;
                unit.Position = newPos;
                
                // Queue grid update to thread-local buffer (no lock!)
                if (unit.GridEntity != null)
                {
                    _threadLocalGridUpdates.Value.Add(new GridUpdate(unit, newPos));
                }
            }
            else
            {
                unit.Velocity = Vector3.Zero;
            }
        });
        
        // Apply grid updates on main thread (single-threaded, no contention)
        ApplyAllGridUpdates();
    }
    
    /// <summary>
    /// Applies all queued grid position updates from all thread buffers on the main thread.
    /// </summary>
    private void ApplyAllGridUpdates()
    {
        foreach (var buffer in _allGridUpdateBuffers)
        {
            var updates = buffer.Array;
            var count = buffer.Count;
        
            for (int j = 0; j < count; j++)
            {
                var update = updates[j];
                _gridManager.UpdateEntityPosition(update.Unit.GridEntity, update.NewPosition);
            }
        }
    }
    
    /// <summary>
    /// Cleans up a unit's path when it's destroyed.
    /// Call this from UnitLifecycleSystem.DestroyUnit()
    /// </summary>
    public void CleanupUnitPath(Unit unit)
    {
        if (unit.NavPath != null && unit.NavPath.Length > 0 && unit.NavPath != Empty<Vector3>())
        {
            _pathPool.Return(unit.NavPath, clearArray: false);
            unit.NavPath = [];
            unit.PathLength = 0;
        }
    }
}
