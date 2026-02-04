using System.Runtime.InteropServices;
using CryptoCore.Storage.Models.Enums;
using CryptoCore.Storage.Models.Primitives;

namespace CryptoCore.Storage.Models;

/// <summary>
/// Unified packed record used for both L2 updates and trades.
/// The struct is exactly 24 bytes in size and is designed for
/// efficient binary storage and sequential streaming.
/// </summary>
[FeedType(FeedType.Combined, 1, 0, 0)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct PackedMarketData24(int timeMs, Decimal9 price, Decimal9 quantity, int flags)
{
    /// <summary>
    /// Milliseconds since the start of the trading day (UTC).
    /// Range: [0 .. 86_399_999].
    /// The actual absolute timestamp is reconstructed from
    /// <see cref="MarketDataHash.Date"/> + <see cref="TimeMs"/>.
    /// </summary>
    public readonly int TimeMs = timeMs; // 4 bytes

    /// <summary>
    /// Fixed-point price encoded as <see cref="Decimal9"/>.
    /// The numeric value is <c>Price.ToDecimal()</c>.
    /// </summary>
    public readonly Decimal9 Price = price; // 8 bytes (wrapped long)

    /// <summary>
    /// Fixed-point quantity/size encoded as <see cref="Decimal9"/>.
    /// The numeric value is <c>Quantity.ToDecimal()</c>.
    /// </summary>
    public readonly Decimal9 Quantity = quantity; // 8 bytes (wrapped long)

    /// <summary>
    /// Bit-packed metadata for this record.
    /// The recommended bit layout is:
    /// <list type="bullet">
    ///   <item><description>bits 0–1: <see cref="MarketDataMessageType"/> (0 = L2, 1 = Trade)</description></item>
    ///   <item><description>bits 2–3: side (0 = undefined, 1 = long/bid/buy, 2 = short/ask/sell)</description></item>
    ///   <item><description>bit 4: IsSnapshot (for L2 updates)</description></item>
    ///   <item><description>bits 5–31: reserved for future use</description></item>
    /// </list>
    /// </summary>
    public readonly int Flags = flags; // 4 bytes
}
