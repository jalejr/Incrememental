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
    public GridCell WorldToGrid(Vector3 position)
    {
        return new GridCell(
            Mathf.FloorToInt(position.X / GridCellSize),
            Mathf.FloorToInt(position.Z / GridCellSize)
        );
    }

    /// <summary>
    /// Converts grid coordinates to world position.
    /// </summary>
    public Vector3 GridToWorld(GridCell gridPos, bool centered = true)
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
    public CellsInRadiusEnumerator GetCellsInRadius(GridCell center, int radius)
    {
        return new CellsInRadiusEnumerator(center, radius);
    }

    /// <summary>
    /// Gets all cells for a rectangular area.
    /// Zero-allocation enumerator pattern.
    /// </summary>
    public CellsForAreaEnumerator GetCellsForArea(GridCell gridPos, GridCell size)
    {
        return new CellsForAreaEnumerator(gridPos, size);
    }
    
    /// <summary>
    /// Gets all cells in a ring at a specific distance from center.
    /// Ring 0 returns center cell only. Ring 1 returns 8 surrounding cells, etc.
    /// Zero-allocation enumerator for ring-based spatial searches.
    /// </summary>
    public CellsInRingEnumerator GetCellsInRing(GridCell center, int ringRadius)
    {
        return new CellsInRingEnumerator(center, ringRadius);
    }
    
    /// <summary>
    /// Gets all cells potentially occupied by an entity with given radius.
    /// Entities larger than half a cell size occupy multiple cells.
    /// </summary>
    public GridCell[] GetPotentiallyOccupiedCells(GridCell centerCell, float entityRadius)
    {
        if (entityRadius < GridCellSize * 0.5f)
        {
            return new GridCell[] { centerCell };
        }

        var cellRadius = Mathf.CeilToInt(entityRadius / GridCellSize);
        var cellDiameter = cellRadius * 2 + 1;
        var totalCells = cellDiameter * cellDiameter;
        var occupiedCells = new GridCell[totalCells];
        var index = 0;

        for (int xOffset = -cellRadius; xOffset <= cellRadius; xOffset++)
        {
            for (int zOffset = -cellRadius; zOffset <= cellRadius; zOffset++)
            {
                occupiedCells[index++] = new GridCell(
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
    private readonly GridCell _center;
    private readonly int _radius;
    private int _x;
    private int _y;

    public CellsInRadiusEnumerator(GridCell center, int radius)
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

    public GridCell Current => new GridCell(_center.X + _x, _center.Y + _y);
    
    // Makes foreach work
    public CellsInRadiusEnumerator GetEnumerator() => this;
}

/// <summary>
/// Zero-allocation enumerator for iterating cells in a rectangular area.
/// Generates cells on-demand instead of allocating an array.
/// </summary>
public struct CellsForAreaEnumerator
{
    private readonly GridCell _gridPos;
    private readonly GridCell _size;
    private int _x;
    private int _y;

    public CellsForAreaEnumerator(GridCell gridPos, GridCell size)
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

    public GridCell Current => new GridCell(_gridPos.X + _x, _gridPos.Y + _y);
    
    // Makes foreach work
    public CellsForAreaEnumerator GetEnumerator() => this;
}

/// <summary>
/// Zero-allocation enumerator for ring iteration using GridCell (no Vector2I marshalling).
/// Generates cells on-demand in a hollow square pattern.
/// </summary>
public struct CellsInRingEnumerator
{
    private readonly GridCell _center;
    private readonly int _radius;
    private int _side; // 0=top, 1=bottom, 2=left, 3=right
    private int _offset;
    
    public CellsInRingEnumerator(GridCell center, int radius)
    {
        _center = center;
        _radius = radius;
        _side = radius == 0 ? 4 : 0;
        _offset = radius == 0 ? 0 : -radius - 1;
    }
    
    public bool MoveNext()
    {
        if (_radius == 0)
        {
            if (_side == 4)
            {
                _side = 5;
                return true;
            }
            return false;
        }
        
        _offset++;
        
        while (_side < 4)
        {
            var sideLength = _radius * 2 + 1;
            
            switch (_side)
            {
                case 0: // Top edge
                    if (_offset < sideLength)
                        return true;
                    _offset = -_radius;
                    _side++;
                    break;
                    
                case 1: // Bottom edge
                    if (_offset < sideLength)
                        return true;
                    _offset = -_radius + 1;
                    _side++;
                    break;
                    
                case 2: // Left edge (skip corners)
                    if (_offset < _radius)
                        return true;
                    _offset = -_radius + 1;
                    _side++;
                    break;
                    
                case 3: // Right edge (skip corners)
                    if (_offset < _radius)
                        return true;
                    _side++;
                    break;
            }
        }
        
        return false;
    }
    
    public GridCell Current
    {
        get
        {
            if (_radius == 0)
                return _center;
                
            return _side switch
            {
                0 => new GridCell(_center.X - _radius + _offset, _center.Y - _radius), // Top
                1 => new GridCell(_center.X - _radius + _offset, _center.Y + _radius), // Bottom
                2 => new GridCell(_center.X - _radius, _center.Y + _offset), // Left
                3 => new GridCell(_center.X + _radius, _center.Y + _offset), // Right
                _ => _center
            };
        }
    }
    
    public CellsInRingEnumerator GetEnumerator() => this;
}
