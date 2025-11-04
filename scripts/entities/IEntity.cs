using Godot;

namespace Incrememental.scripts.entities;

/// <summary>
/// Core interface for all game entities (units, buildings, etc.).
/// Defines the common contract for spatial queries and game systems.
/// </summary>
public interface IEntity
{
    // Spatial properties
    Vector3 Position { get; set; }
    float Radius { get; set; }
    
    // Team and type
    Team TeamId { get; set; }
    EntityType Type { get; }
    
    // Targeting
    bool IsTargetable { get; set; }
    
    // Lifecycle
    bool IsAlive { get; set; }
}

/// <summary>
/// Interface for entities that can participate in combat.
/// </summary>
public interface ICombatEntity : IEntity
{
    bool IsAttackable { get; set; }
    float Health { get; set; }
    float MaxHealth { get; set; }
    void TakeDamage(float damage, Vector3 position, object context);
}