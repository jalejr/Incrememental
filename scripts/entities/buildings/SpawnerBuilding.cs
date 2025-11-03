using Godot;
using Incrememental.resources;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.global;
using Incrememental.scripts.unit_manager;
using System.Collections.Generic;

namespace Incrememental.scripts.entities.buildings;

/// <summary>
/// Building that spawns units at regular intervals.
/// </summary>
[GlobalClass]
public partial class SpawnerBuilding : Building
{
    [Export] public SpawnerData SpawnerData { get; set; }
    [Export] public Godot.Collections.Array<Node3D> SpawnPoints { get; set; } = new();

    private UnitManager _unitManager;
    private List<Unit> _spawnedUnits = new();  // C# List for performance
    private Timer _spawnTimer;
    private int _nextSpawnPointIndex = 0;

    public override void _Ready()
    {
        base._Ready();

        // Get UnitManager from parent (C# node)
        _unitManager = GetNode<UnitManager>("../UnitManager");
        if (_unitManager == null)
        {
            GD.PushWarning($"No unit manager assigned to building: {Name}");
            return;
        }

        // Subscribe to global unit death events via EventBus
        EventBus.Instance.UnitDied += OnUnitDiedFromEventBus;

        // Create and configure spawn timer
        _spawnTimer = new Timer();
        _spawnTimer.WaitTime = SpawnerData.SpawnCooldown;
        _spawnTimer.Timeout += OnSpawnTimerTimeout;
        AddChild(_spawnTimer);

        StartSpawning();
    }

    /// <summary>
    /// Starts the spawning timer.
    /// </summary>
    public void StartSpawning()
    {
        _spawnTimer?.Start();
    }

    /// <summary>
    /// Stops the spawning timer.
    /// </summary>
    public void StopSpawning()
    {
        _spawnTimer?.Stop();
    }

    /// <summary>
    /// Despawns all units spawned by this building.
    /// </summary>
    public void DespawnAllUnits()
    {
        // TODO: Change logic to units leaving
        foreach (var unit in _spawnedUnits)
        {
            if (unit != null && unit.IsAlive)
            {
                _unitManager.DestroyUnit(unit.ManagerIndex);
            }
        }

        _spawnedUnits.Clear();
    }

    private void AttemptSpawning()
    {
        if (_spawnedUnits.Count >= SpawnerData.MaxUnits)
            return;

        var spawnPos = GetNextSpawnPosition();
        if (spawnPos == Vector3.Zero)
        {
            GD.PushWarning($"No valid spawn position for building: {Name}");
            return;
        }

        // Convert buffs dictionary to Variant-keyed dictionary
        var buffsVariant = new Dictionary<Variant, float>();
        foreach (var kvp in CachedBuffsCalculated)
        {
            buffsVariant[Variant.From((int)kvp.Key)] = kvp.Value;
        }

        // Call C# UnitManager.SpawnUnit method
        var unit = _unitManager.SpawnUnit(
            SpawnerData.UnitType,
            TeamId,
            spawnPos,
            buffsVariant,
            this
        );

        if (unit != null)
        {
            _spawnedUnits.Add(unit);
            OnUnitSpawned(unit, spawnPos);
        }
    }

    private Vector3 GetNextSpawnPosition()
    {
        if (SpawnPoints.Count == 0)
        {
            var random = new RandomNumberGenerator();
            return GlobalPosition + new Vector3(
                random.RandfRange(-2, 2),
                0,
                random.RandfRange(-2, 2)
            );
        }

        var spawnPoint = SpawnPoints[_nextSpawnPointIndex];
        _nextSpawnPointIndex = (_nextSpawnPointIndex + 1) % SpawnPoints.Count;

        return spawnPoint.GlobalPosition;
    }

    private void OnUnitDiedFromEventBus(Unit unit, Node building)
    {
        // Check if this building is the one that spawned the unit
        if (building != this)
            return;
        
        //TODO alter to be stable List to avoid GC
        _spawnedUnits.Remove(unit);
        OnUnitDied(unit);
    }

    private void OnSpawnTimerTimeout()
    {
        AttemptSpawning();
    }

    /// <summary>
    /// Called when a unit is spawned. Override for custom behavior.
    /// </summary>
    protected virtual void OnUnitSpawned(Unit unit, Vector3 position)
    {
        // Override in derived classes for custom logic
    }

    /// <summary>
    /// Called when a unit dies. Override for custom behavior.
    /// </summary>
    protected virtual void OnUnitDied(Unit unit)
    {
        // Override in derived classes for custom logic
    }
}
