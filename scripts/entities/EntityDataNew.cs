using Godot;

namespace Incrememental.scripts.entities;

/// <summary>
/// Represents core entity data for units and buildings.
/// </summary>
[GlobalClass]
public partial class EntityDataNew : RefCounted
{
    public Vector3 Position;
    public float Radius;
    public Team TeamId;
    public EntityType Type = EntityType.Undefined;
    public bool IsTargetable = true;
    public bool IsAttackable = true;
    public bool IsAlive = true;
    public float Health;
    public float MaxHealth;
}
