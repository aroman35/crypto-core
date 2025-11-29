using CryptoCore.Primitives;
using CryptoCore.Storage.Extensions;
using CryptoCore.Storage.Models.Enums;
using Shouldly;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// Tests for bit-packing helpers on PackedMarketData24.Flags.
/// </summary>
public class MarketDataFlagsTests
{
    [Fact]
    public void PackUnpack_L2Update_BuySnapshot()
    {
        var flags = MarketDataFlags.Pack(
            MarketDataMessageType.L2Update,
            Side.Buy,
            isSnapshot: true);

        MarketDataFlags.Unpack(flags, out var type, out var side, out var isSnapshot);

        type.ShouldBe(MarketDataMessageType.L2Update);
        side.ShouldBe(Side.Buy);
        isSnapshot.ShouldBeTrue();
    }

    [Fact]
    public void PackUnpack_Trade_Sell_NoSnapshot()
    {
        var flags = MarketDataFlags.Pack(
            MarketDataMessageType.Trade,
            Side.Sell,
            isSnapshot: false);

        MarketDataFlags.Unpack(flags, out var type, out var side, out var isSnapshot);

        type.ShouldBe(MarketDataMessageType.Trade);
        side.ShouldBe(Side.Sell);
        isSnapshot.ShouldBeFalse();
    }
}