using Godot;
using Incrememental.resources;
using Incrememental.scripts.entities;
using Incrememental.scripts.global;
using Incrememental.scripts.grids.spatial;
using Incrememental.scripts.unit_manager;

namespace Incrememental.scripts.entities.units;

/// <summary>
/// Base class for all units in the game.
/// </summary>
public partial class UnitNew : RefCounted, ICombatEntity
{
    // IEntity implementation - direct properties (no delegation!)
    public EntityType Type => EntityType.Unit;
    public Vector3 Position { get; set; }
    public float Radius { get; set; }
    public Team TeamId { get; set; }
    public bool IsTargetable { get; set; } = true;
    public bool IsAttackable { get; set; } = true;
    public bool IsAlive { get; set; } = true;
    public float Health { get; set; }
    public float MaxHealth { get; set; }
    
    // Unit-specific data
    public int ManagerIndex { get; set; }
    public UnitType UnitType { get; set; }
    public Vector3 VisualPosition { get; set; }
    public Vector3 Velocity { get; set; }
    public UnitStatsNew Stats { get; set; }

    // Spawning/Death state
    public float SpawnTimer { get; set; } = 0.0f;
    public float SpawnProtectionTime { get; set; } = 1.5f;
    public bool IsDying { get; set; } = false;
    public float DeathTimer { get; set; } = 0.0f;
    public float DeathDuration { get; set; } = 1.5f;

    // Homeless (spawner building destroyed)
    public bool IsHomeless { get; set; } = false;

    // Pathfinding
    public Vector3 CachedTargetPosition { get; set; } = Vector3.Zero;
    public float PathAge { get; set; } = 0.0f;
    public float PathRecalcInterval { get; set; } = 0.5f;

    // Navigation
    public Rid AgentRid { get; set; }
    public Vector3[] NavPath { get; set; } = System.Array.Empty<Vector3>();
    public int PathIndex { get; set; } = 0;

    // Grid tracking - direct reference for performance
    public SpatialGridEntity GridEntity { get; set; }
    public Node SpawnBuilding { get; set; }

    // Caching - direct reference for performance
    public UnitTypeRuntimeData CachedRuntime { get; set; }
    private LifecycleState LifecycleState { get; set; } = LifecycleState.Spawning;

    /// <summary>
    /// Main update loop called by UnitManager.
    /// </summary>
    public virtual void UpdateLogic(float delta, UnitLogicContext context)
    {
        switch (LifecycleState)
        {
            case LifecycleState.Spawning:
                UpdateSpawningState(delta, context);
                break;
            case LifecycleState.Active:
                UpdateActiveState(delta, context);
                break;
            case LifecycleState.Dying:
                UpdateDyingState(delta, context);
                break;
            case LifecycleState.Retreating:
                UpdateRetreatingState(delta, context);
                break;
            case LifecycleState.Dead:
                return;
        }
    }

    /// <summary>
    /// Returns custom visual data for rendering (override in derived classes).
    /// </summary>
    public virtual Color GetCustomVisualData()
    {
        return new Color();
    }

    /// <summary>
    /// Applies damage to the unit.
    /// </summary>
    public virtual void TakeDamage(float damage, Vector3 sourcePos, object context)
    {
        // Add to damage queue
        var actualDamage = Mathf.Max(1, (int)(damage - Stats.Armor));
        
        if (context is UnitLogicContext logicContext)
            logicContext.QueueDamage(this, actualDamage);

        // Emit damage number particle
        var customData = new Color(
            damage,
            0.0f,
            0.0f,
            GD.Randf() * 0.6f - 0.3f
        );
        var transform = new Transform3D(Basis.Identity, Position);
        
        // Call global NumberParticles autoload
        NumberParticlesNew.Instance?.EmitParticle(
            transform,
            Vector3.Zero,
            Colors.White,
            customData,
            1 | 16
        );

        ShowHitEffect(sourcePos);
    }

    /// <summary>
    /// Shows hit effect (override for custom effects).
    /// </summary>
    protected virtual void ShowHitEffect(Vector3 sourcePos)
    {
        // TODO: Implement hit effect
    }

    /// <summary>
    /// Transitions unit to dying state.
    /// </summary>
    public void StartDying()
    {
        LifecycleState = LifecycleState.Dying;
        UpdateFlags();
    }

    /// <summary>
    /// Update during spawning protection period.
    /// </summary>
    protected virtual void UpdateSpawningState(float delta, UnitLogicContext context)
    {
        SpawnTimer += delta;
        if (SpawnTimer >= SpawnProtectionTime)
        {
            LifecycleState = LifecycleState.Active;
            UpdateFlags();
        }
    }

    /// <summary>
    /// Update during active gameplay (override in derived classes).
    /// </summary>
    protected virtual void UpdateActiveState(float delta, UnitLogicContext context)
    {
        // Override in derived classes
    }

    /// <summary>
    /// Update during death animation.
    /// </summary>
    protected virtual void UpdateDyingState(float delta, UnitLogicContext context)
    {
        DeathTimer += delta;

        if (DeathTimer >= DeathDuration)
        {
            context.QueueDestroy(this);
        }
    }

    /// <summary>
    /// Update during retreat to base (override in derived classes).
    /// </summary>
    protected virtual void UpdateRetreatingState(float delta, UnitLogicContext context)
    {
        // Override in derived classes
    }

    /// <summary>
    /// Checks if the unit needs pathfinding recalculation.
    /// </summary>
    public bool NeedsPathRecalc(Vector3 targetPosition, float maxAge = -1.0f, float maxDrift = 5.0f)
    {
        if (maxAge < 0)
            maxAge = PathRecalcInterval;

        if (NavPath.Length == 0)
            return true;

        if (PathAge > maxAge)
            return true;

        if (CachedTargetPosition.DistanceTo(targetPosition) > maxDrift)
            return true;

        return false;
    }

    /// <summary>
    /// Marks that path was just recalculated.
    /// </summary>
    public void MarkPathRecalculated(Vector3 targetPosition)
    {
        CachedTargetPosition = targetPosition;
        PathAge = 0.0f;
    }

    /// <summary>
    /// Updates flags based on lifecycle state.
    /// </summary>
    private void UpdateFlags()
    {
        switch (LifecycleState)
        {
            case LifecycleState.Spawning:
            case LifecycleState.Dead:
            case LifecycleState.Dying:
                IsTargetable = false;
                IsAttackable = false;
                break;
            case LifecycleState.Active:
                IsTargetable = true;
                IsAttackable = true;
                break;
        }
    }
}
