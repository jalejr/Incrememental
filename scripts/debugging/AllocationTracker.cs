using Godot;
using System;

namespace Incrememental.scripts.debugging;

/// <summary>
/// Tracks GC activity and memory allocations for performance profiling.
/// Use this to verify zero-allocation code paths.
/// </summary>
public static class AllocationTracker
{
    private static long _lastGen0Count = 0;
    private static long _lastGen1Count = 0;
    private static long _lastGen2Count = 0;
    private static long _lastMemory = 0;
    private static bool _enabled = true;
    
    /// <summary>
    /// Call at the beginning of a frame or code section to track.
    /// </summary>
    public static void BeginFrame()
    {
        if (!_enabled) return;
        
        _lastGen0Count = GC.CollectionCount(0);
        _lastGen1Count = GC.CollectionCount(1);
        _lastGen2Count = GC.CollectionCount(2);
        _lastMemory = GC.GetTotalMemory(false);
    }
    
    /// <summary>
    /// Call at the end of a frame or code section to track.
    /// Prints if any GC occurred or significant memory growth.
    /// </summary>
    public static void EndFrame(string label = "Frame", bool alwaysPrint = false)
    {
        if (!_enabled) return;
        
        var gen0 = GC.CollectionCount(0) - _lastGen0Count;
        var gen1 = GC.CollectionCount(1) - _lastGen1Count;
        var gen2 = GC.CollectionCount(2) - _lastGen2Count;
        var currentMemory = GC.GetTotalMemory(false);
        var memoryDelta = currentMemory - _lastMemory;
        
        // Only print if GC happened (indicates allocation occurred)
        if (gen0 > 0 || gen1 > 0 || gen2 > 0)
        {
            GD.Print($"[{label}] GC: Gen0={gen0}, Gen1={gen1}, Gen2={gen2} | Memory: {memoryDelta / 1024:+0;-0} KB");
        }
    }
    
    /// <summary>
    /// Prints current memory statistics.
    /// </summary>
    public static void PrintMemoryInfo(string label = "Memory Info")
    {
        var totalMemory = GC.GetTotalMemory(false);
        var gen0 = GC.CollectionCount(0);
        var gen1 = GC.CollectionCount(1);
        var gen2 = GC.CollectionCount(2);
        
        GD.Print($"=== {label} ===");
        GD.Print($"Total Managed Memory: {totalMemory / 1024 / 1024} MB");
        GD.Print($"Total GC Collections: Gen0={gen0}, Gen1={gen1}, Gen2={gen2}");
    }
    
    /// <summary>
    /// Runs a memory stability test over a period of time.
    /// Useful for detecting leaks or steady-state allocation.
    /// </summary>
    public static async void TestStability(Node node, float durationSeconds = 5.0f)
    {
        GD.Print($"Starting allocation stability test ({durationSeconds}s)...");
        
        // Warmup - let everything initialize
        await node.ToSignal(node.GetTree().CreateTimer(2.0), "timeout");
        
        // Force GC to get clean baseline
        GC.Collect();
        GC.WaitForPendingFinalizers();
        GC.Collect();
        
        var beforeMem = GC.GetTotalMemory(false);
        var beforeGen0 = GC.CollectionCount(0);
        var beforeGen1 = GC.CollectionCount(1);
        var beforeGen2 = GC.CollectionCount(2);
        
        // Run test period
        await node.ToSignal(node.GetTree().CreateTimer(durationSeconds), "timeout");
        
        var afterMem = GC.GetTotalMemory(false);
        var afterGen0 = GC.CollectionCount(0);
        var afterGen1 = GC.CollectionCount(1);
        var afterGen2 = GC.CollectionCount(2);
        
        var memoryGrowth = afterMem - beforeMem;
        var gen0Collections = afterGen0 - beforeGen0;
        var gen1Collections = afterGen1 - beforeGen1;
        var gen2Collections = afterGen2 - beforeGen2;
        
        GD.Print($"=== Allocation Stability Test Results ({durationSeconds}s) ===");
        GD.Print($"Memory Growth: {memoryGrowth / 1024} KB ({memoryGrowth / 1024 / durationSeconds:F2} KB/s)");
        GD.Print($"Gen0 Collections: {gen0Collections} ({gen0Collections / durationSeconds:F2} per second)");
        GD.Print($"Gen1 Collections: {gen1Collections}");
        GD.Print($"Gen2 Collections: {gen2Collections}");
        
        // Evaluation
        if (memoryGrowth < 100 * 1024 && gen0Collections <= 2)
        {
            GD.Print("✅ EXCELLENT - Near-zero allocation");
        }
        else if (memoryGrowth < 500 * 1024 && gen0Collections <= 5)
        {
            GD.Print("✅ GOOD - Low allocation rate");
        }
        else if (memoryGrowth < 2 * 1024 * 1024)
        {
            GD.Print("⚠️ MODERATE - Some allocations happening");
        }
        else
        {
            GD.Print("❌ HIGH - Significant allocations detected");
        }
    }
    
    /// <summary>
    /// Enable or disable tracking (useful for performance).
    /// </summary>
    public static void SetEnabled(bool enabled)
    {
        _enabled = enabled;
    }
}
