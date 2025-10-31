using Godot;
using Incrememental.scripts.entities;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Represents an entity tracked in the spatial grid.
/// </summary>
public partial class SpatialGridEntity : RefCounted
{
    public int EntityId { get; set; }
    public IEntity Entity { get; set; }  // Interface instead of concrete class!
    public Variant EntityObject { get; set; }  // Store the object reference here
    public Vector2I GridCell { get; set; }
    public Vector2I[] OccupiedCells { get; set; } = System.Array.Empty<Vector2I>();
}
