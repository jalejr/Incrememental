using Godot;
using Incrememental.resources;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Runtime data for each unit type including MultiMesh rendering.
/// Shared across lifecycle, render, and manager systems.
/// </summary>
public class UnitTypeRuntimeData
{
    public UnitTypeConfigNew Config { get; set; }
    public MultiMesh MultiMesh { get; set; }
    public MultiMeshInstance3D MultiMeshInstance { get; set; }
    public int AliveCount { get; set; } = 0;
    public int VisualIndex { get; set; } = 0;
}
