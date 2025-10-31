using Godot;

namespace Incrememental.scripts.entities;

/// <summary>
/// Core interface for all game entities (units, buildings, etc.).
/// Defines the common contract for spatial queries, combat, and game systems.
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
    bool IsAttackable { get; set; }
    
    // Lifecycle
    bool IsAlive { get; set; }
    
    // Combat
    float Health { get; set; }
    float MaxHealth { get; set; }
}