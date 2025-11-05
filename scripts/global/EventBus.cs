using Godot;
using Incrememental.scripts.grids;

namespace Incrememental.scripts.global;

/// <summary>
/// Global event bus for decoupled communication between systems using C# events
/// </summary>
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }
    public event System.Action<Node3D, GridCell> BuildingPlaced;
    public event System.Action<Node3D> BuildingRemoved;
    public event System.Action<Node3D> BuildingUpgraded;
    public event System.Action<Node3D> BuildingSold;
    public event System.Action<entities.units.Unit, Node> UnitSpawned;
    public event System.Action<entities.units.Unit, Node> UnitDied;
    public override void _Ready()
    {
        Instance = this;
    }
    public void OnBuildingPlaced(Node3D building, GridCell gridPos) => BuildingPlaced?.Invoke(building, gridPos);
    public void OnBuildingRemoved(Node3D building) => BuildingRemoved?.Invoke(building);
    public void OnBuildingUpgraded(Node3D building) => BuildingUpgraded?.Invoke(building);
    public void OnBuildingSold(Node3D building) => BuildingSold?.Invoke(building);
    public void OnUnitSpawned(Incrememental.scripts.entities.units.Unit unit, Node building) => UnitSpawned?.Invoke(unit, building);
    public void OnUnitDied(Incrememental.scripts.entities.units.Unit unit, Node building) => UnitDied?.Invoke(unit, building);
}
