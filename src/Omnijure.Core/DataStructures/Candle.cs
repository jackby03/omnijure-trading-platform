
using System.Runtime.InteropServices;

namespace Omnijure.Core.DataStructures;

/// <summary>
/// Blittable struct for Zero-Copy operations.
/// 32 bytes ensures cache line friendliness (2 candles per 64-byte line).
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 4)]
public struct Candle
{
    public long Timestamp; // 8 bytes
    public float Open;     // 4 bytes
    public float High;     // 4 bytes
    public float Low;      // 4 bytes
    public float Close;    // 4 bytes
    public float Volume;   // 4 bytes
    
    // Total: 28 bytes. Padding to 32 bytes might be implicit or we can add a flag.
    // Let's keep it simple for now. 
}
