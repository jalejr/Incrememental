using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace Incrememental.resources;

/// <summary>
/// Catalog of available building types for lookup and retrieval.
/// Optimized C# port of BuildingCatalog.
/// </summary>
[GlobalClass]
public partial class BuildingCatalogNew : Resource
{
    [Export] public Godot.Collections.Dictionary<string, BuildingCatalogEntryNew> Entries { get; set; } = new();

    /// <summary>
    /// Gets a building catalog entry by its ID.
    /// </summary>
    public BuildingCatalogEntryNew GetEntryById(string buildingId)
    {
        if (Entries.TryGetValue(buildingId, out var entry))
        {
            return entry;
        }

        GD.PushError($"No building found with ID: {buildingId}");
        return null;
    }

    /// <summary>
    /// Gets all available building entries.
    /// Returns native C# list for performance.
    /// </summary>
    public List<BuildingCatalogEntryNew> GetAvailableEntries()
    {
        var available = new List<BuildingCatalogEntryNew>(Entries.Count);
        
        foreach (var entry in Entries.Values)
        {
            available.Add(entry);
        }
        
        return available;
    }

    /// <summary>
    /// Gets all unlocked building entries based on research requirements.
    /// </summary>
    public List<BuildingCatalogEntryNew> GetUnlockedEntries()
    {
        var unlocked = new List<BuildingCatalogEntryNew>();
        
        foreach (var entry in Entries.Values)
        {
            if (entry.BuildingData != null && entry.BuildingData.IsUnlocked())
            {
                unlocked.Add(entry);
            }
        }
        
        return unlocked;
    }
}
