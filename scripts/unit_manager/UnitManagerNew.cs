using Godot;
using Incrememental.resources;
using Incrememental.scripts.entities;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.grids.spatial;
using System.Collections.Generic;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Manages all units in the game with efficient MultiMesh rendering.
/// </summary>
[GlobalClass]
public partial class UnitManagerNew : Node
{
    [Signal]
    public delegate void UnitDiedEventHandler(Variant unit, Node building);

    /// <summary>
    /// Runtime data for each unit type including MultiMesh rendering.
    /// </summary>
    public partial class UnitTypeRuntimeData : GodotObject
    {
        public UnitTypeConfigNew Config { get; set; }
        public MultiMesh MultiMesh { get; set; }
        public MultiMeshInstance3D MultiMeshInstance { get; set; }
        public int AliveCount { get; set; } = 0;
        public int VisualIndex { get; set; } = 0;
    }

    [Export] public Godot.Collections.Array<UnitTypeConfigNew> UnitTypeConfigs { get; set; } = new();
    [Export] public NavigationRegion3D NavigationRegion { get; set; }
    [Export] public SpatialGridManagerNew GridManager { get; set; }
    [Export] public int MaxUnitsUpdatedPerFrame { get; set; } = 100;
    [Export] public float VisualLerpSpeed { get; set; } = 10.0f;

    private Rid _navMap;
    private int _updateIndex = 0;

    private Dictionary<UnitType, UnitTypeRuntimeData> _unitTypesRuntime = new();
    private List<UnitNew> _allUnits = new();
    private List<int> _freeIndices = new();
    private int _aliveCountForAll = 0;

    // Threading (not yet implemented, structure in place)
    private int _threadCount;
    private Dictionary<UnitNew, int> _damageQueue = new();
    private Mutex _damageQueueMutex = new();
    private List<UnitNew> _destroyQueue = new();
    private Mutex _destroyQueueMutex = new();

    public override void _Ready()
    {
        _threadCount = CalculateOptimalThreadCount();
        SetupUnitTypes();
        
        if (GridManager == null)
        {
            GD.PushError($"GridManager not assigned to {Name}");
            return;
        }
        
        SetupNavigation();
    }

