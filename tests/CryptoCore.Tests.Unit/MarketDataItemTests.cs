using CryptoCore.Primitives;
using CryptoCore.Storage.Extensions;
using CryptoCore.Storage.Models;
using Shouldly;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// Tests for MarketDataItem{T}.
/// </summary>
public class MarketDataItemTests
{
    [Fact]
    public void MarketDataItem_Timestamp_And_Date_And_Time()
    {
        var ts = new DateTimeOffset(2025, 2, 2, 10, 30, 0, TimeSpan.Zero);
        long unixMs = ts.ToUnixMillisecondsTimestamp();

        var trade = new Trade(ts, Side.Buy, 1.23m, 4.56m);
        var item = new MarketDataItem<Trade>(trade, unixMs);

        item.Timestamp.ShouldBe(unixMs);
        item.DateTime.ShouldBe(ts.UtcDateTime);
        item.Date.ShouldBe(ts.ToUtcDateOnly());
        item.Time.ShouldBe(ts.ToUtcTimeOnly());
    }
}