using Godot;
using Incrememental.scripts.entities.units;

namespace Incrememental.resources;

/// <summary>
/// Configuration for a specific unit type.
/// </summary>
[GlobalClass]
public partial class UnitTypeConfigNew : Resource
{
    [Export] public UnitType UnitType { get; set; }
    [Export] public UnitStatsNew DefaultStats { get; set; }
    [Export] public Texture2D Icon { get; set; }
    [Export] public Mesh Mesh { get; set; }
    [Export] public int MaxCount { get; set; }

    /// <summary>
    /// Creates a new instance of the unit for this configuration.
    /// Uses a factory pattern to determine which unit class to instantiate.
    /// </summary>
    public UnitNew CreateUnit()
    {
        // Factory pattern - customize this based on your unit types
        return UnitType switch
        {
            UnitType.Soldier => new MeleeUnitNew(),
            UnitType.Demon => new MeleeUnitNew(),
            UnitType.Troll => new MeleeUnitNew(),
            _ => new UnitNew() // Base unit for others
        };
    }
}
