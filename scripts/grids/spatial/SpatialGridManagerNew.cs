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

    // 2D array of lists of entities per cell
    private List<SpatialGridEntity>[][] _unitGrid;
    private List<SpatialGridEntity>[][] _buildingGrid;
    private Vector2I _gridSize = Vector2I.Zero;
    
    private Dictionary<int, SpatialGridEntity> _entityIdToEntity = new();
    private int _nextEntityId = 0;

    public override void _Ready()
    {
        InitializeGrids();
        GD.Print($"SpatialGridManagerNew initialized - Grid size: {_gridSize.X}x{_gridSize.Y}");
    }

    /// <summary>
    /// Registers an entity in the spatial grid.
    /// </summary>
    public SpatialGridEntity RegisterEntity(EntityDataNew entityData, Variant entityObject)
    {
        var entity = new SpatialGridEntity
        {
            EntityData = entityData,
            EntityObject = entityObject,
            EntityId = _nextEntityId++
        };

        entity.GridCell = WorldToGrid(entityData.Position);
        entity.OccupiedCells = GetPotentiallyOccupiedCells(entity.GridCell, entityData.Radius);

        AddToGrid(entity);

        _entityIdToEntity[entity.EntityId] = entity;

        return entity;
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
            entity.EntityData.Position = newPosition;
            entity.OccupiedCells = GetPotentiallyOccupiedCells(entity.GridCell, entity.EntityData.Radius);

            AddToGrid(entity);
        }
        else
        {
            entity.EntityData.Position = newPosition;
        }
    }

    /// <summary>
    /// Gets all nearby entities (units and buildings) within radius.
    /// </summary>
    public List<Variant> GetNearbyEntities(Vector3 position, float radius, Team teamId, bool isTargetingAllies)
    {
        var nearbyUnits = GetNearbyEntitiesByType(EntityType.Unit, position, radius, teamId, isTargetingAllies);
        var nearbyBuildings = GetNearbyEntitiesByType(EntityType.Building, position, radius, teamId, isTargetingAllies);

        var result = new List<Variant>(nearbyUnits.Count + nearbyBuildings.Count);
        result.AddRange(nearbyUnits);
        result.AddRange(nearbyBuildings);

        return result;
    }

    /// <summary>
    /// Gets nearby entities of a specific type within radius.
    /// </summary>
    public List<Variant> GetNearbyEntitiesByType(
        EntityType entityType,
        Vector3 position,
        float radius,
        Team teamId,
        bool isTargetingAllies = false)
    {
        var nearbyEntities = new List<Variant>();
        var seenEntities = new HashSet<int>();
        var centerCell = WorldToGrid(position);
        var cellDistanceToCheck = Mathf.CeilToInt(radius / GridCellSize) + 1;
        var cellsToCheck = GetCellsInRadius(centerCell, cellDistanceToCheck);
        var targetGrid = FindGridToTarget(entityType);

        foreach (var cellToCheck in cellsToCheck)
        {
            if (!IsCellInBounds(cellToCheck))
                continue;

            var cellEntities = targetGrid[cellToCheck.X][cellToCheck.Y];

            foreach (var entity in cellEntities)
            {
                if (seenEntities.Contains(entity.EntityId))
                    continue;

                seenEntities.Add(entity.EntityId);

                var data = entity.EntityData;

                if (!data.IsAlive || !data.IsTargetable)
                    continue;

                if (teamId != Team.None)
                {
                    if (isTargetingAllies)
                    {
                        if (data.TeamId != teamId)
                            continue;
                    }
                    else
                    {
                        if (data.TeamId == teamId)
                            continue;
                    }
                }

                var distanceSquared = position.DistanceSquaredTo(data.Position);
                var effectiveRadius = radius + data.Radius;

                if (distanceSquared <= effectiveRadius * effectiveRadius)
                {
                    nearbyEntities.Add(GetEntityObject(entity));
                }
            }
        }

        return nearbyEntities;
    }

    /// <summary>
    /// Gets the nearest entity (unit or building) within radius.
    /// </summary>
    public Variant GetNearestEntity(Vector3 position, float radius, Team teamId, bool isTargetingAllies)
    {
        var nearbyUnit = GetNearestEntityByType(EntityType.Unit, position, radius, teamId, isTargetingAllies);
        if (nearbyUnit.Obj != null)
            return nearbyUnit;

        var nearbyBuilding = GetNearestEntityByType(EntityType.Building, position, radius, teamId, isTargetingAllies);
        if (nearbyBuilding.Obj != null)
            return nearbyBuilding;

        return default;
    }

    /// <summary>
    /// Gets the nearest entity of a specific type within radius.
    /// Uses ring-based search for optimal performance.
    /// </summary>
    public Variant GetNearestEntityByType(
        EntityType entityType,
        Vector3 position,
        float radius,
        Team teamId,
        bool isTargetingAllies = false)
    {
        var centerCell = WorldToGrid(position);
        var maxCellRadius = Mathf.CeilToInt(radius / GridCellSize) + 1;
        var seenEntities = new HashSet<int>();
        var targetGrid = FindGridToTarget(entityType);

        // Search in expanding rings for early exit
        for (int ring = 0; ring <= maxCellRadius; ring++)
        {
            var cellsInRing = GetCellsInRing(centerCell, ring);

            foreach (var cell in cellsInRing)
            {
                if (!IsCellInBounds(cell))
                    continue;

                var cellEntities = targetGrid[cell.X][cell.Y];

                foreach (var entity in cellEntities)
                {
                    if (seenEntities.Contains(entity.EntityId))
                        continue;

                    seenEntities.Add(entity.EntityId);

                    var data = entity.EntityData;

                    if (!data.IsAlive || !data.IsTargetable)
                        continue;

                    if (teamId != Team.None)
                    {
                        if (isTargetingAllies)
                        {
                            if (data.TeamId != teamId)
                                continue;
                        }
                        else
                        {
                            if (data.TeamId == teamId)
                                continue;
                        }
                    }

                    var distanceSquared = position.DistanceSquaredTo(data.Position);
                    var effectiveRadius = radius + data.Radius;

                    if (distanceSquared <= effectiveRadius * effectiveRadius)
                    {
                        return GetEntityObject(entity);
                    }
                }
            }
        }

        return default;
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
                if (_unitGrid[x][y].Count > 0 || _buildingGrid[x][y].Count > 0)
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

        _unitGrid = new List<SpatialGridEntity>[_gridSize.X][];
        _buildingGrid = new List<SpatialGridEntity>[_gridSize.X][];

        for (int x = 0; x < _gridSize.X; x++)
        {
            _unitGrid[x] = new List<SpatialGridEntity>[_gridSize.Y];
            _buildingGrid[x] = new List<SpatialGridEntity>[_gridSize.Y];

            for (int y = 0; y < _gridSize.Y; y++)
            {
                _unitGrid[x][y] = new List<SpatialGridEntity>();
                _buildingGrid[x][y] = new List<SpatialGridEntity>();
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
        var targetGrid = FindGridToTarget(entity.EntityData.Type);

        foreach (var cell in entity.OccupiedCells)
        {
            if (!IsCellInBounds(cell))
                continue;

            targetGrid[cell.X][cell.Y].Add(entity);
        }
    }

    private void RemoveFromGrid(SpatialGridEntity entity)
    {
        var targetGrid = FindGridToTarget(entity.EntityData.Type);

        foreach (var cell in entity.OccupiedCells)
        {
            if (!IsCellInBounds(cell))
                continue;

            targetGrid[cell.X][cell.Y].Remove(entity);
        }
    }

    private List<SpatialGridEntity>[][] FindGridToTarget(EntityType entityType)
    {
        return entityType switch
        {
            EntityType.Unit => _unitGrid,
            EntityType.Building => _buildingGrid,
            _ => throw new System.ArgumentException($"Unknown entity type: {entityType}")
        };
    }

    private Variant GetEntityObject(SpatialGridEntity entity)
    {
        return entity.EntityObject;
    }

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
