using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CryptoCore.Primitives;

namespace CryptoCore.MarketData;

/// <summary>
/// Batch of level-2 updates for a single <see cref="Symbol"/>. Works for both snapshot and incremental.
/// Compatible with Binance/Tardis: carries update ids and <see cref="IsSnapshot"/> flag.
/// Provides allocation-free binary (de)serialization when caller supplies buffers.
/// </summary>
public sealed class L2Update
{
    /// <summary>Instrument symbol these changes belong to.</summary>
    public Symbol Symbol { get; }

    /// <summary>Unix time in milliseconds when the event was emitted by venue (if known).</summary>
    public long EventTimeMs { get; }

    /// <summary>
    /// Venue sequencing (Binance-like): first/last update ids of this batch and previous-last.
    /// Use <see cref="PrevLastUpdateId"/> to validate continuity (prev == order book’s last).
    /// </summary>
    public ulong FirstUpdateId { get; }

    /// <summary>Last (inclusive) exchange update id for this batch. Zero when unknown.</summary>
    public ulong LastUpdateId { get; }

    /// <summary>Previous last update id (Binance: <c>pu</c>) for continuity checks. Zero when unknown.</summary>
    public ulong PrevLastUpdateId { get; }

    /// <summary>True when the batch is a full snapshot (replace all levels).</summary>
    public bool IsSnapshot { get; }

    /// <summary>Read-only view of constituent deltas.</summary>
    public ReadOnlyMemory<L2Delta> Deltas => _deltas;

    private readonly L2Delta[] _deltas;

    /// <summary>Create a new update. Caller may supply already-pooled array.</summary>
    public L2Update(
        Symbol symbol,
        long eventTimeMs,
        bool isSnapshot,
        ulong firstUpdateId,
        ulong lastUpdateId,
        ulong prevLastUpdateId,
        L2Delta[] deltas)
    {
        Symbol = symbol;
        EventTimeMs = eventTimeMs;
        IsSnapshot = isSnapshot;
        FirstUpdateId = firstUpdateId;
        LastUpdateId = lastUpdateId;
        PrevLastUpdateId = prevLastUpdateId;
        _deltas = deltas;
    }

    /// <summary>Convenience factory for snapshot.</summary>
    public static L2Update Snapshot(Symbol symbol, long eventTimeMs, L2Delta[] deltas)
        => new(symbol, eventTimeMs, isSnapshot: true, firstUpdateId: 0, lastUpdateId: 0, prevLastUpdateId: 0, deltas: deltas);

    /// <summary>
    /// Binary size of a single <see cref="L2Delta"/> in bytes (for buffer sizing).
    /// </summary>
    public static readonly int DeltaSize = Unsafe.SizeOf<L2Delta>();

    /// <summary>
    /// Write to a binary buffer: header + count + deltas. Returns false if buffer too small.
    /// Layout (little-endian):
    /// [Symbol(…bytes…)][Int64 EventTime][Byte IsSnapshot][UInt64 First][UInt64 Last][UInt64 PrevLast][Int32 Count][Deltas...]
    /// </summary>
    public bool TryWrite(Span<byte> dst, out int written)
    {
        written = 0;

        var symSize = Unsafe.SizeOf<Symbol>();
        var need = symSize + 8 + 1 + 8 + 8 + 8 + 4 + _deltas.Length * DeltaSize;
        if (dst.Length < need)
            return false;

        // 1) Symbol — пишем из локальной копии (нельзя брать ref к свойству напрямую)
        var sym = Symbol; // локальная копия -> можно брать ref
        MemoryMarshal.Write(dst, in sym);
        var p = symSize;

        // 2) EventTimeMs (Int64)
        Unsafe.WriteUnaligned(ref dst[p], EventTimeMs);
        p += 8;

        // 3) IsSnapshot (byte)
        dst[p++] = IsSnapshot ? (byte)1 : (byte)0;

        // 4) First/Last/Prev (UInt64)
        Unsafe.WriteUnaligned(ref dst[p], FirstUpdateId);
        p += 8;
        Unsafe.WriteUnaligned(ref dst[p], LastUpdateId);
        p += 8;
        Unsafe.WriteUnaligned(ref dst[p], PrevLastUpdateId);
        p += 8;

        // 5) Count (Int32)
        Unsafe.WriteUnaligned(ref dst[p], _deltas.Length);
        p += 4;

        // 6) Deltas block
        var deltasBytes = MemoryMarshal.AsBytes(_deltas.AsSpan());
        deltasBytes.CopyTo(dst[p..]);
        p += deltasBytes.Length;

        written = p;
        return true;
    }

    /// <summary>
    /// Read from binary form produced by <see cref="TryWrite"/>. Allocates array for deltas.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> src, out L2Update update)
    {
        update = default!;

        var symSize = Unsafe.SizeOf<Symbol>();
        if (src.Length < symSize + 8 + 1 + 8 + 8 + 8 + 4)
            return false;

        // Symbol
        var symbol = MemoryMarshal.Read<Symbol>(src);
        var p = symSize;

        // EventTime
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

        var need = p + count * DeltaSize;
        if (src.Length < need)
            return false;

        var deltas = new L2Delta[count];
        src[p..need].CopyTo(MemoryMarshal.AsBytes(deltas.AsSpan()));

        update = new L2Update(symbol, eventTime, isSnap, first, last, prev, deltas);
        return true;
    }
}
