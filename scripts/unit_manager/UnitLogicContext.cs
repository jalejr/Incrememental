using Godot;
using Incrememental.scripts.entities;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.grids.spatial;
using System.Collections.Generic;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Context object passed to units during logic updates.
/// Provides access to manager functions without using Callables.
/// </summary>
public class UnitLogicContext
{
    private readonly UnitManagerNew _manager;
    
    /// <summary>
    /// Direct access to spatial grid for Query Builder pattern.
    /// Example: context.SpatialGrid.Query().At(pos).Within(range).OfType(EntityType.Unit).EnemiesOf(team).Execute()
    /// </summary>
    public SpatialGridManagerNew SpatialGrid { get; }
    
    /// <summary>
    /// Queue for accumulated damage to be applied at end of frame.
    /// Key: Target unit, Value: Total damage amount.
    /// </summary>
    public Dictionary<UnitNew, int> DamageQueue { get; }
    
    /// <summary>
    /// Queue for units to be destroyed at end of frame.
    /// </summary>
    public List<UnitNew> DestroyQueue { get; }

    public UnitLogicContext(UnitManagerNew manager)
    {
        _manager = manager;
        SpatialGrid = manager.GridManager;
        DamageQueue = new Dictionary<UnitNew, int>();
        DestroyQueue = new List<UnitNew>();
    }

    /// <summary>
    /// Sets the navigation path for a unit to the target position.
    /// </summary>
    public void SetPath(UnitNew unit, Vector3 target)
    {
        _manager.SetUnitPath(unit, target);
    }


    /// <summary>
    /// Queues damage to be applied to a target unit.
    /// Damage is accumulated and applied at the end of the update frame.
    /// </summary>
    public void QueueDamage(UnitNew target, int damage)
    {
        if (!DamageQueue.ContainsKey(target))
        {
            DamageQueue[target] = 0;
        }
        DamageQueue[target] += damage;
    }

    /// <summary>
    /// Queues a unit to be destroyed at the end of the update frame.
    /// </summary>
    public void QueueDestroy(UnitNew unit)
    {
        DestroyQueue.Add(unit);
    }
}
