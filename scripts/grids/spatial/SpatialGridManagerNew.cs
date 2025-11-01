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
    
    // Type indexing for fast filtering
    private Dictionary<EntityType, HashSet<SpatialGridEntity>> _entitiesByType = new();
    
    private Dictionary<int, SpatialGridEntity> _entityIdToEntity = new();
    private int _nextEntityId = 0;
    
    // Object pooling for queries (zero allocations!)
    private readonly Stack<SpatialQuery> _queryPool = new();
    private const int MaxPooledQueries = 20;  // Prevents unbounded growth

    public override void _Ready()
    {
        InitializeGrids();
        GD.Print($"SpatialGridManagerNew initialized - Grid size: {_gridSize.X}x{_gridSize.Y}");
    }

    /// <summary>
    /// Registers an entity in the spatial grid.
    /// </summary>
    public SpatialGridEntity RegisterEntity(IEntity entity, Variant entityObject)
    {
        var gridEntity = new SpatialGridEntity
        {
            Entity = entity,
            EntityObject = entityObject,
            EntityId = _nextEntityId++
        };

        gridEntity.GridCell = WorldToGrid(entity.Position);
        gridEntity.OccupiedCells = GetPotentiallyOccupiedCells(gridEntity.GridCell, entity.Radius);

        AddToGrid(gridEntity);
        
        // Add to type index for fast filtering
        if (!_entitiesByType.ContainsKey(entity.Type))
            _entitiesByType[entity.Type] = new HashSet<SpatialGridEntity>();
        _entitiesByType[entity.Type].Add(gridEntity);

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
        
        // Remove from type index
        if (_entitiesByType.TryGetValue(entity.Entity.Type, out var typeSet))
        {
            typeSet.Remove(entity);
            if (typeSet.Count == 0)
                _entitiesByType.Remove(entity.Entity.Type);
        }
        
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
    /// Gets all nearby entities (units and buildings) within radius.
    /// Uses Query Builder internally.
    /// </summary>
    public List<Variant> GetNearbyEntities(Vector3 position, float radius, Team teamId, bool isTargetingAllies)
    {
        var query = Query()
            .At(position)
            .Within(radius)
            .OfTypes(EntityType.Unit, EntityType.Building)
            .ThatAreAlive()
            .ThatAreTargetable();
        
        if (teamId != Team.None)
        {
            if (isTargetingAllies)
                query.AlliesOf(teamId);
            else
                query.EnemiesOf(teamId);
        }
        
        return query.Execute();
    }

    /// <summary>
    /// Gets nearby entities of a specific type within radius.
    /// Uses Query Builder internally.
    /// </summary>
    public List<Variant> GetNearbyEntitiesByType(
        EntityType entityType,
        Vector3 position,
        float radius,
        Team teamId,
        bool isTargetingAllies = false)
    {
        var query = Query()
            .At(position)
            .Within(radius)
            .OfType(entityType)
            .ThatAreAlive()
            .ThatAreTargetable();
        
        if (teamId != Team.None)
        {
            if (isTargetingAllies)
                query.AlliesOf(teamId);
            else
                query.EnemiesOf(teamId);
        }
        
        return query.Execute();
    }

    /// <summary>
    /// Gets the nearest entity (unit or building) within radius.
    /// Uses Query Builder internally.
    /// </summary>
    public Variant GetNearestEntity(Vector3 position, float radius, Team teamId, bool isTargetingAllies)
    {
        var query = Query()
            .At(position)
            .Within(radius)
            .OfTypes(EntityType.Unit, EntityType.Building)
            .ThatAreAlive()
            .ThatAreTargetable()
            .Limit(1);
        
        if (teamId != Team.None)
        {
            if (isTargetingAllies)
                query.AlliesOf(teamId);
            else
                query.EnemiesOf(teamId);
        }
        
        var results = query.Execute();
        return results.Count > 0 ? results[0] : default;
    }

    /// <summary>
    /// Gets the nearest entity of a specific type within radius.
    /// Uses Query Builder internally.
    /// </summary>
    public Variant GetNearestEntityByType(
        EntityType entityType,
        Vector3 position,
        float radius,
        Team teamId,
        bool isTargetingAllies = false)
    {
        var query = Query()
            .At(position)
            .Within(radius)
            .OfType(entityType)
            .ThatAreAlive()
            .ThatAreTargetable()
            .Limit(1);
        
        if (teamId != Team.None)
        {
            if (isTargetingAllies)
                query.AlliesOf(teamId);
            else
                query.EnemiesOf(teamId);
        }
        
        var results = query.Execute();
        return results.Count > 0 ? results[0] : default;
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

    private bool IsCellInBounds(Vector2I gridPos)
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


    private Variant GetEntityObject(SpatialGridEntity entity)
    {
        return entity.EntityObject;
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
    /// Uses ring-based search for optimal performance (early exit on limits).
    /// </summary>
    internal List<Variant> ExecuteQuery(SpatialQuery query)
    {
        var results = new List<Variant>();
        var seenEntities = new HashSet<int>();
        var centerCell = WorldToGrid(query.Position);
        var maxCellRadius = Mathf.CeilToInt(query.Radius / GridCellSize) + 1;
        
        // Build list of type sets to check (no UnionWith - just direct references!)
        List<HashSet<SpatialGridEntity>> typeSets = null;
        if (query.TypeFilter != null && query.TypeFilter.Count > 0)
        {
            typeSets = new List<HashSet<SpatialGridEntity>>(query.TypeFilter.Count);
            foreach (var type in query.TypeFilter)
            {
                if (_entitiesByType.TryGetValue(type, out var typeSet))
                {
                    typeSets.Add(typeSet);  // Just store reference, no copying!
                }
            }
            
            // Early exit if no entities of requested types exist
            if (typeSets.Count == 0)
                return results;
        }
        
        // Ring-based search for early exit and natural distance ordering
        for (int ring = 0; ring <= maxCellRadius; ring++)
        {
            var cellsInRing = GetCellsInRing(centerCell, ring);
            
            foreach (var cell in cellsInRing)
            {
                if (!IsCellInBounds(cell))
                    continue;
                
                var cellEntities = _entityGrid[cell.X][cell.Y];
                
                foreach (var entity in cellEntities)
                {
                    // Apply all filters
                    if (!PassesFilters(entity, query, typeSets, seenEntities))
                        continue;
                    
                    // Entity passed all filters!
                    results.Add(GetEntityObject(entity));
                    
                    // Early exit if limit reached (works for FindNearest too!)
                    if (query.ResultLimit.HasValue && results.Count >= query.ResultLimit.Value)
                        return results;
                }
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Checks if an entity passes all query filters.
    /// </summary>
    private bool PassesFilters(
        SpatialGridEntity entity,
        SpatialQuery query,
        List<HashSet<SpatialGridEntity>> typeSets,
        HashSet<int> seenEntities)
    {
        // Type filter: Check if entity is in ANY of the type sets
        if (typeSets != null)
        {
            bool matchedType = false;
            for (int i = 0; i < typeSets.Count; i++)
            {
                if (typeSets[i].Contains(entity))
                {
                    matchedType = true;
                    break;
                }
            }
            if (!matchedType)
                return false;
        }
        
        // Deduplication
        if (seenEntities.Contains(entity.EntityId))
            return false;
        
        seenEntities.Add(entity.EntityId);
        
        var e = entity.Entity;
        
        // Alive filter (if specified)
        if (query.MustBeAlive.HasValue && query.MustBeAlive.Value && !e.IsAlive)
            return false;
        
        // Targetable filter (if specified)
        if (query.MustBeTargetable.HasValue && query.MustBeTargetable.Value && !e.IsTargetable)
            return false;
        
        // Attackable filter (if specified)
        if (query.MustBeAttackable.HasValue && query.MustBeAttackable.Value && !e.IsAttackable)
            return false;
        
        // Team filter (if specified)
        if (query.TeamFilter.HasValue)
        {
            var teamId = query.TeamFilter.Value;
            
            if (query.IsTargetingAllies.HasValue)
            {
                // Allies/Enemies filter
                var shouldSkipEntity = (teamId, query.IsTargetingAllies.Value, e.TeamId) switch
                {
                    (Team.None, _, _) => false,
                    (var myTeam, true, var entityTeam) => entityTeam != myTeam,
                    (var myTeam, false, var entityTeam) => entityTeam == myTeam,
                };
                
                if (shouldSkipEntity)
                    return false;
            }
            else
            {
                // Exact team match
                if (e.TeamId != teamId)
                    return false;
            }
        }
        
        // Distance check
        var distanceSquared = query.Position.DistanceSquaredTo(e.Position);
        var effectiveRadius = query.Radius + e.Radius;
        
        if (distanceSquared > effectiveRadius * effectiveRadius)
            return false;
        
        return true;  // Passed all filters!
    }
    
    #endregion
    
    private Vector2I[] GetPotentiallyOccupiedCells(Vector2I centerCell, float radius)
    {
        if (radius < GridCellSize * 0.5f)
        {
            return new Vector2I[] { centerCell };
        }

        var cellRadius = Mathf.CeilToInt(radius / GridCellSize);
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

    private Vector2I[] GetCellsInRing(Vector2I center, int ringRadius)
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
}
