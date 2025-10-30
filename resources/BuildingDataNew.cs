using Godot;
using Godot.Collections;
using Incrememental.scripts.entities;

namespace Incrememental.resources;

/// <summary>
/// Configuration data for building types.
/// </summary>
[GlobalClass]
public partial class BuildingDataNew : Resource
{
    // Identity
    [ExportGroup("Identity")]
    [Export] public string BuildingId { get; set; } = "name";
    [Export] public string DisplayName { get; set; } = "Name";
    [Export(PropertyHint.MultilineText)] public string Description { get; set; } = "Populate with real description";
    [Export] public Texture2D Icon { get; set; }

    // Placement
    [ExportGroup("Placement")]
    [Export] public Vector2I GridSize { get; set; } = new(2, 2);
    [Export] public int UnlockRadius { get; set; } = 1;
    [Export] public int BuffRadius { get; set; } = 1;
    [Export] public int PlacementCost { get; set; } = 50;

    // Combat
    [ExportGroup("Combat")]
    [Export] public float MaxHealth { get; set; } = 100.0f;
    [Export] public Team TeamId { get; set; }
    [Export] public float Radius { get; set; } = 2.0f;

    // Adjacency Buffs
    [ExportGroup("Adjacency Buffs")]
    [Export] public Array<BuffNew> AdjacentAuraBuffs { get; set; } = new();

    // Meta Progression
    [ExportGroup("Meta Progression")]
    [Export] public string RequiresResearchId { get; set; } = "";

    /// <summary>
    /// Checks if this building is unlocked based on research requirements.
    /// </summary>
    public bool IsUnlocked()
    {
        if (!string.IsNullOrEmpty(RequiresResearchId))
        {
            // TODO: Implement research logic
            return false;
        }
        return true;
    }
}
