using Godot;
using Incrememental.resources;
using System.Collections.Generic;
using Incrememental.scripts.global;

namespace Incrememental.scripts.entities.buildings;

/// <summary>
/// Base class for all buildings in the game.
/// Uses C# events instead of Godot signals to avoid marshalling overhead.
/// </summary>
[GlobalClass]
public partial class Building : Node3D, ICombatEntity
{
    // C# event instead of Godot signal
    public event System.Action<float, float> HealthChanged;

    // Export variables
    [Export] public BuildingData BuildingData { get; set; }

    // IEntity implementation - direct properties (no delegation!)
    public EntityType Type => EntityType.Building;
    
    // Buildings sync Position with Godot's GlobalPosition
    // Note: 'new' keyword explicitly hides Node3D.Position (which is Transform.Origin)
    public new Vector3 Position
    {
        get => GlobalPosition;
        set => GlobalPosition = value;
    }
    
    public float Radius { get; set; }
    public Team TeamId { get; set; }
    public bool IsTargetable { get; set; } = true;
    public bool IsAttackable { get; set; } = true;
    public bool IsAlive { get; set; } = true;
    public float Health { get; set; }
    public float MaxHealth { get; set; }

    // Building-specific data
    public List<Buff> AdjacentAuraBuffs { get; set; } = new();
    public Dictionary<Building, List<Buff>> ActiveBuffs { get; set; } = new();
    public Dictionary<BuffType, float> CachedBuffsCalculated { get; set; } = new();
    public bool IsDying = false;

    public override void _Ready()
    {
        Initialize();
        Placed();
    }

    /// <summary>
    /// Sets the building data configuration.
    /// </summary>
    public void SetData(BuildingData data)
    {
        BuildingData = data;
    }

    /// <summary>
    /// Applies damage to the building.
    /// </summary>
    public void TakeDamage(float amount, Vector3 position, object context)
    {
        Health -= amount;
        Health = Mathf.Max(0, Health);
        HealthChanged?.Invoke(Health, MaxHealth);

        if (Health <= 0)
        {
            Killed();
        }
    }

    /// <summary>
    /// Heals the building.
    /// </summary>
    public void Heal(float amount)
    {
        Health += amount;
        Health = Mathf.Min(Health, MaxHealth);
        HealthChanged?.Invoke(Health, MaxHealth);
    }

    /// <summary>
    /// Adds adjacency buffs from another building.
    /// </summary>
    public void AddAdjacencyBuffs(Building sourceBuilding, List<Buff> adjacencyBuffs)
    {
        ActiveBuffs[sourceBuilding] = adjacencyBuffs;
        UpdateActiveBuffs();
    }

    /// <summary>
    /// Removes adjacency buffs from a source building.
    /// </summary>
    public void RemoveAdjacencyBuffs(Building sourceBuilding)
    {
        ActiveBuffs.Remove(sourceBuilding);
        UpdateActiveBuffs();
    }

    // Private methods
    private void Initialize()
    {
        // Initialize IEntity properties directly
        Radius = BuildingData.Radius;
        MaxHealth = BuildingData.MaxHealth;
        Health = BuildingData.MaxHealth;
        TeamId = BuildingData.TeamId;
        IsAlive = true;
        IsAttackable = true;
        IsTargetable = true;

        // Convert Godot.Collections.Array to List<T>
        AdjacentAuraBuffs.Clear();
        foreach (var buff in BuildingData.AdjacentAuraBuffs)
        {
            AdjacentAuraBuffs.Add(buff);
        }
    }

    private List<Building> FindNearbyBuildings(int cellRadius)
    {
        var foundBuildings = new List<Building>();
        // TODO: Logic here - probably need to rework placement grid
        return foundBuildings;
    }

    private void Placed()
    {
        var nearbyBuildings = FindNearbyBuildings(BuildingData.BuffRadius);
        foreach (var building in nearbyBuildings)
        {
            building.AddAdjacencyBuffs(this, AdjacentAuraBuffs);
        }
    }

    private void Killed()
    {
        var nearbyBuildings = FindNearbyBuildings(BuildingData.BuffRadius);
        foreach (var building in nearbyBuildings)
        {
            building.RemoveAdjacencyBuffs(this);
        }

        // TODO: Animation work here
        Destroy();
    }

    private void Destroy()
    {
        // TODO: Other necessary cleanup
        EventBus.Instance.OnBuildingRemoved(this);
        QueueFree();
    }

    private void UpdateActiveBuffs()
    {
        // TODO: Calculate buff changes
        // TODO: Update UI through signals
    }
}
