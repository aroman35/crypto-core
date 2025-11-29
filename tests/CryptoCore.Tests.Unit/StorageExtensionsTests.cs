using CryptoCore.Primitives;
using CryptoCore.Storage.Extensions;
using CryptoCore.Storage.Models;
using Shouldly;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// Tests for conversion between domain models and PackedMarketData24.
/// </summary>
public class StorageExtensionsTests
{
    [Fact]
    public void LevelUpdate_ToStorage_And_Back_Roundtrip()
    {
        var ts = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var update = new LevelUpdate(
            ts,
            Side.Buy,
            12345.123456789m,
            0.000000001m,
            true);

        var packed = update.ToStorage();
        var back = packed.ToLevelUpdate(update.TradeDate);

        back.Timestamp.ShouldBe(update.Timestamp);
        back.Side.ShouldBe(update.Side);
        back.Price.ShouldBe(update.Price);
        back.Quantity.ShouldBe(update.Quantity);
        back.IsSnapshot.ShouldBe(update.IsSnapshot);
    }

    [Fact]
    public void Trade_ToStorage_And_Back_Roundtrip()
    {
        var ts = new DateTimeOffset(2025, 1, 1, 12, 0, 1, TimeSpan.Zero);
        var trade = new Trade(
            ts,
            Side.Sell,
            9999.999999999m,
            1.000000001m);

        var packed = trade.ToStorage();
        var back = packed.ToTrade(trade.TradeDate);

        back.Timestamp.ShouldBe(trade.Timestamp);
        back.Side.ShouldBe(trade.Side);
        back.Price.ShouldBe(trade.Price);
        back.Quantity.ShouldBe(trade.Quantity);
    }
}