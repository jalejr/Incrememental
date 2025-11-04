using Godot;
using Godot.Collections;
using System.Collections.Generic;

namespace Incrememental.resources;

/// <summary>
/// Catalog of available building types for lookup and retrieval
/// </summary>
[GlobalClass]
public partial class BuildingCatalog : Resource
{
    [Export] public Godot.Collections.Dictionary<string, BuildingCatalogEntry> Entries { get; set; } = new();

    /// <summary>
    /// Gets a building catalog entry by its ID.
    /// </summary>
    public BuildingCatalogEntry GetEntryById(string buildingId)
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
    /// </summary>
    public List<BuildingCatalogEntry> GetAvailableEntries()
    {
        var available = new List<BuildingCatalogEntry>(Entries.Count);
        
        foreach (var entry in Entries.Values)
        {
            available.Add(entry);
        }
        
        return available;
    }

    /// <summary>
    /// Gets all unlocked building entries based on research requirements.
    /// </summary>
    public List<BuildingCatalogEntry> GetUnlockedEntries()
    {
        var unlocked = new List<BuildingCatalogEntry>();
        
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
