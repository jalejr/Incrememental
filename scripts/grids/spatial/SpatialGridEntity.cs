using Godot;
using Incrememental.scripts.entities;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Represents an entity tracked in the spatial grid
/// </summary>
public class SpatialGridEntity
{
    public int EntityId { get; set; }
    public IEntity Entity { get; set; }
    public Vector2I GridCell { get; set; }
    public Vector2I[] OccupiedCells { get; set; } = System.Array.Empty<Vector2I>();
}
