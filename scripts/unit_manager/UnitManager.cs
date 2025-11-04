using Godot;
using Incrememental.resources;
using Incrememental.scripts.entities;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.global;
using Incrememental.scripts.grids.spatial;
using System.Collections.Generic;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Manages all units in the game with efficient MultiMesh rendering
/// </summary>
[GlobalClass]
public partial class UnitManager : Node
{
    [Export] public Godot.Collections.Array<UnitTypeConfig> UnitTypeConfigs { get; set; } = new();
    [Export] public NavigationRegion3D NavigationRegion { get; set; }
    [Export] public SpatialGridManager GridManager { get; set; }
    [Export] public int MaxUnitsUpdatedPerFrame { get; set; } = 100;
    [Export] public float VisualLerpSpeed { get; set; } = 10.0f;

    private Rid _navMap;

    private Dictionary<UnitType, UnitTypeRuntimeData> _unitTypesRuntime = new();
    private List<Unit> _allUnits = new();
    private List<int> _freeIndices = new();

    // Threading (not yet implemented, structure in place)
    private int _threadCount;
    private Dictionary<Unit, int> _damageQueue;
    private Mutex _damageQueueMutex = new();
    private List<Unit> _destroyQueue;
    private Mutex _destroyQueueMutex = new();

    // Subsystems
    private UnitLifecycleSystem _lifecycleSystem;
    private UnitLogicSystem _logicSystem;
    private UnitMovementSystem _movementSystem;
    private UnitRenderSystem _renderSystem;
    
    private System.Threading.ThreadLocal<UnitLogicContext> _threadLocalContext;
    private System.Collections.Concurrent.ConcurrentBag<UnitLogicContext> _activeContexts;

    public override void _Ready()
    {
        _threadCount = CalculateOptimalThreadCount();
        
        if (GridManager == null)
        {
            GD.PushError($"GridManager not assigned to {Name}");
            return;
        }
        
        SetupNavigation();
        
        // Initialize subsystems
        _renderSystem = new UnitRenderSystem(this, UnitTypeConfigs, _unitTypesRuntime, VisualLerpSpeed);
        _movementSystem = new UnitMovementSystem(_navMap, GridManager);
        _lifecycleSystem = new UnitLifecycleSystem(_navMap, GridManager, _unitTypesRuntime, _allUnits, _freeIndices, _movementSystem);
        _logicSystem = new UnitLogicSystem(this);
        
        // Allocating thread-local contexts and queues
        _activeContexts = new System.Collections.Concurrent.ConcurrentBag<UnitLogicContext>();
        
        // Thread-local context for true thread isolation
        _threadLocalContext = new System.Threading.ThreadLocal<UnitLogicContext>(() => 
        {
            var context = new UnitLogicContext(this, MaxUnitsUpdatedPerFrame);
            _activeContexts.Add(context); // Track for merging
            return context;
        });
        
        _damageQueue = new Dictionary<Unit, int>(MaxUnitsUpdatedPerFrame);
        _destroyQueue = new List<Unit>(MaxUnitsUpdatedPerFrame / 2);
        
        // Pre-warm thread pool to create worker threads and initialize ThreadLocal contexts
        PreWarmThreadPool();
    }
    
    /// <summary>
    /// Pre-warms the thread pool by forcing worker thread creation and ThreadLocal initialization.
    /// This prevents first-frame allocation spikes.
    /// </summary>
    private void PreWarmThreadPool()
    {
        GD.Print($"Pre-warming thread pool with {_threadCount} workers...");
        
        // Force thread pool to create workers by running empty parallel work
        System.Threading.Tasks.Task.Run(() =>
        {
            System.Threading.Tasks.Parallel.For(0, _threadCount * 2, new System.Threading.Tasks.ParallelOptions
            {
                MaxDegreeOfParallelism = _threadCount
            }, _ =>
            {
                // Touch ThreadLocal to force initialization
                var context = GetContextForThread();
                
                // Touch grid manager's ThreadLocal query pool
                var query = GridManager.Query();
                GridManager.ReturnQueryToPool(query);
            });
        }).Wait(); // Block until pre-warming completes
        
        GD.Print($"Thread pool pre-warmed. Active contexts: {_activeContexts.Count}");
    }

    public override void _Process(double delta)
    {
        _renderSystem.UpdateVisuals(_allUnits, _unitTypesRuntime, (float)delta);
    }

    public override void _PhysicsProcess(double delta)
    {
        _logicSystem.Update((float)delta, _allUnits, _lifecycleSystem.AliveCount, MaxUnitsUpdatedPerFrame, 
            _damageQueue, _destroyQueue, _damageQueueMutex, _destroyQueueMutex);
        ProcessDamageQueue();
        ProcessDestroyQueue();
        _movementSystem.UpdateNavigationSync(_allUnits);
        _movementSystem.UpdateMovement(_allUnits, (float)delta);
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
        return _lifecycleSystem.SpawnUnit(unitType, teamId, position, buffs, building, targetable, attackable);
    }

    /// <summary>
    /// Destroys a unit at the specified index.
    /// </summary>
    public void DestroyUnit(int index)
    {
        _lifecycleSystem.DestroyUnit(index);
    }
    
    /// <summary>
    /// Sets the navigation path for a unit.
    /// </summary>
    public void SetUnitPath(Unit unit, Vector3 target)
    {
        _movementSystem.SetUnitPath(unit, target);
    }
    
    private void SetupNavigation()
    {
        if (NavigationRegion != null)
        {
            _navMap = NavigationRegion.GetNavigationMap();
        }
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
    
    private int CalculateOptimalThreadCount()
    {
        var cpuCount = OS.GetProcessorCount();
        return Mathf.Max(1, Mathf.Min((int)(cpuCount * 0.75f), 8));
    }
    
    /// <summary>
    /// Gets context for current thread. Each thread gets its own context.
    /// Uses ThreadLocal for guaranteed isolation (lock-free).
    /// </summary>
    internal UnitLogicContext GetContextForThread()
    {
        var context = _threadLocalContext.Value;
        context.Clear();
        return context;
    }
    
    /// <summary>
    /// Gets all contexts for merging results after parallel work.
    /// </summary>
    internal System.Collections.Concurrent.ConcurrentBag<UnitLogicContext> GetAllContexts()
    {
        return _activeContexts;
    }
    
    /// <summary>
    /// Thread count for parallel operations.
    /// </summary>
    public int ThreadCount => _threadCount;
    
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
