using Godot;
using Godot.Collections;
using Incrememental.resources;

namespace Incrememental.scripts.entities.buildings;

/// <summary>
/// Base class for all buildings in the game.
/// </summary>
[GlobalClass]
public partial class BuildingNew : Node3D
{
    [Signal]
    public delegate void HealthChangedEventHandler(float newHealth, float maxHealth);

    // Export variables
    [Export] public BuildingDataNew BuildingData { get; set; }

    // Public variables
    public EntityDataNew EntityData { get; private set; }
    public Array<BuffNew> AdjacentAuraBuffs { get; set; } = new();
    public Dictionary<BuildingNew, Array<BuffNew>> ActiveBuffs { get; set; } = new();
    public Dictionary<BuffType, float> CachedBuffsCalculated { get; set; } = new();

    // TODO: Hacky - should use interface
    public bool IsDying = false;

    // Delegating properties to EntityData
    public float Radius
    {
        get => EntityData?.Radius ?? 0.5f;
        set { if (EntityData != null) EntityData.Radius = value; }
    }

    public float MaxHealth
    {
        get => EntityData?.MaxHealth ?? 0.0f;
        set { if (EntityData != null) EntityData.MaxHealth = value; }
    }

    public float Health
    {
        get => EntityData?.Health ?? 0.0f;
        set { if (EntityData != null) EntityData.Health = value; }
    }

    public Team TeamId
    {
        get => EntityData?.TeamId ?? Team.None;
        set { if (EntityData != null) EntityData.TeamId = value; }
    }

    public bool IsAlive
    {
        get => EntityData?.IsAlive ?? false;
        set { if (EntityData != null) EntityData.IsAlive = value; }
    }

    public bool IsTargetable
    {
        get => EntityData?.IsTargetable ?? true;
        set { if (EntityData != null) EntityData.IsTargetable = value; }
    }

    public bool IsAttackable
    {
        get => EntityData?.IsAttackable ?? true;
        set { if (EntityData != null) EntityData.IsAttackable = value; }
    }

    public override void _Ready()
    {
        Initialize();
        Placed();
    }

    /// <summary>
    /// Sets the building data configuration.
    /// </summary>
    public void SetData(BuildingDataNew data)
    {
        BuildingData = data;
    }

    /// <summary>
    /// Applies damage to the building.
    /// </summary>
    public void TakeDamage(float amount, Vector3 position, Dictionary context)
    {
        Health -= amount;
        Health = Mathf.Max(0, Health);
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);

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
        EmitSignal(SignalName.HealthChanged, Health, MaxHealth);
    }

    /// <summary>
    /// Adds adjacency buffs from another building.
    /// </summary>
    public void AddAdjacencyBuffs(BuildingNew sourceBuilding, Array<BuffNew> adjacencyBuffs)
    {
        ActiveBuffs[sourceBuilding] = adjacencyBuffs;
        UpdateActiveBuffs();
    }

    /// <summary>
    /// Removes adjacency buffs from a source building.
    /// </summary>
    public void RemoveAdjacencyBuffs(BuildingNew sourceBuilding)
    {
        ActiveBuffs.Remove(sourceBuilding);
        UpdateActiveBuffs();
    }

    // Private methods
    private void Initialize()
    {
        EntityData = new EntityDataNew
        {
            Type = EntityType.Building,
            Position = GlobalPosition,
            Radius = BuildingData.Radius,
            MaxHealth = BuildingData.MaxHealth,
            Health = BuildingData.MaxHealth,
            TeamId = BuildingData.TeamId,
            IsAlive = true,
            IsAttackable = true,
            IsTargetable = true
        };

        AdjacentAuraBuffs = new Array<BuffNew>(BuildingData.AdjacentAuraBuffs);
    }

    private Array<BuildingNew> FindNearbyBuildings(int cellRadius)
    {
        var foundBuildings = new Array<BuildingNew>();
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
        // EventBusNew.BuildingRemoved.Emit(this); // Uncomment when EventBusNew is set up as autoload
        QueueFree();
    }

    private void UpdateActiveBuffs()
    {
        // TODO: Calculate buff changes
        // TODO: Update UI through signals
    }
}
