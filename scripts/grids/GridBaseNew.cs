using Godot;
using Godot.Collections;

namespace Incrememental.scripts.grids;

/// <summary>
/// Base class for grid systems with spatial cell calculations.
/// </summary>
[GlobalClass]
public partial class GridBaseNew : Node
{
    [Export] public float GridCellSize { get; set; } = 5.0f;

    /// <summary>
    /// Converts a world position to grid coordinates.
    /// </summary>
    public Vector2I WorldToGrid(Vector3 position)
    {
        return new Vector2I(
            Mathf.FloorToInt(position.X / GridCellSize),
            Mathf.FloorToInt(position.Z / GridCellSize)
        );
    }

    /// <summary>
    /// Converts grid coordinates to world position.
    /// </summary>
    public Vector3 GridToWorld(Vector2I gridPos, bool centered = true)
    {
        var offset = centered ? GridCellSize * 0.5f : 0.0f;
        return new Vector3(
            gridPos.X * GridCellSize + offset,
            0,
            gridPos.Y * GridCellSize + offset
        );
    }

    /// <summary>
    /// Gets all cells within a radius of a center cell.
    /// Returns native C# array for performance.
    /// </summary>
    public Vector2I[] GetCellsInRadius(Vector2I center, int radius)
    {
        var diameter = radius * 2 + 1;
        var totalCells = diameter * diameter;
        var cells = new Vector2I[totalCells];
        var index = 0;
        
        for (int x = -radius; x <= radius; x++)
        {
            for (int y = -radius; y <= radius; y++)
            {
                cells[index++] = center + new Vector2I(x, y);
            }
        }
        return cells;
    }

    /// <summary>
    /// Gets all cells for a rectangular area.
    /// Returns native C# array for performance.
    /// </summary>
    public Vector2I[] GetCellsForArea(Vector2I gridPos, Vector2I size)
    {
        var totalCells = size.X * size.Y;
        var cells = new Vector2I[totalCells];
        var index = 0;
        
        for (int x = 0; x < size.X; x++)
        {
            for (int y = 0; y < size.Y; y++)
            {
                cells[index++] = gridPos + new Vector2I(x, y);
            }
        }
        return cells;
    }
}
