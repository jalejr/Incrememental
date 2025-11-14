using Godot;
using Incrememental.resources;
using System.Collections.Generic;
using Incrememental.scripts.global;
using Incrememental.scripts.grids;

namespace Incrememental.scripts.entities.buildings;

/// <summary>
/// Base class for all buildings in the game
/// </summary>
[GlobalClass]
public partial class Building : Node3D, ICombatEntity
{
    public event System.Action<float, float> HealthChanged;
    [Export] public BuildingData BuildingData { get; set; }
    public EntityType Type => EntityType.Building;
    // Hiding Godot Position property
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
    public List<Buff> AdjacentAuraBuffs { get; set; } = new();
    public Dictionary<Building, List<Buff>> ActiveBuffs { get; set; } = new();
    public Dictionary<BuffType, float> CachedBuffsCalculated { get; set; } = new();
    public bool IsDying = false;


    public override void _Ready()
    {
        Initialize();
    }

    public void Initialize()
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
    private List<Building> FindNearbyBuildingsForBuffs(int cellRadius)
    {
        var buildingNodes = GameSystems.PlacementGrid.GetBuildingsInRadius(this, cellRadius);
        // TODO should look into cleaning Node3D and use an interface
        var buildings = new List<Building>();
        
        foreach (var node in buildingNodes)
        {
            if (node is Building building)
            {
                buildings.Add(building);
            }
        }
        
        return buildings;
    }

    public void Placed()
    {
        var gridSize = GridCell.FromVector2I(BuildingData.GridSize);
        var cellSize = GameSystems.PlacementGrid.GridCellSize;
        var bottomLeftPos = Position - new Vector3(
            gridSize.X * cellSize * 0.5f,
            0,
            gridSize.Y * cellSize * 0.5f
        );
        var gridPos = GameSystems.PlacementGrid.WorldToGrid(bottomLeftPos);
        
        GameSystems.PlacementGrid.PlaceBuilding(
            this,
            gridPos,
            gridSize,
            BuildingData.UnlockRadius
        );
        
        var nearbyBuildings = FindNearbyBuildingsForBuffs(BuildingData.BuffRadius);
        foreach (var building in nearbyBuildings)
        {
            building.AddAdjacencyBuffs(this, AdjacentAuraBuffs);
            AddAdjacencyBuffs(building, building.AdjacentAuraBuffs);
        }
    }

    private void Killed()
    {
        var nearbyBuildings = FindNearbyBuildingsForBuffs(BuildingData.BuffRadius);
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
        EventBus.EmitBuildingRemoved(this);
        QueueFree();
    }

    private void UpdateActiveBuffs()
    {
        // TODO could be made better by doing this on add and remove instead of all at once
        // this would avoid clearing and simplify the math
        CachedBuffsCalculated.Clear();
        
        // Aggregate all buffs from all sources
        foreach (var (sourceBuilding, buffList) in ActiveBuffs)
        {
            foreach (var buff in buffList)
            {
                if (!CachedBuffsCalculated.ContainsKey(buff.Type))
                {
                    CachedBuffsCalculated[buff.Type] = 0f;
                }
                CachedBuffsCalculated[buff.Type] += buff.Value;
            }
        }
        
        // GD.Print($"Building {Name} has {CachedBuffsCalculated} active buffs");
    }
}
