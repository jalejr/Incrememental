namespace Incrememental.scripts.entities;

public interface IDamageable
{
    float Health { get; set; }
    float MaxHealth { get; }
    void TakeDamage(float damage);
    void Heal(float amount);
    bool IsTargetable { get; }
    bool IsInvulnerable { get; }
}