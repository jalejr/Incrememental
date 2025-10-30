using Godot;

namespace Incrememental.scripts.global;

/// <summary>
/// Global event bus for decoupled communication between systems.
/// This should be set up as an AutoLoad singleton in Project Settings.
/// </summary>
public partial class EventBusNew : Node
{
    [Signal]
    public delegate void BuildingPlacedEventHandler(Variant building, Vector2I gridPos);
    
    [Signal]
    public delegate void BuildingRemovedEventHandler(Variant building);
    
    [Signal]
    public delegate void BuildingUpgradedEventHandler(Node3D building);
    
    [Signal]
    public delegate void BuildingSoldEventHandler(Node3D building);
}
