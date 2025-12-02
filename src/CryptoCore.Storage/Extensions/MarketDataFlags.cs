using System.Runtime.CompilerServices;
using CryptoCore.Primitives;
using CryptoCore.Storage.Models;
using CryptoCore.Storage.Models.Enums;

namespace CryptoCore.Storage.Extensions;

/// <summary>
/// Bit-level helper for encoding and decoding the <see cref="PackedMarketData24.Flags"/> field.
/// </summary>
public static class MarketDataFlags
{
    // Layout:
    // bits  0-1 : message type (MarketDataMessageType)
    // bits  2-3 : side (0=Undefined, 1=Buy, 2=Sell)
    // bit      4: IsSnapshot (for L2)
    // bits  5-31: reserved

    private const int TypeBits = 0;
    private const int SideBits = 2;
    private const int SnapshotBit = 4;

    private const int TypeMask = 0b11 << TypeBits;
    private const int SideMask = 0b11 << SideBits;
    private const int SnapshotMask = 1 << SnapshotBit;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Pack(
        MarketDataMessageType type,
        Side side,
        bool isSnapshot)
    {
        int flags = 0;

        // type
        flags |= ((int)type & 0b11) << TypeBits;

        // side
        int sideBits = side switch
        {
            Side.Buy => 0b01,
            Side.Sell => 0b10,
            _ => 0b00
        };
        flags |= (sideBits & 0b11) << SideBits;

        // snapshot
        if (isSnapshot)
            flags |= SnapshotMask;

        return flags;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Unpack(
        int flags,
        out MarketDataMessageType type,
        out Side side,
        out bool isSnapshot)
    {
        type = GetMessageType(flags);

        var sideBits = (flags & SideMask) >> SideBits;
        side = sideBits switch
        {
            0b01 => Side.Buy,
            0b10 => Side.Sell,
            _ => Side.None
        };

        isSnapshot = (flags & SnapshotMask) != 0;
    }

    /// <summary>
    /// Returns the message type encoded in the flags (L2Update or Trade).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static MarketDataMessageType GetMessageType(int flags)
    {
        var typeBits = (flags & TypeMask) >> TypeBits;
        return typeBits switch
        {
            0b00 => MarketDataMessageType.L2Update,
            0b01 => MarketDataMessageType.Trade,
            _ => MarketDataMessageType.L2Update // резерв, дефолтим в L2
        };
    }

    /// <summary>
    /// Returns true if the flags encode an L2 update message.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsL2Update(int flags)
        => GetMessageType(flags) == MarketDataMessageType.L2Update;

    /// <summary>
    /// Returns true if the flags encode a trade message.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsTrade(int flags)
        => GetMessageType(flags) == MarketDataMessageType.Trade;
}
