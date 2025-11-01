using Godot;
using Incrememental.scripts.entities;
using System.Collections.Generic;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Handles execution of spatial queries. Separated from SpatialGridManagerNew for better organization.
/// Contains the core query algorithm: ring-based search with type filtering and early exit optimization.
/// </summary>
internal class SpatialQueryExecutor
{
    private readonly SpatialGridManagerNew _gridManager;
    
    public SpatialQueryExecutor(SpatialGridManagerNew gridManager)
    {
        _gridManager = gridManager;
    }
    
    /// <summary>
    /// Executes a spatial query built by SpatialQuery.
    /// Uses ring-based search for optimal performance (early exit on limits).
    /// </summary>
    public List<Variant> Execute(SpatialQuery query)
    {
        var results = new List<Variant>();
        var seenEntities = new HashSet<int>();
        var centerCell = _gridManager.WorldToGrid(query.Position);
        var maxCellRadius = Mathf.CeilToInt(query.Radius / _gridManager.GridCellSize) + 1;
        
        // Build list of type sets to check (no UnionWith - just direct references!)
        List<HashSet<SpatialGridEntity>> typeSets = null;
        if (query.TypeFilter != null && query.TypeFilter.Count > 0)
        {
            typeSets = new List<HashSet<SpatialGridEntity>>(query.TypeFilter.Count);
            foreach (var type in query.TypeFilter)
            {
                if (_gridManager.EntitiesByType.TryGetValue(type, out var typeSet))
                {
                    typeSets.Add(typeSet);  // Just store reference, no copying!
                }
            }
            
            // Early exit if no entities of requested types exist
            if (typeSets.Count == 0)
                return results;
        }
        
        // Ring-based search for early exit and natural distance ordering
        for (int ring = 0; ring <= maxCellRadius; ring++)
        {
            var cellsInRing = _gridManager.GetCellsInRing(centerCell, ring);
            
            foreach (var cell in cellsInRing)
            {
                if (!_gridManager.IsCellInBounds(cell))
                    continue;
                
                var cellEntities = _gridManager.GetCellEntities(cell);
                
                foreach (var entity in cellEntities)
                {
                    // Apply all filters
                    if (!PassesFilters(entity, query, typeSets, seenEntities))
                        continue;
                    
                    // Entity passed all filters!
                    results.Add(_gridManager.GetEntityObject(entity));
                    
                    // Early exit if limit reached (works for FindNearest too!)
                    if (query.ResultLimit.HasValue && results.Count >= query.ResultLimit.Value)
                        return results;
                }
            }
        }
        
        return results;
    }
    
    /// <summary>
    /// Checks if an entity passes all query filters.
    /// </summary>
    private bool PassesFilters(
        SpatialGridEntity entity,
        SpatialQuery query,
        List<HashSet<SpatialGridEntity>> typeSets,
        HashSet<int> seenEntities)
    {
        // Type filter: Check if entity is in ANY of the type sets
        if (typeSets != null)
        {
            bool matchedType = false;
            for (int i = 0; i < typeSets.Count; i++)
            {
                if (typeSets[i].Contains(entity))
                {
                    matchedType = true;
                    break;
                }
            }
            if (!matchedType)
                return false;
        }
        
        // Deduplication
        if (seenEntities.Contains(entity.EntityId))
            return false;
        
        seenEntities.Add(entity.EntityId);
        
        var e = entity.Entity;
        
        // Alive filter (if specified)
        if (query.MustBeAlive.HasValue && query.MustBeAlive.Value && !e.IsAlive)
            return false;
        
        // Targetable filter (if specified)
        if (query.MustBeTargetable.HasValue && query.MustBeTargetable.Value && !e.IsTargetable)
            return false;
        
        // Attackable filter (if specified)
        if (query.MustBeAttackable.HasValue && query.MustBeAttackable.Value && !e.IsAttackable)
            return false;
        
        // Team filter (if specified)
        if (query.TeamFilter.HasValue)
        {
            var teamId = query.TeamFilter.Value;
            
            if (query.IsTargetingAllies.HasValue)
            {
                // Allies/Enemies filter
                var shouldSkipEntity = (teamId, query.IsTargetingAllies.Value, e.TeamId) switch
                {
                    (Team.None, _, _) => false,
                    (var myTeam, true, var entityTeam) => entityTeam != myTeam,
                    (var myTeam, false, var entityTeam) => entityTeam == myTeam,
                };
                
                if (shouldSkipEntity)
                    return false;
            }
            else
            {
                // Exact team match
                if (e.TeamId != teamId)
                    return false;
            }
        }
        
        // Distance check
        var distanceSquared = query.Position.DistanceSquaredTo(e.Position);
        var effectiveRadius = query.Radius + e.Radius;
        
        if (distanceSquared > effectiveRadius * effectiveRadius)
            return false;
        
        return true;  // Passed all filters!
    }
}
