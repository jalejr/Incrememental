using Godot;
using Incrememental.scripts.entities;
using System.Collections.Generic;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Spatial partitioning grid for efficient entity queries.
/// </summary>
[GlobalClass]
public partial class SpatialGridManagerNew : GridBaseNew
{
    [Export] public Vector2 GridWorldSize { get; set; } = new(512.0f, 512.0f);

    // Unified grid - all entities in one place
    private List<SpatialGridEntity>[][] _entityGrid;
    private Vector2I _gridSize = Vector2I.Zero;
    
    private Dictionary<int, SpatialGridEntity> _entityIdToEntity = new();
    private int _nextEntityId = 0;
    
    // Object pooling for queries (zero allocations!)
    private readonly Stack<SpatialQuery> _queryPool = new();
    private const int MaxPooledQueries = 20;  // Prevents unbounded growth
    
    // Query executor (separated for better organization)
    private SpatialQueryExecutor _queryExecutor;
    
    // Internal accessors for query executor
    internal List<SpatialGridEntity> GetCellEntities(Vector2I cell) => _entityGrid[cell.X][cell.Y];

    public override void _Ready()
    {
        InitializeGrids();
        _queryExecutor = new SpatialQueryExecutor(this);
        GD.Print($"SpatialGridManagerNew initialized - Grid size: {_gridSize.X}x{_gridSize.Y}");
    }

    /// <summary>
    /// Registers an entity in the spatial grid.
    /// </summary>
    public SpatialGridEntity RegisterEntity(IEntity entity)
    {
        var gridEntity = new SpatialGridEntity
        {
            Entity = entity,
            EntityId = _nextEntityId++
        };

        gridEntity.GridCell = WorldToGrid(entity.Position);
        gridEntity.OccupiedCells = GetPotentiallyOccupiedCells(gridEntity.GridCell, entity.Radius);

        AddToGrid(gridEntity);
        _entityIdToEntity[gridEntity.EntityId] = gridEntity;

        return gridEntity;
    }

    /// <summary>
    /// Unregisters an entity from the spatial grid by its grid entity.
    /// </summary>
    public void UnregisterEntity(SpatialGridEntity entity)
    {
        if (entity == null)
            return;

        RemoveFromGrid(entity);
        _entityIdToEntity.Remove(entity.EntityId);
    }

    /// <summary>
    /// Updates an entity's position in the grid.
    /// </summary>
    public void UpdateEntityPosition(SpatialGridEntity entity, Vector3 newPosition)
    {
        var newCell = WorldToGrid(newPosition);

        if (newCell != entity.GridCell)
        {
            RemoveFromGrid(entity);

            entity.GridCell = newCell;
            entity.Entity.Position = newPosition;
            entity.OccupiedCells = GetPotentiallyOccupiedCells(entity.GridCell, entity.Entity.Radius);

            AddToGrid(entity);
        }
        else
        {
            entity.Entity.Position = newPosition;
        }
    }


    /// <summary>
    /// Gets debug statistics about the grid.
    /// </summary>
    public Dictionary<string, object> GetGridStats()
    {
        var occupiedCells = 0;
        for (int x = 0; x < _gridSize.X; x++)
        {
            for (int y = 0; y < _gridSize.Y; y++)
            {
                if (_entityGrid[x][y].Count > 0)
                    occupiedCells++;
            }
        }

        return new Dictionary<string, object>
        {
            ["total_entities"] = _entityIdToEntity.Count,
            ["occupied_cells"] = occupiedCells,
            ["cell_size"] = GridCellSize,
            ["grid_dimensions"] = _gridSize
        };
    }

    /// <summary>
    /// Prints grid statistics to console.
    /// </summary>
    public void PrintStats()
    {
        var stats = GetGridStats();
        GD.Print("=== Grid Stats ===");
        GD.Print($"Grid dimensions: {stats["grid_dimensions"]}");
        GD.Print($"Occupied cells: {stats["occupied_cells"]}");
        GD.Print($"Total entities: {stats["total_entities"]}");
        var occupiedCells = (int)stats["occupied_cells"];
        var totalEntities = (int)stats["total_entities"];
        var avg = occupiedCells > 0 ? (float)totalEntities / occupiedCells : 0;
        GD.Print($"Avg entities per cell: {avg}");
    }

    private void InitializeGrids()
    {
        _gridSize.X = Mathf.CeilToInt(GridWorldSize.X / GridCellSize);
        _gridSize.Y = Mathf.CeilToInt(GridWorldSize.Y / GridCellSize);

        _entityGrid = new List<SpatialGridEntity>[_gridSize.X][];

        for (int x = 0; x < _gridSize.X; x++)
        {
            _entityGrid[x] = new List<SpatialGridEntity>[_gridSize.Y];

            for (int y = 0; y < _gridSize.Y; y++)
            {
                _entityGrid[x][y] = new List<SpatialGridEntity>();
            }
        }
    }

    internal bool IsCellInBounds(Vector2I gridPos)
    {
        return gridPos.X >= 0 && gridPos.X < _gridSize.X &&
               gridPos.Y >= 0 && gridPos.Y < _gridSize.Y;
    }

    private void AddToGrid(SpatialGridEntity entity)
    {
        foreach (var cell in entity.OccupiedCells)
        {
            if (!IsCellInBounds(cell))
                continue;

            _entityGrid[cell.X][cell.Y].Add(entity);
        }
    }

    private void RemoveFromGrid(SpatialGridEntity entity)
    {
        foreach (var cell in entity.OccupiedCells)
        {
            if (!IsCellInBounds(cell))
                continue;

            _entityGrid[cell.X][cell.Y].Remove(entity);
        }
    }


    #region Query Builder API
    
    /// <summary>
    /// Creates a new fluent query builder (from object pool for zero allocations).
    /// Example: grid.Query().At(pos).Within(10f).OfType(EntityType.Unit).Execute()
    /// Thread-safe for queries when all Register/Unregister/Update operations are queued to main thread.
    /// </summary>
    public SpatialQuery Query()
    {
        // Try to get from pool
        SpatialQuery query;
        
        if (_queryPool.Count > 0)
        {
            query = _queryPool.Pop();  // Reuse existing object! ✅
        }
        else
        {
            query = new SpatialQuery();  // Create new if pool is empty
        }
        
        // Reset for reuse
        query.Reset(this);
        
        return query;
    }
    
    /// <summary>
    /// Returns a query to the pool for reuse (called automatically by Execute()).
    /// </summary>
    internal void ReturnQueryToPool(SpatialQuery query)
    {
        if (_queryPool.Count < MaxPooledQueries)
        {
            _queryPool.Push(query);  // Store for reuse
        }
        // If pool is full, let query be garbage collected (prevents unbounded growth)
    }
    
    /// <summary>
    /// Internal method to execute a query built by SpatialQuery.
    /// Delegates to the query executor.
    /// </summary>
    internal List<IEntity> ExecuteQuery(SpatialQuery query)
    {
        return _queryExecutor.Execute(query);
    }
    
    #endregion
    
}
