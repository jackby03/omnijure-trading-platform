using System;
using System.Collections.Generic;

namespace Omnijure.Core.Features.Scripting.SharpScript;

/// <summary>
/// Registry of built-in SharpScript functions.
/// Each function receives series data and current bar index.
/// </summary>
public static class Builtins
{
    /// <summary>
    /// Calculate SMA at a given bar index over a float series.
    /// </summary>
    public static float Sma(float[] source, int barIndex, int length)
    {
        if (barIndex + length > source.Length) return source[barIndex];
        float sum = 0;
        for (int i = barIndex; i < barIndex + length; i++)
            sum += source[i];
        return sum / length;
    }

    /// <summary>
    /// Calculate EMA at a given bar index. Requires previous EMA value for recursion.
    /// </summary>
    public static float Ema(float[] source, int barIndex, int length, float prevEma)
    {
        float k = 2f / (length + 1);
        if (float.IsNaN(prevEma))
        {
            // Seed with SMA
            return Sma(source, barIndex, length);
        }
        return source[barIndex] * k + prevEma * (1 - k);
    }

    /// <summary>
    /// Calculate RSI at a given bar index.
    /// </summary>
    public static float Rsi(float[] source, int barIndex, int length)
    {
        if (barIndex + length >= source.Length) return 50f;

        float avgGain = 0, avgLoss = 0;
        for (int i = barIndex; i < barIndex + length; i++)
        {
            float diff = source[i] - source[i + 1]; // source[i] is more recent
            if (diff > 0) avgGain += diff;
            else avgLoss -= diff;
        }
        avgGain /= length;
        avgLoss /= length;

        if (avgLoss == 0) return 100f;
        float rs = avgGain / avgLoss;
        return 100f - (100f / (1f + rs));
    }

    /// <summary>
    /// Standard deviation of source over length bars.
    /// </summary>
    public static float Stdev(float[] source, int barIndex, int length)
    {
        if (barIndex + length > source.Length) return 0f;

        float mean = Sma(source, barIndex, length);
        float sumSqDiff = 0;
        for (int i = barIndex; i < barIndex + length; i++)
        {
            float diff = source[i] - mean;
            sumSqDiff += diff * diff;
        }
        return MathF.Sqrt(sumSqDiff / length);
    }

    /// <summary>
    /// Highest value in source over length bars from barIndex.
    /// </summary>
    public static float Highest(float[] source, int barIndex, int length)
    {
        if (barIndex + length > source.Length) length = source.Length - barIndex;
        float max = float.MinValue;
        for (int i = barIndex; i < barIndex + length; i++)
            if (source[i] > max) max = source[i];
        return max;
    }

    /// <summary>
    /// Lowest value in source over length bars from barIndex.
    /// </summary>
    public static float Lowest(float[] source, int barIndex, int length)
    {
        if (barIndex + length > source.Length) length = source.Length - barIndex;
        float min = float.MaxValue;
        for (int i = barIndex; i < barIndex + length; i++)
            if (source[i] < min) min = source[i];
        return min;
    }

    /// <summary>
    /// Returns true when series a crosses above series b at barIndex.
    /// </summary>
    public static bool Crossover(float[] a, float[] b, int barIndex)
    {
        if (barIndex + 1 >= a.Length || barIndex + 1 >= b.Length) return false;
        return a[barIndex] > b[barIndex] && a[barIndex + 1] <= b[barIndex + 1];
    }

    /// <summary>
    /// Returns true when series a crosses below series b at barIndex.
    /// </summary>
    public static bool Crossunder(float[] a, float[] b, int barIndex)
    {
        if (barIndex + 1 >= a.Length || barIndex + 1 >= b.Length) return false;
        return a[barIndex] < b[barIndex] && a[barIndex + 1] >= b[barIndex + 1];
    }
}
