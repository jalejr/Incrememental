using Godot;
using Incrememental.scripts.unit_manager;

namespace Incrememental.scripts.entities.units;

/// <summary>
/// Melee combat unit that pursues and attacks enemies.
/// </summary>
public partial class MeleeUnitNew : UnitNew
{
    public BehaviorState BehaviorState { get; set; } = BehaviorState.Attacking;
    public Variant TargetEntity { get; set; }
    public float AttackCooldown { get; set; } = 0.0f;

    public MeleeUnitNew()
    {
        PathRecalcInterval = 0.5f;
    }

    protected override void UpdateActiveState(float delta, UnitLogicContext context)
    {
        switch (BehaviorState)
        {
            case BehaviorState.Idle:
                UpdateIdleState(delta, context);
                break;
            case BehaviorState.Pursue:
                UpdatePursueState(delta, context);
                break;
            case BehaviorState.Attacking:
                UpdateAttackingState(delta, context);
                break;
        }
    }

    /// <summary>
    /// Update when unit is idle.
    /// </summary>
    protected virtual void UpdateIdleState(float delta, UnitLogicContext context)
    {
        // Override for custom idle behavior
    }

    /// <summary>
    /// Update when unit is pursuing a target.
    /// </summary>
    protected virtual void UpdatePursueState(float delta, UnitLogicContext context)
    {
        // Override for custom pursue behavior
    }

    /// <summary>
    /// Update when unit is in combat.
    /// </summary>
    protected virtual void UpdateAttackingState(float delta, UnitLogicContext context)
    {
        PathAge += delta;
        AttackCooldown -= delta;

        // Cache stats to reduce property access
        var stats = Stats;
        var attackRange = stats.AttackRange;
        var attackDamage = stats.AttackDamage;
        var attackCooldownSec = stats.AttackCooldownSec;
        var currentPos = Position;

        // Check if current target is still valid and in range
        var targetUnit = TargetEntity.Obj != null ? TargetEntity.As<UnitNew>() : null;
        if (targetUnit != null && targetUnit.IsAlive && !targetUnit.IsDying)
        {
            var currentDistance = currentPos.DistanceTo(targetUnit.Position);

            if (currentDistance <= attackRange)
            {
                NavPath = System.Array.Empty<Vector3>();

                if (AttackCooldown <= 0)
                {
                    targetUnit.TakeDamage(attackDamage, currentPos, context);
                    AttackCooldown = attackCooldownSec;
                }
                return;
            }
        }

        // Find new target in attack range
        var nearbyEnemies = context.SpatialGrid.Query()
            .At(currentPos)
            .Within(attackRange)
            .OfTypes(EntityType.Unit, EntityType.Building)
            .ThatAreAlive()
            .ThatAreTargetable()
            .EnemiesOf(TeamId)
            .Limit(1)
            .Execute();
        
        var nearbyEnemyVariant = nearbyEnemies.Count > 0 ? nearbyEnemies[0] : default;

        if (nearbyEnemyVariant.Obj != null)
        {
            var nearbyEnemy = nearbyEnemyVariant.As<UnitNew>();
            if (nearbyEnemy != null && nearbyEnemy.IsAlive && !nearbyEnemy.IsDying)
            {
                TargetEntity = nearbyEnemyVariant;
                NavPath = System.Array.Empty<Vector3>();

                if (AttackCooldown <= 0)
                {
                    nearbyEnemy.TakeDamage(attackDamage, currentPos, context);
                    AttackCooldown = attackCooldownSec;
                }
                return;
            }
        }

        // No target in range, find distant target
        targetUnit = TargetEntity.Obj != null ? TargetEntity.As<UnitNew>() : null;
        if (targetUnit == null || !targetUnit.IsAlive || targetUnit.IsDying)
        {
            var distantEnemies = context.SpatialGrid.Query()
                .At(currentPos)
                .Within(25)
                .OfTypes(EntityType.Unit, EntityType.Building)
                .ThatAreAlive()
                .ThatAreTargetable()
                .EnemiesOf(TeamId)
                .Limit(1)
                .Execute();
            
            TargetEntity = distantEnemies.Count > 0 ? distantEnemies[0] : default;
            if (TargetEntity.Obj != null)
            {
                PathAge = 999.0f; // Force immediate path recalc
            }
        }

        // Still no target, clear path and wait
        targetUnit = TargetEntity.Obj != null ? TargetEntity.As<UnitNew>() : null;
        if (targetUnit == null || !targetUnit.IsAlive || targetUnit.IsDying)
        {
            NavPath = System.Array.Empty<Vector3>();
            return;
        }

        // Have target, update pathfinding
        var targetPos = targetUnit.Position;

        if (NeedsPathRecalc(targetPos))
        {
            context.SetPath(this, targetPos);
            MarkPathRecalculated(targetPos);
        }
        else if (NavPath.Length == 0 || PathIndex >= NavPath.Length)
        {
            if (PathAge > PathRecalcInterval)
            {
                context.SetPath(this, targetPos);
                MarkPathRecalculated(targetPos);
            }
        }
    }

    /// <summary>
    /// Checks if a target entity is valid for attacking.
    /// </summary>
    protected bool IsValidTarget(Variant targetEntity)
    {
        if (targetEntity.Obj == null)
            return false;

        var unit = targetEntity.As<UnitNew>();
        if (unit == null)
            return false;

        return unit.IsAlive && !unit.IsDying;
    }
}
