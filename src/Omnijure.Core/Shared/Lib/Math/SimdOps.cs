
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace Omnijure.Core.Shared.Lib.Math;

public static class SimdOps
{
    /// <summary>
    /// Calculates the sum of an array using SIMD instructions.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Sum(ReadOnlySpan<float> data)
    {
        var vectorSum = Vector<float>.Zero;
        int vectorSize = Vector<float>.Count;
        int i = 0;
        int length = data.Length;

        // Vectorized loop
        while (i <= length - vectorSize)
        {
            var v = new Vector<float>(data.Slice(i));
            vectorSum += v;
            i += vectorSize;
        }

        // Reduce vector to scalar
        float result = Vector.Dot(vectorSum, Vector<float>.One);

        // Handle remaining elements
        while (i < length)
        {
            result += data[i];
            i++;
        }

        return result;
    }

    /// <summary>
    /// Calculates Simple Moving Average (SMA).
    /// </summary>
    public static float Sma(ReadOnlySpan<float> data)
    {
        if (data.Length == 0) return 0f;
        return Sum(data) / data.Length;
    }
    
    // Additional SIMD ops (RSI, Variance) would go here.
}