    public override void _Process(double delta)
    {
        UpdateVisuals((float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        UpdateLogic((float)delta);
        ProcessDamageQueue();
        ProcessDestroyQueue();
        UpdateNavigationSync();
        UpdateMovement((float)delta);
    }

    /// <summary>
    /// Spawns a new unit of the specified type.
    /// </summary>
    public UnitNew SpawnUnit(
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
        unit.EntityData = new EntityDataNew
        {
            Type = EntityType.Unit,
            Position = position,
            TeamId = teamId,
            IsAlive = true,
            IsTargetable = targetable,
            IsAttackable = attackable
        };

        unit.UnitType = config.UnitType;
        unit.Position = position;
        unit.VisualPosition = position;
        unit.SpawnBuilding = building;
        unit.CachedRuntime = runtime;

        unit.Stats = CalculateStatsWithBuffs(config.DefaultStats, buffs);
        unit.Health = unit.Stats.MaxHealth;

        // Setup navigation agent
        unit.AgentRid = NavigationServer3D.AgentCreate();
        NavigationServer3D.AgentSetMap(unit.AgentRid, _navMap);
        NavigationServer3D.AgentSetRadius(unit.AgentRid, unit.Stats.Radius);
        NavigationServer3D.AgentSetMaxSpeed(unit.AgentRid, unit.Stats.MoveSpeed);
        NavigationServer3D.AgentSetAvoidanceEnabled(unit.AgentRid, true);

        // Assign index
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

        unit.ManagerIndex = index;

        // Register with C# grid manager
        var gridEntity = GridManager.RegisterEntity(unit.EntityData, Variant.CreateFrom(unit));
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

        _freeIndices.Add(index);
        
        // Unregister from C# grid manager
        if (unit.GridEntity != null)
        {
            GridManager.UnregisterEntity(unit.GridEntity);
        }

        if (unit.AgentRid.IsValid)
        {
            NavigationServer3D.FreeRid(unit.AgentRid);
        }

        var runtime = _unitTypesRuntime[unit.UnitType];
        runtime.AliveCount--;
        runtime.MultiMesh.VisibleInstanceCount = runtime.AliveCount;

        EmitSignal(SignalName.UnitDied, Variant.CreateFrom(unit), building);
    }

    /// <summary>
    /// Gets a unit by index (if alive).
    /// </summary>
    public UnitNew GetUnit(int index)
    {
        if (index >= 0 && index < _allUnits.Count)
        {
            var unit = _allUnits[index];
            if (unit.IsAlive)
                return unit;
        }
        return null;
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
    /// Finds the nearest ally to a unit.
    /// </summary>
    public Variant FindNearestAlly(UnitNew unit, float searchRange, EntityType findEntityType = EntityType.Undefined)
    {
        if (findEntityType == EntityType.Undefined)
        {
            return GridManager.GetNearestEntity(unit.Position, searchRange, unit.TeamId, true);
        }
        else
        {
            return GridManager.GetNearestEntityByType(findEntityType, unit.Position, searchRange, unit.TeamId, true);
        }
    }

    /// <summary>
    /// Finds the nearest enemy to a unit.
    /// </summary>
    public Variant FindNearestEnemy(UnitNew unit, float searchRange, EntityType findEntityType = EntityType.Undefined)
    {
        if (findEntityType == EntityType.Undefined)
        {
            return GridManager.GetNearestEntity(unit.Position, searchRange, unit.TeamId, false);
        }
        else
        {
            return GridManager.GetNearestEntityByType(findEntityType, unit.Position, searchRange, unit.TeamId, false);
        }
    }

    /// <summary>
    /// Finds nearby allies to a unit.
    /// </summary>
    public Godot.Collections.Array<Variant> FindNearbyAllies(UnitNew unit, float searchRange, EntityType findEntityType = EntityType.Undefined)
    {
        if (findEntityType == EntityType.Undefined)
        {
            return GridManager.GetNearbyEntities(unit.Position, searchRange, unit.TeamId, true);
        }
        else
        {
            return GridManager.GetNearbyEntitiesByType(findEntityType, unit.Position, searchRange, unit.TeamId, true);
        }
    }

    /// <summary>
    /// Finds nearby enemies to a unit.
    /// </summary>
    public Godot.Collections.Array<Variant> FindNearbyEnemies(UnitNew unit, float searchRange, EntityType findEntityType = EntityType.Undefined)
    {
        if (findEntityType == EntityType.Undefined)
        {
            return GridManager.GetNearbyEntities(unit.Position, searchRange, unit.TeamId, false);
        }
        else
        {
            return GridManager.GetNearbyEntitiesByType(findEntityType, unit.Position, searchRange, unit.TeamId, false);
        }
    }

    private void SetupUnitTypes()
    {
        if (UnitTypeConfigs.Count == 0)
        {
            GD.PushWarning("No unit_type_configs added to manager...");
        }

        foreach (var config in UnitTypeConfigs)
        {
            if (_unitTypesRuntime.ContainsKey(config.UnitType))
            {
                GD.PushError($"Duplicate unit type: {config.UnitType}");
                continue;
            }

            var runtime = new UnitTypeRuntimeData
            {
                Config = config
            };

            SetupMultiMesh(runtime, config);

            _unitTypesRuntime[config.UnitType] = runtime;
        }
    }

    private void SetupMultiMesh(UnitTypeRuntimeData runtime, UnitTypeConfigNew config)
    {
        // Validate config
        if (config.MaxCount <= 0)
        {
            GD.PushError($"UnitTypeConfig for {config.UnitType} has invalid MaxCount: {config.MaxCount}. Setting to 100.");
            config.MaxCount = 100;
        }
        
        if (config.Mesh == null)
        {
            GD.PushError($"UnitTypeConfig for {config.UnitType} has no Mesh assigned!");
        }

        runtime.MultiMesh = new MultiMesh
        {
            Mesh = config.Mesh,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            InstanceCount = config.MaxCount,
            VisibleInstanceCount = 0
        };

        runtime.MultiMeshInstance = new MultiMeshInstance3D
        {
            Multimesh = runtime.MultiMesh
        };

        AddChild(runtime.MultiMeshInstance);
        
        GD.Print($"Setup MultiMesh for {config.UnitType}: MaxCount={config.MaxCount}");
    }

    private UnitStatsNew CalculateStatsWithBuffs(UnitStatsNew baseStats, Dictionary<Variant, float> buffs)
    {
        var finalStats = baseStats.DuplicateStats();
        // TODO: Real buff logic
        return finalStats;
    }

    private void SetupNavigation()
    {
        if (NavigationRegion != null)
        {
            _navMap = NavigationRegion.GetNavigationMap();
        }
    }

    private void UpdateLogic(float delta)
    {
        if (_aliveCountForAll == 0)
            return;

        var framesBetweenUpdates = Mathf.CeilToInt(_aliveCountForAll / (float)MaxUnitsUpdatedPerFrame);
        var compensatedDelta = delta * framesBetweenUpdates;
        var unitsThisFrame = Mathf.Min(MaxUnitsUpdatedPerFrame, _aliveCountForAll);
        var checkedCount = 0;
        var updated = 0;
        var context = CreateLogicContext();
        var startIndex = _updateIndex;

        while (updated < unitsThisFrame && checkedCount < _allUnits.Count)
        {
            var index = (startIndex + checkedCount) % _allUnits.Count;
            var unit = _allUnits[index];

            checkedCount++;

            if (!unit.IsAlive)
                continue;

            unit.PathAge += compensatedDelta;
            unit.UpdateLogic(compensatedDelta, context);

            updated++;
        }

        // Process context damage queue
        _damageQueueMutex.Lock();
        foreach (var kvp in context.DamageQueue)
        {
            var targetUnit = kvp.Key;
            var damage = kvp.Value;
            
            if (!_damageQueue.ContainsKey(targetUnit))
                _damageQueue[targetUnit] = 0;
            _damageQueue[targetUnit] += damage;
        }
        _damageQueueMutex.Unlock();

        // Process context destroy queue
        _destroyQueueMutex.Lock();
        _destroyQueue.AddRange(context.DestroyQueue);
        _destroyQueueMutex.Unlock();

        _updateIndex = (startIndex + checkedCount) % Mathf.Max(_allUnits.Count, 1);
    }

    private void ProcessDamageQueue()
    {
        if (_damageQueue.Count == 0)
            return;

        foreach (var kvp in _damageQueue)
        {
            var target = kvp.Key;
            var damage = kvp.Value;

            target.Health -= damage;

            if (target.Health <= 0)
            {
                target.StartDying();
            }
        }

        _damageQueue.Clear();
    }

    private void ProcessDestroyQueue()
    {
        if (_destroyQueue.Count == 0)
            return;

        foreach (var unit in _destroyQueue)
        {
            DestroyUnit(unit.ManagerIndex);
        }

        _destroyQueue.Clear();
    }

    private void UpdateVisuals(float delta)
    {
        // Reset visual indices
        foreach (var runtime in _unitTypesRuntime.Values)
        {
            runtime.VisualIndex = 0;
        }

        var lerpWeight = Mathf.Clamp(VisualLerpSpeed * delta, 0.0f, 1.0f);

        foreach (var unit in _allUnits)
        {
            if (!unit.IsAlive)
                continue;

            // Cache to reduce property access overhead
            var currentPos = unit.Position;
            var visualPos = unit.VisualPosition;
            unit.VisualPosition = visualPos.Lerp(currentPos, lerpWeight);

            var runtime = unit.CachedRuntime;
            var instanceIdx = runtime.VisualIndex;
            runtime.VisualIndex++;

            var transform = new Transform3D(Basis.Identity, unit.VisualPosition);
            var customData = unit.GetCustomVisualData();
            
            // Batch these calls together
            runtime.MultiMesh.SetInstanceTransform(instanceIdx, transform);
            runtime.MultiMesh.SetInstanceCustomData(instanceIdx, customData);
        }
    }

    private void UpdateNavigationSync()
    {
        foreach (var unit in _allUnits)
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

    private void UpdateMovement(float delta)
    {
        for (int i = 0; i < _allUnits.Count; i++)
        {
            var unit = _allUnits[i];
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
                    GridManager.UpdateEntityPosition(unit.GridEntity, newPos);
                }
            }
            else
            {
                unit.Velocity = Vector3.Zero;
            }
        }
    }

    private UnitLogicContext CreateLogicContext()
    {
        return new UnitLogicContext(this);
    }

    private int CalculateOptimalThreadCount()
    {
        var cpuCount = OS.GetProcessorCount();
        return Mathf.Max(1, Mathf.Min((int)(cpuCount * 0.75f), 8));
    }

    public override void _ExitTree()
    {
        foreach (var unit in _allUnits)
        {
            if (unit.IsAlive)
            {
                if (unit.GridEntity != null)
                {
                    GridManager.UnregisterEntity(unit.GridEntity);
                }
                if (unit.AgentRid.IsValid)
                {
                    NavigationServer3D.FreeRid(unit.AgentRid);
                }
            }
        }
    }
}
