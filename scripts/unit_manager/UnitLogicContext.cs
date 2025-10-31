using Godot;
using Incrememental.scripts.entities;
using Incrememental.scripts.entities.units;
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
    /// Finds the nearest enemy within search range.
    /// </summary>
    public Variant FindNearestEnemy(UnitNew unit, float searchRange, EntityType entityType = EntityType.Undefined)
    {
        return _manager.FindNearestEnemy(unit, searchRange, entityType);
    }

    /// <summary>
    /// Finds the nearest ally within search range.
    /// </summary>
    public Variant FindNearestAlly(UnitNew unit, float searchRange, EntityType entityType = EntityType.Undefined)
    {
        return _manager.FindNearestAlly(unit, searchRange, entityType);
    }

    /// <summary>
    /// Finds all nearby enemies within search range.
    /// </summary>
    public List<Variant> FindNearbyEnemies(UnitNew unit, float searchRange, EntityType entityType = EntityType.Undefined)
    {
        return _manager.FindNearbyEnemies(unit, searchRange, entityType);
    }

    /// <summary>
    /// Finds all nearby allies within search range.
    /// </summary>
    public List<Variant> FindNearbyAllies(UnitNew unit, float searchRange, EntityType entityType = EntityType.Undefined)
    {
        return _manager.FindNearbyAllies(unit, searchRange, entityType);
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
