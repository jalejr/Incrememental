using Godot;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.grids.spatial;
using System.Collections.Generic;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Handles unit pathfinding, navigation, and movement.
/// </summary>
internal class UnitMovementSystem
{
    private readonly Rid _navMap;
    private readonly SpatialGridManagerNew _gridManager;

    public UnitMovementSystem(Rid navMap, SpatialGridManagerNew gridManager)
    {
        _navMap = navMap;
        _gridManager = gridManager;
    }

    /// <summary>
    /// Sets the navigation path for a unit.
    /// </summary>
    public void SetUnitPath(UnitNew unit, Vector3 target)
    {
        var godotPath = NavigationServer3D.MapGetPath(_navMap, unit.Position, target, true);
        
        // Convert Godot array to native C# array to avoid bridge overhead on every access
        var pathLength = godotPath.Length;
        if (pathLength > 0)
        {
            var nativePath = new Vector3[pathLength];
            for (int i = 0; i < pathLength; i++)
            {
                nativePath[i] = godotPath[i];
            }
            unit.NavPath = nativePath;
        }
        else
        {
            unit.NavPath = System.Array.Empty<Vector3>();
        }
        
        unit.PathIndex = 0;
        unit.CachedTargetPosition = target;
        unit.PathAge = 0.0f;
    }

    /// <summary>
    /// Syncs unit positions and velocities with navigation server for avoidance.
    /// </summary>
    public void UpdateNavigationSync(List<UnitNew> allUnits)
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
    /// </summary>
    public void UpdateMovement(List<UnitNew> allUnits, float delta)
    {
        for (int i = 0; i < allUnits.Count; i++)
        {
            var unit = allUnits[i];
            if (!unit.IsAlive || unit.IsDying)
                continue;

            var safeVelocity = NavigationServer3D.AgentGetVelocity(unit.AgentRid);
            var navPath = unit.NavPath;
            var pathIndex = unit.PathIndex;

            if (navPath.Length > 0 && pathIndex < navPath.Length)
            {
                var target = navPath[pathIndex];
                var currentPos = unit.Position;
                var distance = currentPos.DistanceTo(target);

                if (distance < 0.5f)
                {
                    pathIndex++;
                    unit.PathIndex = pathIndex;
                    
                    if (pathIndex >= navPath.Length)
                    {
                        unit.Velocity = Vector3.Zero;
                        continue;
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
                
                // Update grid position
                if (unit.GridEntity != null)
                {
                    _gridManager.UpdateEntityPosition(unit.GridEntity, newPos);
                }
            }
            else
            {
                unit.Velocity = Vector3.Zero;
            }
        }
    }
}
