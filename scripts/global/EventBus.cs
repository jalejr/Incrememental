using Godot;
using Incrememental.scripts.entities.buildings;
using Incrememental.scripts.grids;
using Incrememental.scripts.entities.units;

namespace Incrememental.scripts.global;

/// <summary>
/// Global event bus for decoupled communication between systems using C# events
/// </summary>
public partial class EventBus : Node
{
    public static event System.Action<Building, GridCell> BuildingPlaced;
    public static event System.Action<Building> BuildingRemoved;
    public static event System.Action<Building> BuildingUpgraded;
    public static event System.Action<Building> BuildingSold;
    public static event System.Action<Unit, Node> UnitSpawned;
    public static event System.Action<Unit, Node> UnitDied;
    
    public static void EmitBuildingPlaced(Building building, GridCell gridPos) => BuildingPlaced?.Invoke(building, gridPos);
    public static void EmitBuildingRemoved(Building building) => BuildingRemoved?.Invoke(building);
    public static void EmitBuildingUpgraded(Building building) => BuildingUpgraded?.Invoke(building);
    public static void EmitBuildingSold(Building building) => BuildingSold?.Invoke(building);
    public static void EmitUnitSpawned(Unit unit, Node building) => UnitSpawned?.Invoke(unit, building);
    public static void EmitUnitDied(Unit unit, Node building) => UnitDied?.Invoke(unit, building);
}
