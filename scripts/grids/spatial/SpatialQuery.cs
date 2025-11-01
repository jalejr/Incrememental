using Godot;
using Incrememental.scripts.entities;
using System.Collections.Generic;

namespace Incrememental.scripts.grids.spatial;

/// <summary>
/// Fluent query builder for spatial entity searches.
/// Uses the Builder pattern to construct complex queries in a readable way.
/// 
/// Example:
///   var enemies = grid.Query()
///       .At(myPosition)
///       .Within(10f)
///       .OfType(EntityType.Unit)
///       .OnTeam(Team.Enemy)
///       .ThatAreAlive()
///       .Execute();
/// </summary>
public class SpatialQuery
{
    // Required parameters (must be set)
    private Vector3 _position;
    private float _radius;
    private bool _hasPosition = false;
    private bool _hasRadius = false;
    
    // Optional filters (null = not applied)
    private HashSet<EntityType> _typeFilter = null;
    private Team? _teamFilter = null;
    private bool? _isTargetingAllies = null;
    private bool? _mustBeAlive = null;
    private bool? _mustBeTargetable = null;
    private bool? _mustBeAttackable = null;
    
    // Sorting/limiting
    private bool _findNearest = false;
    private int? _limit = null;
    
    // Reference to grid manager
    private SpatialGridManagerNew _gridManager;
    
    // Parameterless constructor for object pooling
    internal SpatialQuery()
    {
    }
    
    // Reset method for pooling - called when getting from pool
    internal void Reset(SpatialGridManagerNew gridManager)
    {
        _gridManager = gridManager;
        _position = Vector3.Zero;
        _radius = 0f;
        _hasPosition = false;
        _hasRadius = false;
        _typeFilter = null;
        _teamFilter = null;
        _isTargetingAllies = null;
        _mustBeAlive = null;
        _mustBeTargetable = null;
        _mustBeAttackable = null;
        _findNearest = false;
        _limit = null;
    }
    
    #region Required Parameters
    
    /// <summary>
    /// Set the center position for the query.
    /// This is a REQUIRED parameter.
    /// </summary>
    public SpatialQuery At(Vector3 position)
    {
        _position = position;
        _hasPosition = true;
        return this;
    }
    
    /// <summary>
    /// Set the search radius from the center position.
    /// This is a REQUIRED parameter.
    /// </summary>
    public SpatialQuery Within(float radius)
    {
        _radius = radius;
        _hasRadius = true;
        return this;
    }
    
    #endregion
    
    #region Type Filters
    
    /// <summary>
    /// Filter by a single entity type.
    /// Can be called multiple times to search for multiple types.
    /// </summary>
    public SpatialQuery OfType(EntityType type)
    {
        _typeFilter ??= new HashSet<EntityType>();
        _typeFilter.Add(type);
        return this;
    }
    
    /// <summary>
    /// Filter by multiple entity types at once.
    /// </summary>
    public SpatialQuery OfTypes(params EntityType[] types)
    {
        _typeFilter ??= new HashSet<EntityType>();
        foreach (var type in types)
        {
            _typeFilter.Add(type);
        }
        return this;
    }
    
    /// <summary>
    /// Search for all entity types (removes type filter).
    /// </summary>
    public SpatialQuery OfAnyType()
    {
        _typeFilter = null;
        return this;
    }
    
    #endregion
    
    #region Team Filters
    
    /// <summary>
    /// Filter by a specific team.
    /// </summary>
    public SpatialQuery OnTeam(Team team)
    {
        _teamFilter = team;
        _isTargetingAllies = null;  // Clear ally/enemy filter (team is explicit)
        return this;
    }
    
    /// <summary>
    /// Filter for allies of the specified team.
    /// </summary>
    public SpatialQuery AlliesOf(Team team)
    {
        _teamFilter = team;
        _isTargetingAllies = true;
        return this;
    }
    
    /// <summary>
    /// Filter for enemies of the specified team.
    /// </summary>
    public SpatialQuery EnemiesOf(Team team)
    {
        _teamFilter = team;
        _isTargetingAllies = false;
        return this;
    }
    
    /// <summary>
    /// Remove team filter (search all teams).
    /// </summary>
    public SpatialQuery OnAnyTeam()
    {
        _teamFilter = null;
        _isTargetingAllies = null;
        return this;
    }
    
    #endregion
    
    #region State Filters
    
    /// <summary>
    /// Only include entities that are alive.
    /// </summary>
    public SpatialQuery ThatAreAlive()
    {
        _mustBeAlive = true;
        return this;
    }
    
    /// <summary>
    /// Only include entities that are targetable.
    /// </summary>
    public SpatialQuery ThatAreTargetable()
    {
        _mustBeTargetable = true;
        return this;
    }
    
    /// <summary>
    /// Only include entities that are attackable.
    /// </summary>
    public SpatialQuery ThatAreAttackable()
    {
        _mustBeAttackable = true;
        return this;
    }
    
    /// <summary>
    /// Convenience method: alive + targetable + attackable.
    /// Common filter for combat targeting.
    /// </summary>
    public SpatialQuery ThatAreValidTargets()
    {
        _mustBeAlive = true;
        _mustBeTargetable = true;
        _mustBeAttackable = true;
        return this;
    }
    
    #endregion
    
    #region Result Modifiers
    
    /// <summary>
    /// Find only the nearest entity instead of all entities.
    /// Returns a list with 0 or 1 element.
    /// </summary>
    public SpatialQuery FindNearest()
    {
        _findNearest = true;
        _limit = 1;
        return this;
    }
    
    /// <summary>
    /// Limit the number of results returned.
    /// </summary>
    public SpatialQuery Limit(int count)
    {
        _limit = count;
        return this;
    }
    
    #endregion
    
    #region Execution
    
    /// <summary>
    /// Execute the query and return matching entities.
    /// Auto-returns query to pool after execution.
    /// </summary>
    public List<Variant> Execute()
    {
        // Validate required parameters
        if (!_hasPosition)
            throw new System.InvalidOperationException("Query requires position to be set via At()");
        
        if (!_hasRadius)
            throw new System.InvalidOperationException("Query requires radius to be set via Within()");
        
        // Execute the query through the grid manager
        var results = _gridManager.ExecuteQuery(this);
        
        // Auto-return to pool for reuse
        _gridManager.ReturnQueryToPool(this);
        
        return results;
    }
    
    #endregion
    
    #region Internal Access (for GridManager)
    
    // These properties are internal so only SpatialGridManagerNew can read them
    internal Vector3 Position => _position;
    internal float Radius => _radius;
    internal HashSet<EntityType> TypeFilter => _typeFilter;
    internal Team? TeamFilter => _teamFilter;
    internal bool? IsTargetingAllies => _isTargetingAllies;
    internal bool? MustBeAlive => _mustBeAlive;
    internal bool? MustBeTargetable => _mustBeTargetable;
    internal bool? MustBeAttackable => _mustBeAttackable;
    internal bool FindNearestOnly => _findNearest;
    internal int? ResultLimit => _limit;
    
    #endregion
}
