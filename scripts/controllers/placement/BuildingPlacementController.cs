using Godot;
using Incrememental.resources;
using Incrememental.scripts.global;
using Incrememental.scripts.grids;
using Incrememental.scripts.grids.placement;

namespace Incrememental.scripts.controllers.placement;

/// <summary>
/// Handles player input for building placement with visual preview
/// </summary>
[GlobalClass]
public partial class BuildingPlacementController : Node
{
    [Export] public Camera3D Camera { get; set; }
    
    // TODO: Remove - testing only
    [Export] public BuildingCatalogEntry TestCatalogEntry { get; set; }

    private BuildingCatalogEntry _selectedCatalogEntry;
    private Vector3 _previewPosition;
    private bool _isPlacing = false;
    private bool _previewValid = false;

    public override void _Process(double delta)
    {
        // TODO: Remove test input when done testing
        if (Input.IsActionJustPressed(InputAction.Test) && TestCatalogEntry != null)
        {
            StartPlacement(TestCatalogEntry);
        }

        if (!_isPlacing)
            return;

        UpdatePreview();

        if (Input.IsActionJustPressed(InputAction.UiAccept))
        {
            TryPlacingBuilding();
        }

        if (Input.IsActionJustPressed(InputAction.UiCancel))
        {
            CancelPlacement();
        }
    }

    /// <summary>
    /// Starts the placement mode for a building.
    /// </summary>
    public void StartPlacement(BuildingCatalogEntry catalogEntry)
    {
        _isPlacing = true;
        _selectedCatalogEntry = catalogEntry;
        var gridSize = GridCell.FromVector2I(catalogEntry.BuildingData.GridSize);
        GameSystems.GridVisualizer?.ShowPlacementPreview(Vector3.Zero, gridSize, false);
    }

    /// <summary>
    /// Cancels the current placement mode.
    /// </summary>
    public void CancelPlacement()
    {
        _isPlacing = false;
        _selectedCatalogEntry = null;
        GameSystems.GridVisualizer?.HidePlacementPreview();
    }

    private void UpdatePreview()
    {
        if (Camera == null)
            return;

        var mousePos = GetViewport().GetMousePosition();
        var from = Camera.ProjectRayOrigin(mousePos);
        var to = from + Camera.ProjectRayNormal(mousePos) * 1000.0f;

        var plane = new Plane(Vector3.Up, 0);
        var intersection = plane.IntersectsRay(from, to - from);

        if (intersection.HasValue)
        {
            _previewPosition = intersection.Value;
            var gridPos = GameSystems.PlacementGrid.WorldToGrid(_previewPosition);
            var gridSize = GridCell.FromVector2I(_selectedCatalogEntry.BuildingData.GridSize);
            _previewValid = GameSystems.PlacementGrid.CanPlaceBuilding(gridPos, gridSize);
            GameSystems.GridVisualizer?.ShowPlacementPreview(_previewPosition, gridSize, _previewValid);
        }
    }

    private void TryPlacingBuilding()
    {
        if (!_previewValid)
            return;

        // TODO: Implement economy check
        // if (!RunEconomyManager.CanAfford(_selectedCatalogEntry.BuildingData.PlacementCost))
        // {
        //     GD.Print("Cannot afford building!");
        //     return;
        // }
        //
        // if (!RunEconomyManager.SpendCurrency(_selectedCatalogEntry.BuildingData.PlacementCost))
        //     return;

        var gridPos = GameSystems.PlacementGrid.WorldToGrid(_previewPosition);
        var gridSize = GridCell.FromVector2I(_selectedCatalogEntry.BuildingData.GridSize);
        var worldPos = GameSystems.PlacementGrid.GetPlacementPreviewPosition(
            _previewPosition,
            gridSize
        );

        var building = _selectedCatalogEntry.Scene.Instantiate<Node3D>();
        GetParent().AddChild(building);
        building.GlobalPosition = worldPos;

        var buildingData = GameSystems.PlacementGrid.PlaceBuilding(
            building,
            gridPos,
            gridSize,
            _selectedCatalogEntry.BuildingData.UnlockRadius
        );

        if (buildingData == null)
        {
            // Placement failed - clean up
            // TODO: Refund currency
            // RunEconomyManager.AddCurrency(_selectedCatalogEntry.BuildingData.PlacementCost);
            building.QueueFree();
            GD.Print("Building placement failed!");
        }
        else
        {
            GD.Print($"Building placed successfully at {gridPos}");
        }
    }
}
