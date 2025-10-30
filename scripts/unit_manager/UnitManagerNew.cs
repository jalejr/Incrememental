using Godot;
using Godot.Collections;
using Incrememental.resources;
using Incrememental.scripts.entities;
using Incrememental.scripts.entities.units;

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

    [Export] public Array<UnitTypeConfigNew> UnitTypeConfigs { get; set; } = new();
    [Export] public NavigationRegion3D NavigationRegion { get; set; }
    [Export] public Node GridManager { get; set; }
    [Export] public int MaxUnitsUpdatedPerFrame { get; set; } = 100;
    [Export] public float VisualLerpSpeed { get; set; } = 10.0f;

    private Rid _navMap;
    private int _updateIndex = 0;

    private Dictionary<UnitType, UnitTypeRuntimeData> _unitTypesRuntime = new();
    private System.Collections.Generic.List<UnitNew> _allUnits = new();
    private System.Collections.Generic.List<int> _freeIndices = new();
    private int _aliveCountForAll = 0;

    // Threading (not yet implemented, structure in place)
    private int _threadCount;
    private Dictionary<Variant, int> _damageQueue = new();
    private Mutex _damageQueueMutex = new();
    private System.Collections.Generic.List<Variant> _destroyQueue = new();
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
        unit.CachedRuntime = Variant.CreateFrom(runtime);

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

        // Register with grid manager (GDScript - for now we skip this, will need GDScript EntityData wrapper)
        // TODO: Create GDScript EntityData wrapper or convert SpatialGridManager to C#
        // GridManager.Call("register_entity", unit.EntityData, Variant.CreateFrom(unit));

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
        
        // TODO: Unregister from GDScript grid manager when we have proper interop
        // GridManager.Call("unregister_entity", Variant.CreateFrom(unit));

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
        var path = NavigationServer3D.MapGetPath(_navMap, unit.Position, target, true);
        unit.NavPath = path;
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
            return GridManager.Call("get_nearest_entity", unit.Position, searchRange, (int)unit.TeamId, true);
        }
        else
        {
            return GridManager.Call("get_nearest_entity_by_type", (int)findEntityType, unit.Position, searchRange, (int)unit.TeamId, true);
        }
    }

    /// <summary>
    /// Finds the nearest enemy to a unit.
    /// </summary>
    public Variant FindNearestEnemy(UnitNew unit, float searchRange, EntityType findEntityType = EntityType.Undefined)
    {
        if (findEntityType == EntityType.Undefined)
        {
            return GridManager.Call("get_nearest_entity", unit.Position, searchRange, (int)unit.TeamId, false);
        }
        else
        {
            return GridManager.Call("get_nearest_entity_by_type", (int)findEntityType, unit.Position, searchRange, (int)unit.TeamId, false);
        }
    }

    /// <summary>
    /// Finds nearby allies to a unit.
    /// </summary>
    public Array<Variant> FindNearbyAllies(UnitNew unit, float searchRange, EntityType findEntityType = EntityType.Undefined)
    {
        if (findEntityType == EntityType.Undefined)
        {
            return GridManager.Call("get_nearby_entities", unit.Position, searchRange, (int)unit.TeamId, true).As<Array<Variant>>();
        }
        else
        {
            return GridManager.Call("get_nearby_entities_by_type", (int)findEntityType, unit.Position, searchRange, (int)unit.TeamId, true).As<Array<Variant>>();
        }
    }

    /// <summary>
    /// Finds nearby enemies to a unit.
    /// </summary>
    public Array<Variant> FindNearbyEnemies(UnitNew unit, float searchRange, EntityType findEntityType = EntityType.Undefined)
    {
        if (findEntityType == EntityType.Undefined)
        {
            return GridManager.Call("get_nearby_entities", unit.Position, searchRange, (int)unit.TeamId, false).As<Array<Variant>>();
        }
        else
        {
            return GridManager.Call("get_nearby_entities_by_type", (int)findEntityType, unit.Position, searchRange, (int)unit.TeamId, false).As<Array<Variant>>();
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
        var contextDamageQueue = context["damage_queue"].As<Dictionary>();
        foreach (var targetKey in contextDamageQueue.Keys)
        {
            var damage = contextDamageQueue[targetKey].AsInt32();

            if (!_damageQueue.ContainsKey(targetKey))
                _damageQueue[targetKey] = 0;
            _damageQueue[targetKey] += damage;
        }
        _damageQueueMutex.Unlock();

        // Process context destroy queue
        _destroyQueueMutex.Lock();
        var contextDestroyQueue = context["destroy_queue"].As<Array>();
        foreach (var item in contextDestroyQueue)
        {
            _destroyQueue.Add(item);
        }
        _destroyQueueMutex.Unlock();

        _updateIndex = (startIndex + checkedCount) % Mathf.Max(_allUnits.Count, 1);
    }

    private void ProcessDamageQueue()
    {
        if (_damageQueue.Count == 0)
            return;

        foreach (var targetKey in _damageQueue.Keys)
        {
            var damage = _damageQueue[targetKey];
            var target = targetKey.As<UnitNew>();

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

        foreach (var target in _destroyQueue)
        {
            var unit = target.As<UnitNew>();
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

            unit.VisualPosition = unit.VisualPosition.Lerp(unit.Position, lerpWeight);

            var runtime = unit.CachedRuntime.As<UnitTypeRuntimeData>();
            var instanceIdx = runtime.VisualIndex;
            runtime.VisualIndex++;

            var transform = new Transform3D(Basis.Identity, unit.VisualPosition);
            runtime.MultiMesh.SetInstanceTransform(instanceIdx, transform);

            var customData = unit.GetCustomVisualData();
            runtime.MultiMesh.SetInstanceCustomData(instanceIdx, customData);
        }
    }

    private void UpdateNavigationSync()
    {
        foreach (var unit in _allUnits)
        {
            if (!unit.IsAlive || unit.IsDying)
                continue;

            NavigationServer3D.AgentSetPosition(unit.AgentRid, unit.Position);
            NavigationServer3D.AgentSetVelocity(unit.AgentRid, unit.Velocity);
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

            if (unit.NavPath.Length > 0 && unit.PathIndex < unit.NavPath.Length)
            {
                var target = unit.NavPath[unit.PathIndex];
                var distance = unit.Position.DistanceTo(target);

                if (distance < 0.5f)
                {
                    unit.PathIndex++;
                    if (unit.PathIndex >= unit.NavPath.Length)
                    {
                        unit.Velocity = Vector3.Zero;
                        continue;
                    }
                    else
                    {
                        target = unit.NavPath[unit.PathIndex];
                    }
                }

                var direction = (target - unit.Position).Normalized();
                unit.Velocity = direction * unit.Stats.MoveSpeed;

                safeVelocity.Y = unit.Velocity.Y;
                unit.Position += safeVelocity * delta;
            }
            else
            {
                unit.Velocity = Vector3.Zero;
            }

            // TODO: Update grid position when we have proper GDScript interop
            // if (unit.GridData.Obj != null)
            // {
            //     var newCell = GridManager.Call("world_to_grid", unit.Position);
            //     var currentCell = unit.GridData.AsGodotObject().Get("grid_cell");
            //     
            //     if (!newCell.Equals(currentCell))
            //     {
            //         GridManager.Call("update_unit_position", unit.GridData, unit.Position);
            //     }
            // }
        }
    }

    private Dictionary CreateLogicContext()
    {
        var context = new Dictionary();
        context["set_path"] = Callable.From((UnitNew unit, Vector3 target) => SetUnitPath(unit, target));
        context["find_nearest_enemy"] = Callable.From((UnitNew unit, float range) => FindNearestEnemy(unit, range));
        context["find_nearest_ally"] = Callable.From((UnitNew unit, float range) => FindNearestAlly(unit, range));
        context["find_nearby_enemies"] = Callable.From((UnitNew unit, float range) => FindNearbyEnemies(unit, range));
        context["find_nearby_allies"] = Callable.From((UnitNew unit, float range) => FindNearbyAllies(unit, range));
        context["damage_queue"] = new Dictionary();
        context["destroy_queue"] = new Array();
        return context;
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
                if (unit.GridData.Obj != null)
                {
                    GridManager.Call("unregister_unit", unit.GridData);
                }
                if (unit.AgentRid.IsValid)
                {
                    NavigationServer3D.FreeRid(unit.AgentRid);
                }
            }
        }
    }
}
