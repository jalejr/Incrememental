using Godot;
using Incrememental.scripts.entities.units;

namespace Incrememental.resources;

/// <summary>
/// Configuration for a specific unit type.
/// </summary>
[GlobalClass]
public partial class UnitTypeConfig : Resource
{
    [Export] public UnitType UnitType { get; set; }
    [Export] public UnitStats DefaultStats { get; set; }
    [Export] public Texture2D Icon { get; set; }
    [Export] public Mesh Mesh { get; set; }
    [Export] public int MaxCount { get; set; }

    /// <summary>
    /// Creates a new instance of the unit for this configuration
    /// </summary>
    public Unit CreateUnit()
    {
        return UnitType switch
        {
            UnitType.Soldier => new MeleeUnit(),
            UnitType.Demon => new MeleeUnit(),
            UnitType.Troll => new MeleeUnit(),
            _ => new Unit() // Base unit for others
        };
    }
}
