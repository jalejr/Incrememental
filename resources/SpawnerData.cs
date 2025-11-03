using Godot;
using Incrememental.scripts.entities.units;

namespace Incrememental.resources;

/// <summary>
/// Configuration data for spawner buildings.
/// </summary>
[GlobalClass]
public partial class SpawnerData : Resource
{
    [Export] public UnitType UnitType { get; set; } = UnitType.Base;
    [Export] public int MaxUnits { get; set; } = 10;
    [Export] public float SpawnCooldown { get; set; } = 5.0f;
}
