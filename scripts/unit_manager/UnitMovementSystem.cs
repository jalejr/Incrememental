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
    private readonly Rid _navMap;
    private readonly SpatialGridManager _gridManager;
    private readonly ArrayPool<Vector3> _pathPool;
    private readonly System.Threading.ThreadLocal<List<(Unit unit, Vector3 newPosition)>> _threadLocalGridUpdates;
    private readonly System.Collections.Concurrent.ConcurrentBag<List<(Unit, Vector3)>> _allGridUpdateBuffers;

    public UnitMovementSystem(Rid navMap, SpatialGridManager gridManager)
    {
        _navMap = navMap;
        _gridManager = gridManager;
        _pathPool = ArrayPool<Vector3>.Shared;
        
        // Thread-local grid update buffers (no locking needed)
        _allGridUpdateBuffers = new System.Collections.Concurrent.ConcurrentBag<List<(Unit, Vector3)>>();
        _threadLocalGridUpdates = new System.Threading.ThreadLocal<List<(Unit, Vector3)>>(() =>
        {
            var buffer = new List<(Unit, Vector3)>(100); // Pre-sized
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
            if (unit.NavPath != null && unit.NavPath.Length > 0 && unit.NavPath != System.Array.Empty<Vector3>())
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
        foreach (var unit in allUnits)
        {
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
        // Clear all thread-local buffers
        foreach (var buffer in _allGridUpdateBuffers)
        {
            buffer.Clear();
        }
        
        // Parallel movement computation - NO LOCKS
        Parallel.For(0, allUnits.Count, i =>
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
                    _threadLocalGridUpdates.Value.Add((unit, newPos));
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
            foreach (var (unit, newPosition) in buffer)
            {
                _gridManager.UpdateEntityPosition(unit.GridEntity, newPosition);
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
