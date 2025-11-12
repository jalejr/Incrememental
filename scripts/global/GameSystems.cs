using Godot;
using Incrememental.scripts.grids.placement;
using Incrememental.scripts.grids.spatial;
using Incrememental.scripts.unit_manager;

namespace Incrememental.scripts.global;

/// <summary>
/// Provides static access to core game systems within the active game scene.
/// Automatically resets when scene is unloaded.
/// </summary>
public partial class GameSystems : Node
{
    private static GameSystems _instance;
    
    // Instance fields (private)
    private PlacementGrid _placementGrid;
    private UnitManager _unitManager;
    private SpatialGridManager _spatialGrid;
    private PlacementGridVisualizer _gridVisualizer;
    
    // Static properties (public) - clean API without .Instance
    public static PlacementGrid PlacementGrid => _instance?._placementGrid;
    public static UnitManager UnitManager => _instance?._unitManager;
    public static SpatialGridManager SpatialGrid => _instance?._spatialGrid;
    public static PlacementGridVisualizer GridVisualizer => _instance?._gridVisualizer;
    
    public override void _Ready()
    {
        _instance = this;
        
        // Auto-discover systems using Unique Names
        // Make sure these nodes have "Unique Name" enabled in the editor (% icon)
        _placementGrid = GetNodeOrNull<PlacementGrid>("%PlacementGrid");
        _unitManager = GetNodeOrNull<UnitManager>("%UnitManager");
        _spatialGrid = GetNodeOrNull<SpatialGridManager>("%SpatialGrid");
        _gridVisualizer = GetNodeOrNull<PlacementGridVisualizer>("%GridVisualizer");
        
        // Warn if systems are missing
        if (_placementGrid == null)
            GD.PushWarning("PlacementGrid not found - ensure it has Unique Name enabled in the editor");
        if (_unitManager == null)
            GD.PushWarning("UnitManager not found - ensure it has Unique Name enabled in the editor");
        if (_spatialGrid == null)
            GD.PushWarning("SpatialGridManager not found - ensure it has Unique Name enabled in the editor");
        if (_gridVisualizer == null)
            GD.PushWarning("GridVisuaalizer not found - ensure it has Unique Name enabled in the editor");
        
        GD.Print($"GameSystems initialized - PlacementGrid: {_placementGrid != null}, UnitManager: {_unitManager != null}, SpatialGrid: {_spatialGrid != null}, GridVisualizer: {_gridVisualizer != null}");
    }
    
    public override void _ExitTree()
    {
        // Auto-cleanup when scene unloads
        _instance = null;
    }
}
