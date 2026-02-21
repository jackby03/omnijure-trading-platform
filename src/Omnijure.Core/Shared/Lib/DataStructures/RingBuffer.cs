
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;

namespace Omnijure.Core.Shared.Lib.DataStructures;

/// <summary>
/// A high-performance, fixed-size circular buffer.
/// Optimized for random access relative to the head (0 = latest).
/// </summary>
public sealed class RingBuffer<T>
{
    private readonly T[] _buffer;
    private readonly int _mask;
    private int _head; // Index of the *next* write slot, or current? Let's say: points to LATEST written.
    private bool _isFull;

    public int Capacity => _buffer.Length;
    public int Count { get; private set; }

    public RingBuffer(int capacity)
    {
        if (capacity <= 0 || (capacity & (capacity - 1)) != 0)
        {
            throw new ArgumentException("Capacity must be a power of 2", nameof(capacity));
        }
        _buffer = new T[capacity];
        _mask = capacity - 1;
        _head = -1; // Empty
        Count = 0;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Push(T item)
    {
        _head = (_head + 1) & _mask;
        _buffer[_head] = item;
        
        if (Count < Capacity)
        {
            Count++;
        }
    }

    /// <summary>
    /// Gets item at 'offset' from the head.
    /// 0 = Latest item pushed.
    /// 1 = Item before latest.
    /// </summary>
    public ref T this[int offset]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            if ((uint)offset >= (uint)Count)
            {
                throw new IndexOutOfRangeException();
            }
            int index = (_head - offset) & _mask;
            return ref _buffer[index];
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<T> AsSpan() => _buffer.AsSpan();

    public void Clear()
    {
        _head = -1;
        Count = 0;
        Array.Clear(_buffer);
    }
}
