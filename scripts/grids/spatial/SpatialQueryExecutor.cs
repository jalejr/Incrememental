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
    /// Executes a spatial query built by SpatialQuery
    /// </summary>
    public List<IEntity> Execute(SpatialQuery query)
    {
        var results = new List<IEntity>();
        var seenEntities = new HashSet<int>();
        var centerCell = _gridManager.WorldToGrid(query.Position);
        var maxCellRadius = Mathf.CeilToInt(query.Radius / _gridManager.GridCellSize) + 1;

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
                    if (!PassesFilters(entity, query, seenEntities))
                        continue;
                    
                    results.Add(entity.Entity);
                    
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
        if (query.TypeFilter != null && query.TypeFilter.Count > 0)
        {
            if (!query.TypeFilter.Contains(entity.Entity.Type))
                return false;
        }
        
        if (seenEntities.Contains(entity.EntityId))
            return false;
        
        seenEntities.Add(entity.EntityId);
        
        var e = entity.Entity;
        
        if (query.MustBeAlive.HasValue && query.MustBeAlive.Value && !e.IsAlive)
            return false;
        
        if (query.MustBeTargetable.HasValue && query.MustBeTargetable.Value && !e.IsTargetable)
            return false;
        
        if (query.MustBeAttackable.HasValue && query.MustBeAttackable.Value)
        {
            if (e is not ICombatEntity combatEntity || !combatEntity.IsAttackable)
                return false;
        }
        
        if (query.TeamFilter.HasValue)
        {
            var teamId = query.TeamFilter.Value;
            
            if (query.IsTargetingAllies.HasValue)
            {
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
                if (e.TeamId != teamId)
                    return false;
            }
        }
        
        var distanceSquared = query.Position.DistanceSquaredTo(e.Position);
        var effectiveRadius = query.Radius + e.Radius;
        
        if (distanceSquared > effectiveRadius * effectiveRadius)
            return false;
        
        return true;  // Passed all filters!
    }
}
