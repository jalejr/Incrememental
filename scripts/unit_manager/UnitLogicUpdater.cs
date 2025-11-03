using Godot;
using Incrememental.scripts.entities.units;
using System.Collections.Generic;

namespace Incrememental.scripts.unit_manager;

/// <summary>
/// Handles unit AI logic updates with compensated delta for staggered updates.
/// </summary>
internal class UnitLogicUpdater
{
    private readonly UnitManagerNew _manager;
    private int _updateIndex = 0;

    public UnitLogicUpdater(UnitManagerNew manager)
    {
        _manager = manager;
    }

    /// <summary>
    /// Updates unit logic with compensated delta for performance.
    /// Only updates a subset of units per frame based on MaxUnitsUpdatedPerFrame.
    /// </summary>
    public void Update(float delta, List<UnitNew> allUnits, int aliveCount, int maxUnitsPerFrame, 
        Dictionary<UnitNew, int> damageQueue, List<UnitNew> destroyQueue, Mutex damageQueueMutex, Mutex destroyQueueMutex)
    {
        if (aliveCount == 0)
            return;

        var framesBetweenUpdates = Mathf.CeilToInt(aliveCount / (float)maxUnitsPerFrame);
        var compensatedDelta = delta * framesBetweenUpdates;
        var unitsThisFrame = Mathf.Min(maxUnitsPerFrame, aliveCount);
        var checkedCount = 0;
        var updated = 0;
        var context = new UnitLogicContext(_manager);
        var startIndex = _updateIndex;

        while (updated < unitsThisFrame && checkedCount < allUnits.Count)
        {
            var index = (startIndex + checkedCount) % allUnits.Count;
            var unit = allUnits[index];

            checkedCount++;

            if (!unit.IsAlive)
                continue;

            unit.PathAge += compensatedDelta;
            unit.UpdateLogic(compensatedDelta, context);

            updated++;
        }

        // Merge context queues into main queues
        damageQueueMutex.Lock();
        foreach (var kvp in context.DamageQueue)
        {
            var targetUnit = kvp.Key;
            var damage = kvp.Value;
            
            if (!damageQueue.ContainsKey(targetUnit))
                damageQueue[targetUnit] = 0;
            damageQueue[targetUnit] += damage;
        }
        damageQueueMutex.Unlock();

        destroyQueueMutex.Lock();
        destroyQueue.AddRange(context.DestroyQueue);
        destroyQueueMutex.Unlock();

        _updateIndex = (startIndex + checkedCount) % Mathf.Max(allUnits.Count, 1);
    }
}
