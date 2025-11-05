using Godot;
using Incrememental.resources;
using Incrememental.scripts.entities.units;
using System.Collections.Generic;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Handles MultiMesh rendering and visual updates for units.
/// </summary>
internal class UnitRenderSystem
{
    private readonly Node _parentNode;
    private readonly float _lerpSpeed;

    public UnitRenderSystem(Node parentNode, Godot.Collections.Array<UnitTypeConfig> unitTypeConfigs,
        Dictionary<UnitType, UnitTypeRuntimeData> unitTypesRuntime, float lerpSpeed)
    {
        _parentNode = parentNode;
        _lerpSpeed = lerpSpeed;
        
        SetupUnitTypes(unitTypeConfigs, unitTypesRuntime);
    }

    /// <summary>
    /// Sets up all unit type runtime data including MultiMesh instances.
    /// </summary>
    private void SetupUnitTypes(
        Godot.Collections.Array<UnitTypeConfig> unitTypeConfigs, Dictionary<UnitType, UnitTypeRuntimeData> unitTypesRuntime)
    {
        if (unitTypeConfigs.Count == 0)
        {
            GD.PushWarning("No unit_type_configs added to manager...");
        }

        foreach (var config in unitTypeConfigs)
        {
            if (unitTypesRuntime.ContainsKey(config.UnitType))
            {
                GD.PushError($"Duplicate unit type: {config.UnitType}");
                continue;
            }

            var runtime = new UnitTypeRuntimeData
            {
                Config = config
            };

            SetupMultiMesh(runtime, config);

            unitTypesRuntime[config.UnitType] = runtime;
        }
    }

    /// <summary>
    /// Sets up MultiMesh for a unit type.
    /// </summary>
    public void SetupMultiMesh(UnitTypeRuntimeData runtime, UnitTypeConfig config)
    {
        // Validate config
        if (config.MaxCount <= 0)
        {
            GD.PushError($"UnitTypeConfig for {config.UnitType} has invalid MaxCount: {config.MaxCount}. Setting to 100.");
            config.MaxCount = 100;
        }
        
        if (config.Mesh == null)
        {
            GD.PushError($"UnitTypeConfig for {config.UnitType} has no Mesh assigned!");
        }

        runtime.MultiMesh = new MultiMesh
        {
            Mesh = config.Mesh,
            TransformFormat = MultiMesh.TransformFormatEnum.Transform3D,
            UseCustomData = true,
            InstanceCount = config.MaxCount,
            VisibleInstanceCount = 0
        };

        runtime.MultiMeshInstance = new MultiMeshInstance3D
        {
            Multimesh = runtime.MultiMesh
        };

        _parentNode.AddChild(runtime.MultiMeshInstance);
        
        GD.Print($"Setup MultiMesh for {config.UnitType}: MaxCount={config.MaxCount}");
    }

    /// <summary>
    /// Updates visual transforms with lerping for smooth rendering.
    /// </summary>
    public void UpdateVisuals(List<Unit> allUnits, Dictionary<UnitType, UnitTypeRuntimeData> unitTypesRuntime, float delta)
    {
        // Reset visual indices
        foreach (var runtime in unitTypesRuntime.Values)
        {
            runtime.VisualIndex = 0;
        }

        var lerpWeight = System.Math.Clamp(_lerpSpeed * delta, 0.0f, 1.0f);
        
        for (int i = 0; i < allUnits.Count; i++)
        {
            var unit = allUnits[i];
            if (!unit.IsAlive)
                continue;

            // Cache to reduce property access overhead
            var currentPos = unit.Position;
            var visualPos = unit.VisualPosition;
            unit.VisualPosition = visualPos.Lerp(currentPos, lerpWeight);

            var runtime = unit.CachedRuntime;
            var instanceIdx = runtime.VisualIndex;
            runtime.VisualIndex++;

            var transform = new Transform3D(Basis.Identity, unit.VisualPosition);
            var customData = unit.GetCustomVisualData();
            
            // Batch these calls together
            runtime.MultiMesh.SetInstanceTransform(instanceIdx, transform);
            runtime.MultiMesh.SetInstanceCustomData(instanceIdx, customData);
        }
    }
}
