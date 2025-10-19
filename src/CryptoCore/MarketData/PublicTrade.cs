using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CryptoCore.Extensions;
using CryptoCore.Primitives;

namespace CryptoCore.MarketData;

/// <summary>
/// Compact, allocation-free value type representing an anonymous trade tick from an exchange stream or archive.
/// It stores the instrument (<see cref="Symbol"/>), trade id, timestamp (Unix ms), price, size, and flags
/// (aggressor side / maker / liquidation). Provides zero-allocation binary serialization to/from <see cref="Span{T}"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct PublicTrade : IEquatable<PublicTrade>
{
    /// <summary>
    /// Bit flags describing attributes of a trade. Kept in a single byte for compactness.
    /// </summary>
    [Flags]
    public enum TradeAttributes : byte
    {
        /// <summary>No flags set.</summary>
        None = 0,

        /// <summary>Buyer was the aggressor (taker).</summary>
        AggressorBuy = 1 << 0,

        /// <summary>Seller was the aggressor (taker).</summary>
        AggressorSell = 1 << 1,

        /// <summary>Event is marked as maker fill (if provided by the venue).</summary>
        Maker = 1 << 2,

        /// <summary>Trade is flagged as liquidation (if provided by the venue).</summary>
        Liquidation = 1 << 3,
    }

    /// <summary>Instrument symbol (includes exchange preset semantics).</summary>
    public Symbol Symbol;

    /// <summary>Exchange trade id (64-bit). Use venue's integer id when available; otherwise an internal rolling id.</summary>
    public ulong TradeId;

    /// <summary>Unix timestamp in milliseconds.</summary>
    public long TimestampMs;

    /// <summary>Trade price.</summary>
    public double Price;

    /// <summary>Executed quantity (base asset amount).</summary>
    public double Quantity;

    /// <summary>Attributes: aggressor side, maker, liquidation.</summary>
    public TradeAttributes Attributes;

    /// <summary>
    /// Creates a new <see cref="PublicTrade"/>.
    /// </summary>
    public static PublicTrade Create(Symbol symbol, ulong tradeId, long tsMs, double price, double qty, TradeAttributes attributes = TradeAttributes.None)
        => new PublicTrade
        {
            Symbol = symbol,
            TradeId = tradeId,
            TimestampMs = tsMs,
            Price = price,
            Quantity = qty,
            Attributes = attributes
        };

    /// <summary>
    /// Aggressor side, inferred from <see cref="Attributes"/>; <see cref="Side.None"/> when unknown.
    /// </summary>
    public readonly Side Aggressor =>
        (Attributes & TradeAttributes.AggressorBuy) != 0 ? Side.Buy :
        (Attributes & TradeAttributes.AggressorSell) != 0 ? Side.Sell : Side.None;

    /// <summary>
    /// Returns a string like "<c>BTCUSDT 1700000000000 P=40000 Q=0.5 Side=Buy Flags=AggressorBuy|Maker</c>".
    /// </summary>
    public readonly override string ToString()
        => $"{Symbol} {TimestampMs} P={Price} Q={Quantity} Side={Aggressor} Flags={Attributes}";

    /// <summary>
    /// Binary size (in bytes) of <see cref="PublicTrade"/> for the current build; useful to pre-size buffers.
    /// </summary>
    public static readonly int BinarySize = Unsafe.SizeOf<PublicTrade>();

    /// <summary>
    /// Writes the struct bytes to <paramref name="destination"/> (little-endian layout of the process).
    /// No allocations; returns <c>false</c> when buffer is too small.
    /// </summary>
    public readonly bool TryWrite(Span<byte> destination, out int written)
    {
        var size = BinarySize;
        if (destination.Length < size)
        {
            written = 0;
            return false;
        }

        var src = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref Unsafe.AsRef(in this), 1));
        src.CopyTo(destination);
        written = size;
        return true;
    }

    /// <summary>
    /// Reads a <see cref="PublicTrade"/> from <paramref name="source"/> previously produced by <see cref="TryWrite"/>.
    /// </summary>
    public static bool TryRead(ReadOnlySpan<byte> source, out PublicTrade trade)
    {
        var size = BinarySize;
        if (source.Length < size)
        {
            trade = default;
            return false;
        }

        trade = MemoryMarshal.Read<PublicTrade>(source);
        return true;
    }

    /// <summary>Value equality by field contents.</summary>
    public readonly bool Equals(PublicTrade other)
        => Symbol.Equals(other.Symbol)
           && TradeId == other.TradeId
           && TimestampMs == other.TimestampMs
           && Price.IsEquals(other.Price)
           && Quantity.IsEquals(other.Quantity)
           && Attributes == other.Attributes;

    /// <inheritdoc/>
    public readonly override bool Equals(object? obj) => obj is PublicTrade t && Equals(t);

    /// <inheritdoc/>
    public readonly override int GetHashCode()
    {
        unchecked
        {
            var h = 17;
            h = (h * 31) + Symbol.GetHashCode();
            h = (h * 31) + TradeId.GetHashCode();
            h = (h * 31) + TimestampMs.GetHashCode();
            h = (h * 31) + Price.GetHashCode();
            h = (h * 31) + Quantity.GetHashCode();
            h = (h * 31) + Attributes.GetHashCode();
            return h;
        }
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(PublicTrade left, PublicTrade right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(PublicTrade left, PublicTrade right) => !left.Equals(right);

    /// <summary>
    /// Returns a copy with the provided <see cref="TradeAttributes"/> merged in (bitwise OR).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly PublicTrade WithFlags(TradeAttributes add)
    {
        var t = this;
        t.Attributes |= add;
        return t;
    }

    /// <summary>
    /// Returns a copy with the <see cref="Symbol"/> re-bound (e.g., for mapping feed → canonical).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public readonly PublicTrade WithSymbol(Symbol s)
    {
        var t = this;
        t.Symbol = s;
        return t;
    }

    /// <summary>
    /// Returns a copy with the aggressor side set (updates <see cref="Attributes"/> accordingly).
    /// </summary>
    public readonly PublicTrade WithAggressor(Side side)
    {
        var t = this;
        // сбрасываем агрессора
        t.Attributes &= ~(TradeAttributes.AggressorBuy | TradeAttributes.AggressorSell);
        // выставляем новый
        if (side == Side.Buy)
            t.Attributes |= TradeAttributes.AggressorBuy;
        if (side == Side.Sell)
            t.Attributes |= TradeAttributes.AggressorSell;
        return t;
    }
}
