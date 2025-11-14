using Godot;
using Incrememental.scripts.entities;
using System.Collections.Generic;
using Incrememental.scripts.entities.buildings;
using Incrememental.scripts.entities.units;
using Incrememental.scripts.global;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Spatial partitioning grid for efficient entity queries.
/// </summary>
[GlobalClass]
public partial class SpatialGridManager : GridBase
{
    [Export] public Vector2 GridWorldSize { get; set; } = new(512.0f, 512.0f);

    private List<SpatialGridEntity>[][] _entityGrid;
    private Vector2I _gridSize = Vector2I.Zero;
    
    private Dictionary<int, SpatialGridEntity> _entityIdToEntity = new();
    private Dictionary<IEntity, SpatialGridEntity> _entityToGridEntity = new();
    private int _nextEntityId = 0;
    
    private readonly System.Threading.ThreadLocal<Stack<SpatialQuery>> _threadLocalQueryPool;
    private const int MaxPooledQueries = 20;
    
    private SpatialQueryExecutor _queryExecutor;
    
    public SpatialGridManager()
    {
        // Initialize thread-local query pools
        _threadLocalQueryPool = new System.Threading.ThreadLocal<Stack<SpatialQuery>>(
            () => new Stack<SpatialQuery>(MaxPooledQueries)
        );
    }
    
    /// <summary>
    /// Gets entities in a cell.
    /// </summary>
    internal List<SpatialGridEntity> GetCellEntities(GridCell cell) => _entityGrid[cell.X][cell.Y];

    public override void _Ready()
    {
        InitializeGrids();
        _queryExecutor = new SpatialQueryExecutor(this);
        
        // Subscribe to all entity lifecycle events
        EventBus.BuildingPlaced += OnBuildingPlaced;
        EventBus.BuildingRemoved += OnBuildingRemoved;
        EventBus.UnitSpawned += OnUnitSpawned;
        EventBus.UnitDied += OnUnitDied;
        
        GD.Print($"SpatialGridManager initialized - Grid size: {_gridSize.X}x{_gridSize.Y}");
    }

    /// <summary>
    /// Unregisters an entity from the spatial grid by its grid entity.
    /// Used by UnitMovementSystem for position updates (performance-critical path).
    /// </summary>
    public void UnregisterEntity(SpatialGridEntity gridEntity)
    {
        if (gridEntity == null)
            return;

        RemoveFromGrid(gridEntity);
        _entityIdToEntity.Remove(gridEntity.EntityId);
        _entityToGridEntity.Remove(gridEntity.Entity);
    }

    /// <summary>
    /// Updates an entity's position in the grid
    /// </summary>
    public void UpdateEntityPosition(SpatialGridEntity entity, Vector3 newPosition)
    {
        var newCell = WorldToGrid(newPosition);

        if (newCell != entity.GridCell)
        {
            RemoveFromGrid(entity);

            entity.GridCell = newCell;
            entity.OccupiedCells = GetPotentiallyOccupiedCells(entity.GridCell, entity.Entity.Radius);

            AddToGrid(entity);
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

    /// <summary>
    /// Checks if GridCell is within bounds.
    /// </summary>
    internal bool IsCellInBounds(GridCell gridPos)
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
    /// Creates a new fluent query builder (from thread-local pool for zero allocations).
    /// Example: grid.Query().At(pos).Within(10f).OfType(EntityType.Unit).Execute()
    /// Thread-safe for concurrent queries from multiple threads (lock-free).
    /// </summary>
    public SpatialQuery Query()
    {
        var pool = _threadLocalQueryPool.Value;
        SpatialQuery query;
        
        // Try to get from thread-local pool (no lock needed!)
        if (pool.Count > 0)
        {
            query = pool.Pop();  // Reuse existing object! ✅
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
    /// Returns a query to the thread-local pool for reuse (called automatically by Execute()).
    /// Thread-safe for concurrent returns from multiple threads (lock-free).
    /// </summary>
    internal void ReturnQueryToPool(SpatialQuery query)
    {
        var pool = _threadLocalQueryPool.Value;
        
        if (pool.Count < MaxPooledQueries)
        {
            pool.Push(query);  // Store for reuse
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
    
    /// <summary>
    /// Registers an entity in the spatial grid (called on main thread via CallDeferred).
    /// PUBLIC for Godot reflection - do not call directly!
    /// </summary>
    public void RegisterEntityDeferred(IEntity entity)
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
        _entityToGridEntity[entity] = gridEntity;
        
        // If it's a unit, set GridEntity for performance-critical position updates
        if (entity is Unit unit)
        {
            unit.GridEntity = gridEntity;
        }
    }
    
    /// <summary>
    /// Unregisters an entity from the spatial grid (called on main thread via CallDeferred).
    /// PUBLIC for Godot reflection - do not call directly!
    /// </summary>
    public void UnregisterEntityDeferred(IEntity entity)
    {
        if (entity == null)
            return;
            
        if (_entityToGridEntity.TryGetValue(entity, out var gridEntity))
        {
            RemoveFromGrid(gridEntity);
            _entityIdToEntity.Remove(gridEntity.EntityId);
            _entityToGridEntity.Remove(gridEntity.Entity);
        }
    }
    
    private void OnBuildingPlaced(Building building, GridCell gridPos)
    {
        // Defer to main thread using Godot's built-in mechanism
        Callable.From(() => RegisterEntityDeferred(building)).CallDeferred();
    }

    private void OnBuildingRemoved(Building building)
    {
        // Defer to main thread using Godot's built-in mechanism
        Callable.From(() => UnregisterEntityDeferred(building)).CallDeferred();
    }
    
    private void OnUnitSpawned(Unit unit, Node building)
    {
        // Defer to main thread using Godot's built-in mechanism
        Callable.From(() => RegisterEntityDeferred(unit)).CallDeferred();
    }
    
    private void OnUnitDied(Unit unit, Node building)
    {
        // Defer to main thread using Godot's built-in mechanism
        Callable.From(() => UnregisterEntityDeferred(unit)).CallDeferred();
    }
}
