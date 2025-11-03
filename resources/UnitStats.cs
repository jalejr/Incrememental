using Godot;

namespace Incrememental.resources;

/// <summary>
/// Stats configuration for a unit type.
/// </summary>
[GlobalClass]
public partial class UnitStats : Resource
{
    [Export] public float Radius { get; set; } = 0.5f;
    [Export] public float MoveSpeed { get; set; } = 4.0f;
    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public float AttackDamage { get; set; } = 500.0f;
    [Export] public float Armor { get; set; } = 0.0f;
    [Export] public float AttackRange { get; set; } = 15.0f;
    [Export] public bool HasSplashDamage { get; set; } = false;
    [Export] public float SplashRadius { get; set; } = 0.0f;
    [Export] public float AttacksPerSecond { get; set; } = 1.0f;
    
    public float AttackCooldownSec { get; private set; } = 1.0f;

    public UnitStats()
    {
        RecalculateAttackCooldown();
    }

    /// <summary>
    /// Sets the attack speed and recalculates cooldown.
    /// </summary>
    public void SetAttackSpeed(float speed)
    {
        AttacksPerSecond = speed;
        RecalculateAttackCooldown();
    }

    /// <summary>
    /// Creates a duplicate of this stats object.
    /// </summary>
    public UnitStats DuplicateStats()
    {
        var newStats = new UnitStats
        {
            Radius = Radius,
            MoveSpeed = MoveSpeed,
            MaxHealth = MaxHealth,
            AttackDamage = AttackDamage,
            Armor = Armor,
            AttackRange = AttackRange,
            HasSplashDamage = HasSplashDamage,
            SplashRadius = SplashRadius
        };
        newStats.SetAttackSpeed(AttacksPerSecond);
        return newStats;
    }

    private void RecalculateAttackCooldown()
    {
        AttackCooldownSec = AttacksPerSecond > 0.0f ? 1.0f / AttacksPerSecond : 1.0f;
    }
}
