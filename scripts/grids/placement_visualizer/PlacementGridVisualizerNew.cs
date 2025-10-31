using Godot;
using Incrememental.scripts.global;
using System.Collections.Generic;

namespace Incrememental.scripts.grids.placement;

/// <summary>
/// Visualizes the placement grid with unlocked/occupied cells and placement preview.
/// Optimized C# port of PlacementGridVisualizer.
/// </summary>
[GlobalClass]
public partial class PlacementGridVisualizerNew : Node3D
{
    [Export] public PlacementGridNew PlacementGrid { get; set; }
    [Export] public Color UnlockedCellColor { get; set; } = new(0.2f, 0.8f, 0.2f, 0.3f);
    [Export] public Color OccupiedCellColor { get; set; } = new(0.5f, 0.5f, 0.5f, 0.4f);
    [Export] public Color ValidPlacementColor { get; set; } = new(0.2f, 1.0f, 0.2f, 0.5f);
    [Export] public Color InvalidPlacementColor { get; set; } = new(1.0f, 0.2f, 0.2f, 0.5f);

    private MultiMeshInstance3D _cellMesh;
    private List<MeshInstance3D> _previewMeshes = new();
    private Vector2I _currentPreviewSize = Vector2I.Zero;

    public override void _Ready()
    {
        if (PlacementGrid == null)
        {
            GD.PushError("PlacementGridVisualizerNew needs PlacementGrid assigned!");
            return;
        }

        SetupGridMesh();
        ConnectSignals();
        CallDeferred(nameof(UpdateAllCells));
    }

    /// <summary>
    /// Shows a placement preview at the given world position.
    /// </summary>
    public void ShowPlacementPreview(Vector3 worldPos, Vector2I buildingSize, bool isValid)
    {
        if (buildingSize != _currentPreviewSize)
        {
            CreatePreviewMeshes(buildingSize);
            _currentPreviewSize = buildingSize;
        }

        var gridPos = PlacementGrid.WorldToGrid(worldPos);
        var color = isValid ? ValidPlacementColor : InvalidPlacementColor;

        // Get cells to preview
        var cells = PlacementGrid.GetCellsForArea(gridPos, buildingSize);

        for (int i = 0; i < cells.Length; i++)
        {
            var cellWorldPos = PlacementGrid.GridToWorld(cells[i], true);
            cellWorldPos.Y = 0.02f;

            _previewMeshes[i].GlobalPosition = cellWorldPos;
            _previewMeshes[i].Visible = true;

            var mat = _previewMeshes[i].GetSurfaceOverrideMaterial(0) as StandardMaterial3D;
            if (mat != null)
            {
                mat.AlbedoColor = color;
            }
        }
    }

    /// <summary>
    /// Hides the placement preview.
    /// </summary>
    public void HidePlacementPreview()
    {
        foreach (var mesh in _previewMeshes)
        {
            mesh.Visible = false;
        }
    }

    private void CreatePreviewMeshes(Vector2I buildingSize)
    {
        // Clean up existing preview meshes
        foreach (var mesh in _previewMeshes)
        {
            mesh.QueueFree();
        }
        _previewMeshes.Clear();

        var count = buildingSize.X * buildingSize.Y;
        for (int i = 0; i < count; i++)
        {
            var meshInstance = new MeshInstance3D();
            AddChild(meshInstance);

            var quad = new PlaneMesh
            {
                Size = new Vector2(
                    PlacementGrid.GridCellSize * 0.9f,
                    PlacementGrid.GridCellSize * 0.9f
                )
            };
            meshInstance.Mesh = quad;

            var mat = new StandardMaterial3D
            {
                Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
                ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
                CullMode = BaseMaterial3D.CullModeEnum.Disabled
            };
            meshInstance.SetSurfaceOverrideMaterial(0, mat);

            meshInstance.Visible = false;
            _previewMeshes.Add(meshInstance);
        }
    }

    private void UpdateAllCells()
    {
        var unlockedCells = PlacementGrid.GetUnlockedCells();

        if (unlockedCells.Count == 0)
        {
            _cellMesh.Multimesh.InstanceCount = 0;
            return;
        }

        _cellMesh.Multimesh.InstanceCount = unlockedCells.Count;

        for (int i = 0; i < unlockedCells.Count; i++)
        {
            var cell = unlockedCells[i];
            var worldPos = PlacementGrid.GridToWorld(cell, true);
            worldPos.Y = 0.01f;  // Slightly above ground

            var instanceTransform = new Transform3D(Basis.Identity, worldPos);
            _cellMesh.Multimesh.SetInstanceTransform(i, instanceTransform);

            var color = UnlockedCellColor;
            if (PlacementGrid.IsCellOccupied(cell))
            {
                color = OccupiedCellColor;
            }

            _cellMesh.Multimesh.SetInstanceColor(i, color);
        }
    }

    private void SetupGridMesh()
    {
        _cellMesh = new MultiMeshInstance3D();
        AddChild(_cellMesh);

        var quadMesh = new PlaneMesh
        {
            Size = new Vector2(
                PlacementGrid.GridCellSize * 0.95f,
                PlacementGrid.GridCellSize * 0.95f
            )
        };

        var multiMesh = new MultiMesh
        {
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseColors = true,
            Mesh = quadMesh,
            InstanceCount = 0
        };

        _cellMesh.Multimesh = multiMesh;

        var material = new StandardMaterial3D
        {
            Transparency = BaseMaterial3D.TransparencyEnum.Alpha,
            ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded,
            CullMode = BaseMaterial3D.CullModeEnum.Disabled,
            VertexColorUseAsAlbedo = true
        };
        _cellMesh.MaterialOverride = material;
    }

    private void ConnectSignals()
    {
        // Listen to EventBus - single source of truth
        var eventBus = GetNode<EventBusNew>("/root/EventBusNew");
        eventBus.BuildingPlaced += OnBuildingPlaced;
        eventBus.BuildingSold += OnBuildingRemoved;
    }

    private void OnBuildingPlaced(Variant building, Vector2I gridPos)
    {
        UpdateAllCells();
    }

    private void OnBuildingRemoved(Node3D building)
    {
        UpdateAllCells();
    }
}
