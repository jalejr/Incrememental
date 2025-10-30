using Godot;
using Godot.Collections;
using Incrememental.resources;

namespace Incrememental.scripts.entities.buildings;

/// <summary>
/// Building that spawns units at regular intervals.
/// </summary>
[GlobalClass]
public partial class SpawnerBuildingNew : BuildingNew
{
    [Export] public SpawnerDataNew SpawnerData { get; set; }
    [Export] public Array<Node3D> SpawnPoints { get; set; } = new();

    private Node _unitManager;
    private Array<Variant> _spawnedUnits = new();
    private Timer _spawnTimer;
    private int _nextSpawnPointIndex = 0;

    public override void _Ready()
    {
        base._Ready();

        // Get UnitManager from parent (GDScript node)
        _unitManager = GetNode<Node>("../UnitManager");
        if (_unitManager == null)
        {
            GD.PushWarning($"No unit manager assigned to building: {Name}");
            return;
        }

        // Connect to unit_died signal (GDScript signal)
        _unitManager.Connect("unit_died", Callable.From((Variant unit, Variant building) => 
            OnManagerSaysUnitDied(unit, building)));

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
        // Outdated unit_manager logic
        foreach (var unit in _spawnedUnits)
        {
            if (unit.Obj != null)
            {
                // Call GDScript method
                var units = _unitManager.Get("units");
                if (units.Obj != null)
                {
                    var index = ((Array)units).IndexOf(unit);
                    if (index >= 0)
                    {
                        _unitManager.Call("kill_unit", index);
                    }
                }
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

        // Call GDScript UnitManager.spawn_unit method
        // spawn_unit(unit_type, team_id, position, buffs, building)
        var unitData = _unitManager.Call(
            "spawn_unit",
            (int)SpawnerData.UnitType,  // Convert enum to int for GDScript
            (int)TeamId,
            spawnPos,
            CachedBuffsCalculated,
            this
        );

        _spawnedUnits.Add(unitData);
        OnUnitSpawned(unitData, spawnPos);
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

    private void OnManagerSaysUnitDied(Variant unit, Variant building)
    {
        // Check if this building is the one that spawned the unit
        if (building.AsGodotObject() != this)
            return;

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
    protected virtual void OnUnitSpawned(Variant unitData, Vector3 position)
    {
        // Override in derived classes for custom logic
    }

    /// <summary>
    /// Called when a unit dies. Override for custom behavior.
    /// </summary>
    protected virtual void OnUnitDied(Variant unit)
    {
        // Override in derived classes for custom logic
    }
}
