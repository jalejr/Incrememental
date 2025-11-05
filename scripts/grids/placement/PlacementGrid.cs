using Godot;
using Incrememental.scripts.global;
using Incrememental.scripts.grids;
using System.Collections.Generic;

namespace Incrememental.scripts.grids.placement;

/// <summary>
/// Manages building placement on a grid with unlock/occupied cell tracking
/// </summary>
[GlobalClass]
public partial class PlacementGrid : GridBase
{
    [Export] public Vector2 GridWorldSize { get; set; } = new(512.0f, 512.0f);

    private int[][] _unlockedCells;  // Reference count per cell
    private PlacementGridData[][] _occupiedCells;  // Building data per cell (nullable)
    private Vector2I _gridSize = Vector2I.Zero;
    
    private List<PlacementGridData> _buildings = new();
    private Dictionary<Node3D, PlacementGridData> _buildingToPlacementData = new();

    public override void _Ready()
    {
        InitializeGrids();
        GD.Print($"PlacementGrid initialized - Cell size: {GridCellSize}, Grid size: {_gridSize}");
        
        // Connect to EventBus
        EventBus.Instance.BuildingRemoved += OnBuildingRemoved;
        
        // TODO: Have the unlock happen through level start event
        UnlockStartingArea(new Vector3(256, 0, 256), 5);
    }

    /// <summary>
    /// Unlocks cells around a starting position.
    /// </summary>
    public void UnlockStartingArea(Vector3 startingPosition, int radius = 2)
    {
        var centerCell = WorldToGrid(startingPosition);
        UnlockCellsAround(centerCell, radius);
    }

    /// <summary>
    /// Checks if a cell is unlocked for building placement.
    /// </summary>
    public bool IsCellUnlocked(GridCell cell)
    {
        if (!IsCellInBounds(cell))
            return false;
        return _unlockedCells[cell.X][cell.Y] > 0;
    }

    /// <summary>
    /// Checks if a cell is occupied by a building.
    /// </summary>
    public bool IsCellOccupied(GridCell cell)
    {
        if (!IsCellInBounds(cell))
            return false;
        return _occupiedCells[cell.X][cell.Y] != null;
    }

    /// <summary>
    /// Checks if a building of the given size can be placed at the position.
    /// </summary>
    public bool CanPlaceBuilding(GridCell gridPos, GridCell buildingSize)
    {
        var cellsToCheck = GetCellsForArea(gridPos, buildingSize);
        
        foreach (var cell in cellsToCheck)
        {
            if (!IsCellInBounds(cell))
                return false;
            
            if (!IsCellUnlocked(cell))
                return false;
            
            if (IsCellOccupied(cell))
                return false;
        }
        
        return true;
    }

    /// <summary>
    /// Places a building on the grid.
    /// </summary>
    public PlacementGridData PlaceBuilding(Node3D buildingNode, GridCell gridPos, 
        GridCell buildingSize, int unlockRadius)
    {
        if (!CanPlaceBuilding(gridPos, buildingSize))
            return null;
        
        var buildingData = new PlacementGridData
        {
            BuildingNode = buildingNode,
            GridPosition = gridPos,
            GridSize = buildingSize,
            UnlockRadius = unlockRadius
        };
        
        var occupiedCells = GetBuildingOccupiedCells(buildingData);
        foreach (var cell in occupiedCells)
        {
            if (IsCellInBounds(cell))
            {
                _occupiedCells[cell.X][cell.Y] = buildingData;
            }
        }
        
        _buildings.Add(buildingData);
        _buildingToPlacementData[buildingNode] = buildingData;
        
        UnlockAreaAroundBuilding(buildingData);
        
        // Emit event using C# event
        EventBus.Instance.OnBuildingPlaced(buildingNode, gridPos);
        
        return buildingData;
    }

    /// <summary>
    /// Removes a building from the grid.
    /// </summary>
    public void RemoveBuilding(Node3D building)
    {
        if (!_buildingToPlacementData.TryGetValue(building, out var buildingData))
            return;
        
        var occupiedCells = GetBuildingOccupiedCells(buildingData);
        
        foreach (var cell in occupiedCells)
        {
            if (IsCellInBounds(cell))
            {
                _occupiedCells[cell.X][cell.Y] = null;
            }
        }
        
        LockCellsAroundBuilding(buildingData);
        
        _buildings.Remove(buildingData);
        _buildingToPlacementData.Remove(building);
    }

