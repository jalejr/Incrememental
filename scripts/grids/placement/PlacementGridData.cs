using Godot;
using Incrememental.scripts.grids;

namespace Incrememental.scripts.grids.placement;

/// <summary>
/// Data representing a placed building on the placement grid.
/// </summary>
public class PlacementGridData
{
    public Node3D BuildingNode { get; set; }
    public GridCell GridPosition { get; set; }
    public GridCell GridSize { get; set; }
    public int UnlockRadius { get; set; }
}
