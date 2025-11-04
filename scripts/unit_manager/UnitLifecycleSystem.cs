using Godot;
using Incrememental.resources;
using Incrememental.scripts.entities;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.grids.spatial;
using System.Collections.Generic;
using global::Incrememental.scripts.global;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Handles unit spawning, destruction, and lifecycle management.
/// </summary>
internal class UnitLifecycleSystem
{
    private readonly Rid _navMap;
    private readonly SpatialGridManager _gridManager;
    private readonly Dictionary<UnitType, UnitTypeRuntimeData> _unitTypesRuntime;
    private readonly List<Unit> _allUnits;
    private readonly List<int> _freeIndices;
    private readonly UnitMovementSystem _movementSystem;
    
    private int _aliveCountForAll = 0;

    public int AliveCount => _aliveCountForAll;

    public UnitLifecycleSystem(
        Rid navMap,
        SpatialGridManager gridManager,
        Dictionary<UnitType, UnitTypeRuntimeData> unitTypesRuntime,
        List<Unit> allUnits,
        List<int> freeIndices,
        UnitMovementSystem movementSystem)
    {
        _navMap = navMap;
        _gridManager = gridManager;
        _unitTypesRuntime = unitTypesRuntime;
        _allUnits = allUnits;
        _freeIndices = freeIndices;
        _movementSystem = movementSystem;
    }

    /// <summary>
    /// Spawns a new unit of the specified type.
    /// </summary>
    public Unit SpawnUnit(
        UnitType unitType,
        Team teamId,
        Vector3 position,
        Dictionary<Variant, float> buffs = null,
        Node building = null,
        bool targetable = true,
        bool attackable = true)
    {
        if (!_unitTypesRuntime.ContainsKey(unitType))
        {
            GD.PushError($"Unknown unit type: {unitType}");
            return null;
        }

        var runtime = _unitTypesRuntime[unitType];
        var config = runtime.Config;

        // Use factory to create correct unit type
        var unit = config.CreateUnit();
        
        // Initialize IEntity properties directly
        unit.Position = position;
        unit.TeamId = teamId;
        unit.IsAlive = true;
        unit.IsTargetable = targetable;
        unit.IsAttackable = attackable;
        
        // Unit-specific initialization
        unit.UnitType = config.UnitType;
        unit.VisualPosition = position;
        unit.SpawnBuilding = building;
        unit.CachedRuntime = runtime;

        // Calculate stats with buffs and set health
        unit.Stats = CalculateStatsWithBuffs(config.DefaultStats, buffs);
        unit.Radius = unit.Stats.Radius;
        unit.MaxHealth = unit.Stats.MaxHealth;
        unit.Health = unit.Stats.MaxHealth;

        // Setup navigation agent
        unit.AgentRid = NavigationServer3D.AgentCreate();
        NavigationServer3D.AgentSetMap(unit.AgentRid, _navMap);
        NavigationServer3D.AgentSetRadius(unit.AgentRid, unit.Stats.Radius);
        NavigationServer3D.AgentSetMaxSpeed(unit.AgentRid, unit.Stats.MoveSpeed);
        NavigationServer3D.AgentSetAvoidanceEnabled(unit.AgentRid, true);

        // Assign index
        int index = AssignUnitIndex(unit);
        unit.ManagerIndex = index;

        // Register with spatial grid
        var gridEntity = _gridManager.RegisterEntity(unit);
        unit.GridEntity = gridEntity;

        // Update MultiMesh
        _aliveCountForAll++;
        runtime.AliveCount++;
        
        // Check if we have space in MultiMesh
        if (runtime.AliveCount > runtime.MultiMesh.InstanceCount)
        {
            GD.PushError($"Too many units of type {unitType}! Max: {runtime.MultiMesh.InstanceCount}, Trying to spawn: {runtime.AliveCount}");
            runtime.AliveCount--;
            _aliveCountForAll--;
            return null;
        }
        
        runtime.MultiMesh.VisibleInstanceCount = runtime.AliveCount;

        var instanceIdx = runtime.AliveCount - 1;
        var transform = new Transform3D(Basis.Identity, position);
        runtime.MultiMesh.SetInstanceTransform(instanceIdx, transform);
        runtime.MultiMesh.SetInstanceCustomData(instanceIdx, unit.GetCustomVisualData());

        return unit;
    }

    /// <summary>
    /// Destroys a unit at the specified index.
    /// </summary>
    public void DestroyUnit(int index)
    {
        if (index < 0 || index >= _allUnits.Count)
            return;

        var unit = _allUnits[index];
        var building = unit.SpawnBuilding;

        if (!unit.IsAlive)
            return;

        unit.IsAlive = false;
        _aliveCountForAll--;

        _freeIndices.Add(index);
        
        // Unregister from spatial grid
        if (unit.GridEntity != null)
        {
            _gridManager.UnregisterEntity(unit.GridEntity);
        }

        // Free navigation agent
        if (unit.AgentRid.IsValid)
        {
            NavigationServer3D.FreeRid(unit.AgentRid);
        }
        
        _movementSystem.CleanupUnitPath(unit);

        // Update MultiMesh
        var runtime = _unitTypesRuntime[unit.UnitType];
        runtime.AliveCount--;
        runtime.MultiMesh.VisibleInstanceCount = runtime.AliveCount;

        // Notify
        EventBus.Instance.OnUnitDied(unit, building);
    }

    /// <summary>
    /// Calculates final stats with buffs applied.
    /// </summary>
    private UnitStats CalculateStatsWithBuffs(UnitStats baseStats, Dictionary<Variant, float> buffs)
    {
        var finalStats = baseStats.DuplicateStats();
        
        // TODO: Implement buff system
        // Example:
        // if (buffs != null)
        // {
        //     foreach (var buff in buffs)
        //     {
        //         ApplyBuff(finalStats, buff.Key, buff.Value);
        //     }
        // }
        
        return finalStats;
    }

    /// <summary>
    /// Assigns an index for a new unit, reusing free slots when available.
    /// </summary>
    private int AssignUnitIndex(Unit unit)
    {
        int index;
        if (_freeIndices.Count > 0)
        {
            index = _freeIndices[^1];
            _freeIndices.RemoveAt(_freeIndices.Count - 1);
            _allUnits[index] = unit;
        }
        else
        {
            index = _allUnits.Count;
            _allUnits.Add(unit);
        }
        return index;
    }
}
