using Godot;

namespace Incrememental.scripts.entities;

/// <summary>
/// Immutable snapshot of entity data.
/// Used for multithreading, serialization, and passing entity state around.
/// This is a struct (value type) for zero allocation and safe concurrent access.
/// </summary>
public struct EntityData
{
    public Vector3 Position;
    public float Radius;
    public Team TeamId;
    public EntityType Type;
    public bool IsTargetable;
    public bool IsAttackable;
    public bool IsAlive;
    public float Health;
    public float MaxHealth;
    
    /// <summary>
    /// Create a snapshot from any entity implementing IEntity.
    /// </summary>
    public static EntityData FromEntity(IEntity entity)
    {
        var data = new EntityData
        {
            Position = entity.Position,
            Radius = entity.Radius,
            TeamId = entity.TeamId,
            Type = entity.Type,
            IsTargetable = entity.IsTargetable,
            IsAlive = entity.IsAlive
        };
        
        // Combat properties only exist on ICombatEntity
        if (entity is ICombatEntity combatEntity)
        {
            data.IsAttackable = combatEntity.IsAttackable;
            data.Health = combatEntity.Health;
            data.MaxHealth = combatEntity.MaxHealth;
        }
        
        return data;
    }
    
    /// <summary>
    /// Quick check if this entity can be targeted.
    /// </summary>
    public readonly bool CanBeTargeted() => IsAlive && IsTargetable;
}
