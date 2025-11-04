using Godot;
using Incrememental.scripts.entities.units;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Handles unit AI logic updates with compensated delta for staggered updates.
/// </summary>
internal class UnitLogicSystem
{
    private readonly UnitManager _manager;
    private int _updateIndex = 0;

    public UnitLogicSystem(UnitManager manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Updates unit logic with compensated delta for performance.
    /// Only updates a subset of units per frame based on MaxUnitsUpdatedPerFrame.
    /// Uses parallel processing with thread-local contexts.
    /// </summary>
    public void Update(float delta, List<Unit> allUnits, int aliveCount, int maxUnitsPerFrame, 
        Dictionary<Unit, int> damageQueue, List<Unit> destroyQueue, Mutex damageQueueMutex, Mutex destroyQueueMutex)
    {
        if (aliveCount == 0)
            return;

        var framesBetweenUpdates = Mathf.CeilToInt(aliveCount / (float)maxUnitsPerFrame);
        var compensatedDelta = delta * framesBetweenUpdates;
        var unitsThisFrame = Mathf.Min(maxUnitsPerFrame, aliveCount);
        
        // Collect alive unit indices to update
        var indicesToUpdate = new int[unitsThisFrame];
        var checkedCount = 0;
        var foundCount = 0;
        var startIndex = _updateIndex;
        
        while (foundCount < unitsThisFrame && checkedCount < allUnits.Count)
        {
            var index = (startIndex + checkedCount) % allUnits.Count;
            checkedCount++;
            
            if (allUnits[index].IsAlive)
            {
                indicesToUpdate[foundCount] = index;
                foundCount++;
            }
        }
        
        // Process units in parallel - each thread gets its own context automatically
        Parallel.For(0, foundCount, new ParallelOptions 
        { 
            MaxDegreeOfParallelism = _manager.ThreadCount 
        }, i =>
        {
            var context = _manager.GetContextForThread();
            var unitIndex = indicesToUpdate[i];
            var unit = allUnits[unitIndex];
            
            if (!unit.IsAlive) return;
            
            unit.PathAge += compensatedDelta;
            unit.UpdateLogic(compensatedDelta, context);
        });
        
        // Merge all thread contexts into main queues (single-threaded after parallel work)
        MergeAllContexts(damageQueue, destroyQueue);
        
        _updateIndex = (startIndex + checkedCount) % Mathf.Max(allUnits.Count, 1);
    }
    
    /// <summary>
    /// Merges all thread-local context queues into main queues.
    /// Called on main thread after parallel work completes.
    /// </summary>
    private void MergeAllContexts(Dictionary<Unit, int> damageQueue, List<Unit> destroyQueue)
    {
        foreach (var context in _manager.GetAllContexts())
        {
            // Merge damage queue
            foreach (var kvp in context.DamageQueue)
            {
                if (!damageQueue.ContainsKey(kvp.Key))
                    damageQueue[kvp.Key] = 0;
                damageQueue[kvp.Key] += kvp.Value;
            }
            
            // Merge destroy queue
            destroyQueue.AddRange(context.DestroyQueue);
        }
    }
}
