using Godot;

namespace Incrememental.scripts.grids.placement;

/// <summary>
/// Data representing a placed building on the placement grid.
/// </summary>
public class PlacementGridData
{
    public Node3D BuildingNode { get; set; }
    public Vector2I GridPosition { get; set; }
    public Vector2I GridSize { get; set; }
    public int UnlockRadius { get; set; }
}
