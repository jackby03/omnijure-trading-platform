using SkiaSharp;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Object pool for SKPaint to reduce allocations during rendering.
/// Reuses paint objects instead of creating/disposing them every frame.
/// </summary>
public sealed class PaintPool
{
    private readonly Stack<SKPaint> _availablePaints = new();
    private readonly object _lock = new();
    private int _totalCreated = 0;
    private const int MaxPoolSize = 100; // Limit pool size to avoid memory bloat

    /// <summary>
    /// Rents a paint object from the pool
    /// </summary>
    public SKPaint Rent()
    {
        lock (_lock)
        {
            if (_availablePaints.Count > 0)
            {
                var paint = _availablePaints.Pop();
                return paint;
            }
        }

        _totalCreated++;
        return new SKPaint { IsAntialias = true };
    }

    /// <summary>
    /// Returns a paint object to the pool for reuse
    /// </summary>
    public void Return(SKPaint paint)
    {
        if (paint == null) return;

        // Reset to default state
        paint.Reset();
        paint.IsAntialias = true;

        lock (_lock)
        {
            // Don't grow the pool indefinitely
            if (_availablePaints.Count < MaxPoolSize)
            {
                _availablePaints.Push(paint);
            }
            else
            {
                paint.Dispose();
            }
        }
    }

    /// <summary>
    /// Gets diagnostics about pool usage
    /// </summary>
    public (int Available, int TotalCreated) GetStats()
    {
        lock (_lock)
        {
            return (_availablePaints.Count, _totalCreated);
        }
    }

    /// <summary>
    /// Singleton instance
    /// </summary>
    public static PaintPool Instance { get; } = new();
}
