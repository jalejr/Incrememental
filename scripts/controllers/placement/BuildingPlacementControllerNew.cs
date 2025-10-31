using Godot;
using Incrememental.resources;
using Incrememental.scripts.grids.placement;

namespace Incrememental.scripts.controllers.placement;

/// <summary>
/// Handles player input for building placement with visual preview.
/// Optimized C# port of BuildingPlacementController.
/// </summary>
[GlobalClass]
public partial class BuildingPlacementControllerNew : Node
{
    [Export] public PlacementGridNew PlacementGrid { get; set; }
    [Export] public PlacementGridVisualizerNew GridVisualizer { get; set; }
    [Export] public Camera3D Camera { get; set; }
    
    // TODO: Remove - testing only
    [Export] public BuildingCatalogEntryNew TestCatalogEntry { get; set; }

    private BuildingCatalogEntryNew _selectedCatalogEntry;
    private Vector3 _previewPosition;
    private bool _isPlacing = false;
    private bool _previewValid = false;

    public override void _Process(double delta)
    {
        // TODO: Remove test input when done testing
        if (Input.IsActionJustPressed("test") && TestCatalogEntry != null)
        {
            StartPlacement(TestCatalogEntry);
        }

        if (!_isPlacing)
            return;

        UpdatePreview();

        if (Input.IsActionJustPressed("ui_accept"))
        {
            TryPlacingBuilding();
        }

        if (Input.IsActionJustPressed("ui_cancel"))
        {
            CancelPlacement();
        }
    }

    /// <summary>
    /// Starts the placement mode for a building.
    /// </summary>
    public void StartPlacement(BuildingCatalogEntryNew catalogEntry)
    {
        _isPlacing = true;
        _selectedCatalogEntry = catalogEntry;
        GridVisualizer?.ShowPlacementPreview(Vector3.Zero, catalogEntry.BuildingData.GridSize, false);
    }

    /// <summary>
    /// Cancels the current placement mode.
    /// </summary>
    public void CancelPlacement()
    {
        _isPlacing = false;
        _selectedCatalogEntry = null;
        GridVisualizer?.HidePlacementPreview();
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
            var gridPos = PlacementGrid.WorldToGrid(_previewPosition);
            _previewValid = PlacementGrid.CanPlaceBuilding(gridPos, _selectedCatalogEntry.BuildingData.GridSize);
            GridVisualizer?.ShowPlacementPreview(_previewPosition, _selectedCatalogEntry.BuildingData.GridSize, _previewValid);
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

        var gridPos = PlacementGrid.WorldToGrid(_previewPosition);
        var worldPos = PlacementGrid.GetPlacementPreviewPosition(
            _previewPosition,
            _selectedCatalogEntry.BuildingData.GridSize
        );

        var building = _selectedCatalogEntry.Scene.Instantiate<Node3D>();
        GetParent().AddChild(building);
        building.GlobalPosition = worldPos;

        var buildingData = PlacementGrid.PlaceBuilding(
            building,
            gridPos,
            _selectedCatalogEntry.BuildingData.GridSize,
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
