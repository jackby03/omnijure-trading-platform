
using System;
using System.Runtime.Intrinsics;
using System.Runtime.Intrinsics.X86;
using Omnijure.Core.DataStructures;

namespace Omnijure.Core.Math;

public static class TechnicalAnalysis
{
    // Simple Moving Average
    public static float CalculateSMA(RingBuffer<Candle> buffer, int length, int offset = 0)
    {
        if (buffer.Count < length + offset) return 0;

        float sum = 0;
        for (int i = 0; i < length; i++)
        {
            sum += buffer[i + offset].Close;
        }
        return sum / length;
    }

    // Relative Strength Index (Standard 14 logic)
    // Note: In a real HFT engine, we would maintain stateful RSI (updates per tick) rather than recalculating full window.
    // For this prototype, linear calculation is fine.
    public static float CalculateRSI(RingBuffer<Candle> buffer, int length)
    {
        if (buffer.Count < length + 1) return 50;

        float avgGain = 0;
        float avgLoss = 0;

        // First average (SMA method for simplicity, Wilder's is better but complex for stateless func)
        for (int i = 0; i < length; i++)
        {
            float change = buffer[i].Close - buffer[i + 1].Close; // i is newer
            if (change >= 0) avgGain += change;
            else avgLoss += -change;
        }

        avgGain /= length;
        avgLoss /= length;

        if (avgLoss == 0) return 100;

        float rs = avgGain / avgLoss;
        return 100 - (100 / (1 + rs));
    }

    // Relative Volume (Current Volume / SMA Volume)
    public static float CalculateRVOL(RingBuffer<Candle> buffer, int length)
    {
        if (buffer.Count < length + 1) return 1.0f;
        
        float currentVol = buffer[0].Volume;
        float sumVol = 0;
        
        for (int i = 1; i <= length; i++)
        {
            sumVol += buffer[i].Volume;
        }
        float avgVol = sumVol / length;
        
        return avgVol == 0 ? 1 : currentVol / avgVol;
    }
}
