using Godot;

namespace Incrememental.scripts.grids;

/// <summary>
/// Base class for grid systems with spatial cell calculations.
/// </summary>
[GlobalClass]
public partial class GridBase : Node
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
    /// Zero-allocation enumerator pattern.
    /// </summary>
    public CellsInRadiusEnumerator GetCellsInRadius(Vector2I center, int radius)
    {
        return new CellsInRadiusEnumerator(center, radius);
    }

    /// <summary>
    /// Gets all cells for a rectangular area.
    /// Zero-allocation enumerator pattern.
    /// </summary>
    public CellsForAreaEnumerator GetCellsForArea(Vector2I gridPos, Vector2I size)
    {
        return new CellsForAreaEnumerator(gridPos, size);
    }
    
    /// <summary>
    /// Gets cells as array (when random access is needed).
    /// Use GetCellsForArea() enumerator when possible for zero allocations.
    /// </summary>
    public Vector2I[] GetCellsForAreaArray(Vector2I gridPos, Vector2I size)
    {
        var totalCells = size.X * size.Y;
        var cells = new Vector2I[totalCells];
        var index = 0;
        
        foreach (var cell in GetCellsForArea(gridPos, size))
        {
            cells[index++] = cell;
        }
        
        return cells;
    }
    
    /// <summary>
    /// Gets all cells in a ring at a specific distance from center.
    /// Ring 0 returns center cell only. Ring 1 returns 8 surrounding cells, etc.
    /// Used for ring-based spatial searches with early exit optimization.
    /// </summary>
    public Vector2I[] GetCellsInRing(Vector2I center, int ringRadius)
    {
        if (ringRadius == 0)
        {
            return new Vector2I[] { center };
        }

        // Calculate size: perimeter of square ring
        var ringSize = (ringRadius * 2 + 1) * 4 - 4; // 4 sides minus 4 corners counted twice
        var cells = new Vector2I[ringSize];
        var index = 0;

        // Top and bottom edges
        for (int x = -ringRadius; x <= ringRadius; x++)
        {
            cells[index++] = new Vector2I(center.X + x, center.Y - ringRadius);
            cells[index++] = new Vector2I(center.X + x, center.Y + ringRadius);
        }

        // Left and right edges (excluding corners already added)
        for (int z = -ringRadius + 1; z < ringRadius; z++)
        {
            cells[index++] = new Vector2I(center.X - ringRadius, center.Y + z);
            cells[index++] = new Vector2I(center.X + ringRadius, center.Y + z);
        }

        return cells;
    }
    
    /// <summary>
    /// Gets all cells potentially occupied by an entity with given radius.
    /// Entities larger than half a cell size occupy multiple cells.
    /// </summary>
    public Vector2I[] GetPotentiallyOccupiedCells(Vector2I centerCell, float entityRadius)
    {
        if (entityRadius < GridCellSize * 0.5f)
        {
            return new Vector2I[] { centerCell };
        }

        var cellRadius = Mathf.CeilToInt(entityRadius / GridCellSize);
        var cellDiameter = cellRadius * 2 + 1;
        var totalCells = cellDiameter * cellDiameter;
        var occupiedCells = new Vector2I[totalCells];
        var index = 0;

        for (int xOffset = -cellRadius; xOffset <= cellRadius; xOffset++)
        {
            for (int zOffset = -cellRadius; zOffset <= cellRadius; zOffset++)
            {
                occupiedCells[index++] = new Vector2I(
                    centerCell.X + xOffset,
                    centerCell.Y + zOffset
                );
            }
        }

        return occupiedCells;
    }
}

/// <summary>
/// Zero-allocation enumerator for iterating cells in a radius.
/// Generates cells on-demand instead of allocating an array.
/// </summary>
public struct CellsInRadiusEnumerator
{
    private readonly Vector2I _center;
    private readonly int _radius;
    private int _x;
    private int _y;

    public CellsInRadiusEnumerator(Vector2I center, int radius)
    {
        _center = center;
        _radius = radius;
        _x = -radius;
        _y = -radius - 1;  // Start before first
    }

    public bool MoveNext()
    {
        _y++;
        if (_y > _radius)
        {
            _y = -_radius;
            _x++;
        }
        return _x <= _radius;
    }

    public Vector2I Current => _center + new Vector2I(_x, _y);
    
    // Makes foreach work
    public CellsInRadiusEnumerator GetEnumerator() => this;
}

/// <summary>
/// Zero-allocation enumerator for iterating cells in a rectangular area.
/// Generates cells on-demand instead of allocating an array.
/// </summary>
public struct CellsForAreaEnumerator
{
    private readonly Vector2I _gridPos;
    private readonly Vector2I _size;
    private int _x;
    private int _y;

    public CellsForAreaEnumerator(Vector2I gridPos, Vector2I size)
    {
        _gridPos = gridPos;
        _size = size;
        _x = 0;
        _y = -1;  // Start before first
    }

    public bool MoveNext()
    {
        _y++;
        if (_y >= _size.Y)
        {
            _y = 0;
            _x++;
        }
        return _x < _size.X;
    }

    public Vector2I Current => _gridPos + new Vector2I(_x, _y);
    
    // Makes foreach work
    public CellsForAreaEnumerator GetEnumerator() => this;
}
