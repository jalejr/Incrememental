using Godot;
using Incrememental.scripts.grids;
using Incrememental.scripts.entities.units;

namespace Incrememental.scripts.global;

/// <summary>
/// Global event bus for decoupled communication between systems using C# events
/// </summary>
public partial class EventBus : Node
{
    public static event System.Action<Node3D, GridCell> BuildingPlaced;
    public static event System.Action<Node3D> BuildingRemoved;
    public static event System.Action<Node3D> BuildingUpgraded;
    public static event System.Action<Node3D> BuildingSold;
    public static event System.Action<Unit, Node> UnitSpawned;
    public static event System.Action<Unit, Node> UnitDied;
    
    public static void EmitBuildingPlaced(Node3D building, GridCell gridPos) => BuildingPlaced?.Invoke(building, gridPos);
    public static void EmitBuildingRemoved(Node3D building) => BuildingRemoved?.Invoke(building);
    public static void EmitBuildingUpgraded(Node3D building) => BuildingUpgraded?.Invoke(building);
    public static void EmitBuildingSold(Node3D building) => BuildingSold?.Invoke(building);
    public static void EmitUnitSpawned(Unit unit, Node building) => UnitSpawned?.Invoke(unit, building);
    public static void EmitUnitDied(Unit unit, Node building) => UnitDied?.Invoke(unit, building);
}
