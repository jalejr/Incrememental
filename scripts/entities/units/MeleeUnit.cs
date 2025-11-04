using Godot;
using Incrememental.scripts.unit_manager;

namespace Incrememental.scripts.entities.units;

/// <summary>
/// Melee combat unit that pursues and attacks enemies.
/// </summary>
public partial class MeleeUnit : Unit
{
    public BehaviorState BehaviorState { get; set; } = BehaviorState.Attacking;
    public IEntity TargetEntity { get; set; }
    public float AttackCooldown { get; set; } = 0.0f;

    public MeleeUnit()
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
        if (TargetEntity != null && TargetEntity.IsAlive)
        {
            var currentDistance = currentPos.DistanceTo(TargetEntity.Position);

            if (currentDistance <= attackRange)
            {
                NavPath = System.Array.Empty<Vector3>();

                if (AttackCooldown <= 0 && TargetEntity is ICombatEntity combatTarget)
                {
                    combatTarget.TakeDamage(attackDamage, currentPos, context);
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
        
        var nearbyEnemy = nearbyEnemies.Count > 0 ? nearbyEnemies[0] : null;

        if (nearbyEnemy != null && nearbyEnemy.IsAlive)
        {
            TargetEntity = nearbyEnemy;
            NavPath = System.Array.Empty<Vector3>();

            if (AttackCooldown <= 0 && nearbyEnemy is ICombatEntity combatEnemy)
            {
                combatEnemy.TakeDamage(attackDamage, currentPos, context);
                AttackCooldown = attackCooldownSec;
            }
            return;
        }

        // No target in range, find distant target
        if (TargetEntity == null || !TargetEntity.IsAlive)
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
            
            TargetEntity = distantEnemies.Count > 0 ? distantEnemies[0] : null;
            if (TargetEntity != null)
            {
                PathAge = 999.0f; // Force immediate path recalc
            }
        }

        // Still no target, clear path and wait
        if (TargetEntity == null || !TargetEntity.IsAlive)
        {
            NavPath = System.Array.Empty<Vector3>();
            return;
        }

        // Have target, update pathfinding
        var targetPos = TargetEntity.Position;

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
}
