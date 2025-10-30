using Godot;
using Godot.Collections;

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

    protected override void UpdateActiveState(float delta, Dictionary context)
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
    protected virtual void UpdateIdleState(float delta, Dictionary context)
    {
        // Override for custom idle behavior
    }

    /// <summary>
    /// Update when unit is pursuing a target.
    /// </summary>
    protected virtual void UpdatePursueState(float delta, Dictionary context)
    {
        // Override for custom pursue behavior
    }

    /// <summary>
    /// Update when unit is in combat.
    /// </summary>
    protected virtual void UpdateAttackingState(float delta, Dictionary context)
    {
        PathAge += delta;
        AttackCooldown -= delta;

        // Check if current target is still valid and in range
        if (IsValidTarget(TargetEntity))
        {
            var targetEntityData = TargetEntity.AsGodotObject().Get("entity_data");
            var targetPosition = targetEntityData.AsGodotObject().Get("position").AsVector3();
            var currentDistance = Position.DistanceTo(targetPosition);

            if (currentDistance <= Stats.AttackRange)
            {
                NavPath = System.Array.Empty<Vector3>();

                if (AttackCooldown <= 0)
                {
                    GD.Print($"Attacker: {this}, Defender: {TargetEntity}");
                    GD.Print($"Team Id: {TeamId}");
                    
                    // Call take_damage on target
                    TargetEntity.AsGodotObject().Call("take_damage", Stats.AttackDamage, Position, context);
                    AttackCooldown = Stats.AttackCooldownSec;
                }
                return;
            }
        }

        // Find new target in attack range
        var findNearestEnemy = context["find_nearest_enemy"].AsCallable();
        var nearbyEnemy = findNearestEnemy.Call(this, Stats.AttackRange);

        if (nearbyEnemy.Obj != null)
        {
            var isAlive = nearbyEnemy.AsGodotObject().Get("is_alive").AsBool();
            if (isAlive)
            {
                TargetEntity = nearbyEnemy;
                NavPath = System.Array.Empty<Vector3>();

                if (AttackCooldown <= 0)
                {
                    nearbyEnemy.AsGodotObject().Call("take_damage", Stats.AttackDamage, Position, context);
                    AttackCooldown = Stats.AttackCooldownSec;
                }
                return;
            }
        }

        // No target in range, find distant target
        if (!IsValidTarget(TargetEntity))
        {
            TargetEntity = findNearestEnemy.Call(this, 25);
            if (TargetEntity.Obj != null)
            {
                PathAge = 999.0f; // Force immediate path recalc
            }
        }

        // Still no target, clear path and wait
        if (!IsValidTarget(TargetEntity))
        {
            NavPath = System.Array.Empty<Vector3>();
            return;
        }

        // Have target, update pathfinding
        var targetEntityData2 = TargetEntity.AsGodotObject().Get("entity_data");
        var targetPos = targetEntityData2.AsGodotObject().Get("position").AsVector3();

        if (NeedsPathRecalc(targetPos))
        {
            var setPath = context["set_path"].AsCallable();
            setPath.Call(this, targetPos);
            MarkPathRecalculated(targetPos);
        }
        else if (NavPath.Length == 0 || PathIndex >= NavPath.Length)
        {
            if (PathAge > PathRecalcInterval)
            {
                var setPath = context["set_path"].AsCallable();
                setPath.Call(this, targetPos);
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

        var obj = targetEntity.AsGodotObject();
        var isAlive = obj.Get("is_alive").AsBool();
        var isDying = obj.Get("is_dying").AsBool();

        return isAlive && !isDying;
    }
}
