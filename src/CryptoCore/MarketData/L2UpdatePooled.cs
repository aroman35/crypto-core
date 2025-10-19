using System.Buffers;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CryptoCore.Primitives;

namespace CryptoCore.MarketData;

/// <summary>
/// Pooled variant of <see cref="L2Update"/> that rents its delta storage from <see cref="ArrayPool{T}"/>.
/// Use when you want to avoid allocations during deserialization / parsing. Dispose to return memory to the pool.
/// </summary>
public sealed class L2UpdatePooled : IDisposable
{
    /// <summary>Instrument symbol these changes belong to.</summary>
    public Symbol Symbol { get; private set; }

    /// <summary>Unix time in milliseconds when the event was emitted by venue (if known).</summary>
    public long EventTimeMs { get; private set; }

    /// <summary>Binance-like sequencing ids: first / last / previous-last.</summary>
    public ulong FirstUpdateId { get; private set; }

    /// <summary>Last (inclusive) exchange update id for this batch. Zero when unknown.</summary>
    public ulong LastUpdateId { get; private set; }

    /// <summary>Previous last update id (Binance: <c>pu</c>) for continuity checks. Zero when unknown.</summary>
    public ulong PrevLastUpdateId { get; private set; }

    /// <summary>True when the batch is a full snapshot (replace all levels).</summary>
    public bool IsSnapshot { get; private set; }

    /// <summary>Read-only slice of deltas.</summary>
    public ReadOnlyMemory<L2Delta> Deltas => _buffer.AsMemory(0, _count);

    private L2Delta[] _buffer;
    private int _count;
    private bool _pooled;

    /// <summary>Create an empty pooled update with rented capacity.</summary>
    public L2UpdatePooled(int initialCapacity = 32)
    {
        _buffer = ArrayPool<L2Delta>.Shared.Rent(Math.Max(1, initialCapacity));
        _pooled = true;
        _count = 0;
    }

    /// <summary>Set header values.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void SetHeader(Symbol symbol, long eventTimeMs, bool isSnapshot, ulong first, ulong last, ulong prev)
    {
        Symbol = symbol;
        EventTimeMs = eventTimeMs;
        IsSnapshot = isSnapshot;
        FirstUpdateId = first;
        LastUpdateId = last;
        PrevLastUpdateId = prev;
    }

    /// <summary>Append a single delta (auto-grows the rented buffer).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void AddDelta(in L2Delta d)
    {
        if (_count >= _buffer.Length)
        {
            var newArr = ArrayPool<L2Delta>.Shared.Rent(_buffer.Length * 2);
            Array.Copy(_buffer, 0, newArr, 0, _count);
            ArrayPool<L2Delta>.Shared.Return(_buffer, clearArray: false);
            _buffer = newArr;
        }

        _buffer[_count++] = d;
    }

    /// <summary>Resets the instance for reuse (keeps the rented buffer).</summary>
    public void Clear()
    {
        _count = 0;
        Symbol = default;
        EventTimeMs = 0;
        FirstUpdateId = LastUpdateId = PrevLastUpdateId = 0;
        IsSnapshot = false;
    }

    /// <summary>
    /// Creates a non-pooled immutable <see cref="L2Update"/> snapshot. This copies deltas once.
    /// Prefer staying in pooled form if possible.
    /// </summary>
    public L2Update ToImmutable()
    {
        var dst = new L2Delta[_count];
        Array.Copy(_buffer, 0, dst, 0, _count);
        return new L2Update(Symbol, EventTimeMs, IsSnapshot, FirstUpdateId, LastUpdateId, PrevLastUpdateId, dst);
    }

    /// <summary>Returns rented memory to the pool. Do not use the instance after calling.</summary>
    public void Dispose()
    {
        if (_pooled)
        {
            ArrayPool<L2Delta>.Shared.Return(_buffer, clearArray: false);
            _pooled = false;
        }

        _buffer = Array.Empty<L2Delta>();
        _count = 0;
    }

    /// <summary>
    /// Reads a pooled update from <paramref name="src"/> binary buffer produced by <see cref="L2Update.TryWrite"/>.
    /// No array allocations; deltas are placed into a rented buffer.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> src, out L2UpdatePooled pooled)
    {
        pooled = default!;
        var symSize = Unsafe.SizeOf<Symbol>();
        if (src.Length < symSize + 8 + 1 + 8 + 8 + 8 + 4)
            return false;

        var p = 0;

        // Symbol
        var symbol = MemoryMarshal.Read<Symbol>(src);
        p += symSize;

        // EventTimeMs
        var eventTime = Unsafe.ReadUnaligned<long>(ref MemoryMarshal.GetReference(src[p..]));
        p += 8;

        // IsSnapshot
        var isSnap = src[p++] != 0;

        // IDs
        var first = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(src[p..]));
        p += 8;
        var last = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(src[p..]));
        p += 8;
        var prev = Unsafe.ReadUnaligned<ulong>(ref MemoryMarshal.GetReference(src[p..]));
        p += 8;

        // Count
        var count = Unsafe.ReadUnaligned<int>(ref MemoryMarshal.GetReference(src[p..]));
        p += 4;
        if (count < 0)
            return false;

        var bytes = count * Unsafe.SizeOf<L2Delta>();
        if (src.Length < p + bytes)
            return false;

        var obj = new L2UpdatePooled(count);
        obj.SetHeader(symbol, eventTime, isSnap, first, last, prev);

        // copy deltas block without extra allocations
        var span = MemoryMarshal.Cast<byte, L2Delta>(src[p..(p + bytes)]);
        for (var i = 0; i < span.Length; i++)
            obj.AddDelta(in span[i]);

        pooled = obj;
        return true;
    }
}
