using System;
using System.Runtime.CompilerServices;
using Godot;

namespace Incrememental.scripts.grids;

/// <summary>
/// Pure C# grid cell coordinate (int-based) to eliminate Vector2I marshalling overhead.
/// Optimized for zero-allocation grid operations with aggressive inlining.
/// </summary>
public readonly struct GridCell : IEquatable<GridCell>
{
    public readonly int X;
    public readonly int Y;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public GridCell(int x, int y)
    {
        X = x;
        Y = y;
    }

    // Arithmetic operators
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GridCell operator +(GridCell a, GridCell b) => new(a.X + b.X, a.Y + b.Y);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GridCell operator -(GridCell a, GridCell b) => new(a.X - b.X, a.Y - b.Y);

    // Equality
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(GridCell other) => X == other.X && Y == other.Y;

    public override bool Equals(object obj) => obj is GridCell other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(X, Y);

    public static bool operator ==(GridCell left, GridCell right) => left.Equals(right);

    public static bool operator !=(GridCell left, GridCell right) => !left.Equals(right);

    public override string ToString() => $"({X}, {Y})";

    // Grid-specific operations
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int ManhattanDistance(GridCell other) => Math.Abs(X - other.X) + Math.Abs(Y - other.Y);
    
    // Conversion helpers (only at boundaries)
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static GridCell FromVector2I(Vector2I v) => new(v.X, v.Y);
    
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Vector2I ToVector2I() => new(X, Y);
}
