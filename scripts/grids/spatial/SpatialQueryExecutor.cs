using Godot;
using Incrememental.scripts.entities;
using System.Collections.Generic;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Handles execution of spatial queries. Separated from SpatialGridManager for better organization.
/// Contains the core query algorithm: ring-based search with type filtering and early exit optimization.
/// </summary>
internal class SpatialQueryExecutor
{
    private readonly SpatialGridManager _gridManager;
    
    public SpatialQueryExecutor(SpatialGridManager gridManager)
    {
        _gridManager = gridManager;
    }
    
    /// <summary>
    /// Executes a spatial query built by SpatialQuery.
    /// Uses ring-based search for optimal performance (early exit on limits).
    /// </summary>
    public List<IEntity> Execute(SpatialQuery query)
    {
        var results = new List<IEntity>();
        var seenEntities = new HashSet<int>();
        var centerCell = _gridManager.WorldToGrid(query.Position);
        var maxCellRadius = Mathf.CeilToInt(query.Radius / _gridManager.GridCellSize) + 1;
        
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
                    if (!PassesFilters(entity, query, seenEntities))
                        continue;
                    
                    // Entity passed all filters!
                    results.Add(entity.Entity);
                    
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
        HashSet<int> seenEntities)
    {
        // Type filter: Direct enum comparison (fast!)
        if (query.TypeFilter != null && query.TypeFilter.Count > 0)
        {
            if (!query.TypeFilter.Contains(entity.Entity.Type))
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
        if (query.MustBeAttackable.HasValue && query.MustBeAttackable.Value)
        {
            if (e is not ICombatEntity combatEntity || !combatEntity.IsAttackable)
                return false;
        }
        
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
