using Godot;
using Godot.Collections;

namespace Incrememental.scripts.entities.buildings;

/// <summary>
/// Manages the visual appearance of building previews during placement
/// </summary>
[GlobalClass]
public partial class PreviewBuilding : Node3D
{
    [Export] public StandardMaterial3D ValidMaterial { get; set; }
    [Export] public StandardMaterial3D InvalidMaterial { get; set; }

    /// <summary>
    /// Sets the preview to display as valid placement (green/valid material).
    /// </summary>
    public void SetValid()
    {
        ApplyMaterialToMeshes(ValidMaterial);
    }

    /// <summary>
    /// Sets the preview to display as invalid placement (red/invalid material).
    /// </summary>
    public void SetInvalid()
    {
        ApplyMaterialToMeshes(InvalidMaterial);
    }

    /// <summary>
    /// Applies a material to all MeshInstance3D children.
    /// </summary>
    private void ApplyMaterialToMeshes(Material material)
    {
        Array<Node> children = GetChildren();
        
        foreach (Node child in children)
        {
            if (child is MeshInstance3D meshInstance)
            {
                meshInstance.MaterialOverride = material;
            }
        }
    }
}
