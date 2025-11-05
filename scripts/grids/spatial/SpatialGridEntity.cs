using Godot;
using Incrememental.scripts.entities;
using Incrememental.scripts.grids;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Represents an entity tracked in the spatial grid
/// </summary>
public class SpatialGridEntity
{
    public int EntityId { get; set; }
    public IEntity Entity { get; set; }
    public GridCell GridCell { get; set; }
    public GridCell[] OccupiedCells { get; set; } = System.Array.Empty<GridCell>();
}
