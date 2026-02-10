using SkiaSharp;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Caches text measurements to avoid expensive font metric calculations.
/// Text measurement is surprisingly expensive in rendering loops.
/// </summary>
public sealed class TextMeasureCache
{
    private readonly Dictionary<(string Text, float FontSize), float> _widthCache = new(512);
    private readonly object _lock = new();
    private const int MaxCacheSize = 2000; // Limit cache size

    /// <summary>
    /// Measures text width using the cache when possible
    /// </summary>
    public float MeasureText(string text, SKFont font)
    {
        if (string.IsNullOrEmpty(text)) return 0;

        var key = (text, font.Size);

        lock (_lock)
        {
            if (_widthCache.TryGetValue(key, out float width))
            {
                return width;
            }
        }

        // Cache miss - measure it
        float measuredWidth = font.MeasureText(text);

        lock (_lock)
        {
            // Clear cache if it gets too large
            if (_widthCache.Count >= MaxCacheSize)
            {
                _widthCache.Clear();
            }

            _widthCache[key] = measuredWidth;
        }

        return measuredWidth;
    }

    /// <summary>
    /// Clears the cache (useful when fonts change)
    /// </summary>
    public void Clear()
    {
        lock (_lock)
        {
            _widthCache.Clear();
        }
    }

    /// <summary>
    /// Gets cache statistics
    /// </summary>
    public int GetCacheSize()
    {
        lock (_lock)
        {
            return _widthCache.Count;
        }
    }

    /// <summary>
    /// Singleton instance
    /// </summary>
    public static TextMeasureCache Instance { get; } = new();
}
