using Godot;

namespace Incrememental.scripts.global;

/// <summary>
/// Global event bus for decoupled communication between systems using C# events.
/// This should be set up as an AutoLoad singleton in Project Settings.
/// Access via EventBus.Instance in C# code.
/// Uses pure C# events to avoid marshalling overhead.
/// </summary>
public partial class EventBus : Node
{
    public static EventBus Instance { get; private set; }
    
    // C# events instead of Godot signals - avoids marshalling overhead
    
    // Building events
    public event System.Action<Node3D, Vector2I> BuildingPlaced;
    public event System.Action<Node3D> BuildingRemoved;
    public event System.Action<Node3D> BuildingUpgraded;
    public event System.Action<Node3D> BuildingSold;
    
    // Unit events
    public event System.Action<entities.units.Unit, Node> UnitSpawned;
    public event System.Action<entities.units.Unit, Node> UnitDied;
    
    public override void _Ready()
    {
        Instance = this;
    }
    
    // Building invoke methods for type safety and null-checking
    public void OnBuildingPlaced(Node3D building, Vector2I gridPos) => BuildingPlaced?.Invoke(building, gridPos);
    public void OnBuildingRemoved(Node3D building) => BuildingRemoved?.Invoke(building);
    public void OnBuildingUpgraded(Node3D building) => BuildingUpgraded?.Invoke(building);
    public void OnBuildingSold(Node3D building) => BuildingSold?.Invoke(building);
    
    // Unit invoke methods for type safety and null-checking
    public void OnUnitSpawned(Incrememental.scripts.entities.units.Unit unit, Node building) => UnitSpawned?.Invoke(unit, building);
    public void OnUnitDied(Incrememental.scripts.entities.units.Unit unit, Node building) => UnitDied?.Invoke(unit, building);
}
