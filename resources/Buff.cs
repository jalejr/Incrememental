using Godot;

namespace Incrememental.resources;

/// <summary>
/// Represents a buff that modifies entity stats.
/// </summary>
[GlobalClass]
public partial class Buff : Resource
{
    [Export] public BuffType Type { get; set; } = BuffType.AttackDamage;
    [Export] public float Value { get; set; } = 0.1f;
}