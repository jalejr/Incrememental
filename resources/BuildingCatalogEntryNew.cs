using Godot;

namespace Incrememental.resources;

/// <summary>
/// Catalog entry linking building data with its scene.
/// </summary>
[GlobalClass]
public partial class BuildingCatalogEntryNew : Resource
{
    [Export] public BuildingDataNew BuildingData { get; set; }
    [Export] public PackedScene Scene { get; set; }
    [Export] public PackedScene PreviewScene { get; set; }
}