    /// <summary>
    /// Gets the building data at a specific cell.
    /// </summary>
    public PlacementGridData GetBuildingAtCell(GridCell cell)
    {
        if (!IsCellInBounds(cell))
            return null;
        return _occupiedCells[cell.X][cell.Y];
    }

    /// <summary>
    /// Gets all unlocked cells.
    /// Returns native C# list for performance.
    /// </summary>
    public List<GridCell> GetUnlockedCells()
    {
        var cells = new List<GridCell>();
        
        for (int x = 0; x < _gridSize.X; x++)
        {
            for (int y = 0; y < _gridSize.Y; y++)
            {
                if (_unlockedCells[x][y] > 0)
                {
                    cells.Add(new GridCell(x, y));
                }
            }
        }
        
        return cells;
    }

    /// <summary>
    /// Gets the snapped world position for placement preview.
    /// </summary>
    public Vector3 GetPlacementPreviewPosition(Vector3 worldPos, GridCell buildingSize)
    {
        var gridPos = WorldToGrid(worldPos);
        var corner = GridToWorld(gridPos, false);
        
        return corner + new Vector3(
            buildingSize.X * GridCellSize * 0.5f,
            0,
            buildingSize.Y * GridCellSize * 0.5f
        );
    }

    private void InitializeGrids()
    {
        _gridSize.X = Mathf.CeilToInt(GridWorldSize.X / GridCellSize);
        _gridSize.Y = Mathf.CeilToInt(GridWorldSize.Y / GridCellSize);
        
        // Initialize native C# jagged arrays for performance
        _unlockedCells = new int[_gridSize.X][];
        _occupiedCells = new PlacementGridData[_gridSize.X][];
        
        for (int x = 0; x < _gridSize.X; x++)
        {
            _unlockedCells[x] = new int[_gridSize.Y];
            _occupiedCells[x] = new PlacementGridData[_gridSize.Y];
            
            for (int y = 0; y < _gridSize.Y; y++)
            {
                _unlockedCells[x][y] = 0;
                _occupiedCells[x][y] = null;
            }
        }
    }

    private bool IsCellInBounds(GridCell cell)
    {
        return cell.X >= 0 && cell.X < _gridSize.X &&
               cell.Y >= 0 && cell.Y < _gridSize.Y;
    }

    private CellsForAreaEnumerator GetBuildingOccupiedCells(PlacementGridData buildingData)
    {
        return GetCellsForArea(buildingData.GridPosition, buildingData.GridSize);
    }

    private void UnlockAreaAroundBuilding(PlacementGridData buildingData)
    {
        var cellsToUnlock = GetUnlockCellsAroundBuilding(buildingData);
        foreach (var cell in cellsToUnlock)
        {
            if (!IsCellInBounds(cell))
                continue;
            _unlockedCells[cell.X][cell.Y]++;
        }
    }

    private void UnlockCellsAround(GridCell center, int radius)
    {
        var cellsToUnlock = GetCellsInRadius(center, radius);
        
        foreach (var cell in cellsToUnlock)
        {
            if (!IsCellInBounds(cell))
                continue;
            _unlockedCells[cell.X][cell.Y]++;
        }
    }

    private void LockCellsAroundBuilding(PlacementGridData buildingData)
    {
        var cellsToLock = GetUnlockCellsAroundBuilding(buildingData);
        foreach (var cell in cellsToLock)
        {
            if (!IsCellInBounds(cell))
                continue;
            _unlockedCells[cell.X][cell.Y]--;
            
            // Keep at 0 minimum (don't go negative)
            if (_unlockedCells[cell.X][cell.Y] < 0)
            {
                _unlockedCells[cell.X][cell.Y] = 0;
            }
        }
    }

    private CellsForAreaEnumerator GetUnlockCellsAroundBuilding(PlacementGridData buildingData)
    {
        var unlockMin = buildingData.GridPosition - new GridCell(buildingData.UnlockRadius, buildingData.UnlockRadius);
        var unlockMax = buildingData.GridPosition + buildingData.GridSize + new GridCell(buildingData.UnlockRadius, buildingData.UnlockRadius);
        var unlockSize = unlockMax - unlockMin;
        
        return GetCellsForArea(unlockMin, unlockSize);
    }

    private void OnBuildingRemoved(Node3D building)
    {
        RemoveBuilding(building);
    }
}
