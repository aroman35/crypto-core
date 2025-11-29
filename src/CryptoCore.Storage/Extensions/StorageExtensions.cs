using System.Runtime.CompilerServices;
using CryptoCore.Storage.Models;
using CryptoCore.Storage.Models.Enums;
using CryptoCore.Storage.Models.Primitives;

namespace CryptoCore.Storage.Extensions;

/// <summary>
/// Extension methods for converting between domain models
/// (<see cref="LevelUpdate"/>, <see cref="Trade"/>) and storage model
/// (<see cref="PackedMarketData24"/>).
/// </summary>
public static class StorageExtensions
{
    /// <summary>
    /// Converts a domain-level Level 2 update into a packed 24-byte storage record.
    /// The <see cref="DateTimeOffset"/> timestamp is converted to milliseconds
    /// since the start of the trading day (UTC), and price/quantity are encoded
    /// as <see cref="Decimal9"/>.
    /// </summary>
    /// <param name="update">Domain-level Level 2 update.</param>
    /// <returns>Packed storage record representing the same event.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PackedMarketData24 ToStorage(this LevelUpdate update)
    {
        // Convert timestamp to UTC and take milliseconds from start of day.
        var utc = update.Timestamp.UtcDateTime;
        var timeMs = (int)utc.TimeOfDay.TotalMilliseconds;

        var flags = MarketDataFlags.Pack(
            type: MarketDataMessageType.L2Update,
            side: update.Side,
            isSnapshot: update.IsSnapshot);

        var price = new Decimal9(update.Price);
        var quantity = new Decimal9(update.Quantity);

        return new PackedMarketData24(
            timeMs,
            price,
            quantity,
            flags);
    }

    /// <summary>
    /// Converts a packed storage record into a domain-level Level 2 update.
    /// </summary>
    /// <param name="packed">Packed storage record.</param>
    /// <param name="tradeDate">
    /// Trading date (UTC) taken from the file header (<see cref="MarketDataHash.Date"/>).
    /// </param>
    /// <returns>Domain-level <see cref="LevelUpdate"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static LevelUpdate ToLevelUpdate(this PackedMarketData24 packed, DateOnly tradeDate)
    {
        MarketDataFlags.Unpack(packed.Flags, out var type, out var side, out var isSnapshot);

        // Optionally: assert that type == L2Update
        // if (type != MarketDataMessageType.L2Update) throw ...

        var dayStartUtc = tradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dtUtc = dayStartUtc.AddMilliseconds(packed.TimeMs);
        var timestamp = new DateTimeOffset(dtUtc, TimeSpan.Zero);

        var price = packed.Price.ToDecimal();
        var quantity = packed.Quantity.ToDecimal();

        return new LevelUpdate(
            timestamp,
            side,
            price,
            quantity,
            isSnapshot);
    }

    /// <summary>
    /// Converts a domain-level trade into a packed 24-byte storage record.
    /// The <see cref="DateTimeOffset"/> timestamp is converted to milliseconds
    /// since the start of the trading day (UTC), and price/quantity are encoded
    /// as <see cref="Decimal9"/>.
    /// Trade identifiers are intentionally not stored in the packed format.
    /// </summary>
    /// <param name="trade">Domain-level trade.</param>
    /// <returns>Packed storage record representing the same event.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PackedMarketData24 ToStorage(this Trade trade)
    {
        var utc = trade.Timestamp.UtcDateTime;
        var timeMs = (int)utc.TimeOfDay.TotalMilliseconds;

        var flags = MarketDataFlags.Pack(
            type: MarketDataMessageType.Trade,
            side: trade.Side,
            isSnapshot: false);

        var price = new Decimal9(trade.Price);
        var quantity = new Decimal9(trade.Quantity);

        return new PackedMarketData24(
            timeMs,
            price,
            quantity,
            flags);
    }

    /// <summary>
    /// Converts a packed storage record into a domain-level trade.
    /// </summary>
    /// <param name="packed">Packed storage record.</param>
    /// <param name="tradeDate">
    /// Trading date (UTC) taken from the file header (<see cref="MarketDataHash.Date"/>).
    /// </param>
    /// <returns>Domain-level <see cref="Trade"/> instance.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Trade ToTrade(this PackedMarketData24 packed, DateOnly tradeDate)
    {
        MarketDataFlags.Unpack(packed.Flags, out var type, out var side, out var isSnapshot);

        // Optionally: assert that type == Trade
        // if (type != MarketDataMessageType.Trade) throw ...

        var dayStartUtc = tradeDate.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dtUtc = dayStartUtc.AddMilliseconds(packed.TimeMs);
        var timestamp = new DateTimeOffset(dtUtc, TimeSpan.Zero);

        var price = packed.Price.ToDecimal();
        var quantity = packed.Quantity.ToDecimal();

        return new Trade(
            timestamp,
            side,
            price,
            quantity);
    }
}
