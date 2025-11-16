using CryptoConnector.Binance.Common;
using CryptoConnector.Binance.Parsers;
using CryptoCore.Extensions;
using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Unit.MarketData;

public class OrderBookPooledApplyTests
{
    [Fact(DisplayName = "OrderBook: apply pooled snapshot then pooled delta")]
    public void Pooled_Snapshot_Then_Delta()
    {
        var sym = Symbol.Parse("BTCUSDT").For(Exchange.BinanceSpot);
        var book = new OrderBookL2(sym);

        // pooled snapshot
        using (var snap = new L2UpdatePooled(initialCapacity: 4))
        {
            snap.SetHeader(sym, eventTimeMs: 1000, isSnapshot: true, first: 0, last: 0, prev: 0);
            snap.AddDelta(new L2Delta(Side.Buy,  40000.0, 1.5));
            snap.AddDelta(new L2Delta(Side.Sell, 40001.0, 2.0));

            book.Apply(snap).ShouldBeTrue();
        }

        var (bb, bq) = book.BestBid();
        var (ba, aq) = book.BestAsk();
        bb.IsEquals(40000.0).ShouldBeTrue(); bq.IsEquals(1.5).ShouldBeTrue();
        ba.IsEquals(40001.0).ShouldBeTrue(); aq.IsEquals(2.0).ShouldBeTrue();

        // pooled delta with continuity check
        using (var upd = new L2UpdatePooled(initialCapacity: 2))
        {
            upd.SetHeader(sym, eventTimeMs: 1010, isSnapshot: false, first: 101, last: 102, prev: 0);
            upd.AddDelta(new L2Delta(Side.Buy, 40000.0, 0.0)); // remove best bid
            upd.AddDelta(new L2Delta(Side.Sell, 40000.5, 3.0)); // new ask level closer

            book.Apply(upd).ShouldBeTrue();
        }

        (bb, bq) = book.BestBid();
        bb.IsEquals(0).ShouldBeTrue(); bq.IsEquals(0).ShouldBeTrue();

        (ba, aq) = book.BestAsk();
        ba.IsEquals(40000.5).ShouldBeTrue(); aq.IsEquals(3.0).ShouldBeTrue();
    }

    [Fact(DisplayName = "OrderBook: apply Binance WS JSON via pooled parser")]
    public void Pooled_From_Binance_Json()
    {
        var json = @"
        {
          ""e"": ""depthUpdate"",
          ""E"": 1700000000123,
          ""s"": ""BTCUSDT"",
          ""U"": 100,
          ""u"": 102,
          ""pu"": 99,
          ""b"": [[""40000.1"", ""0.5""], [""39999.9"", ""0""]],
          ""a"": [[""40000.2"", ""1.0""]]
        }"u8.ToArray();

        var book = new OrderBookL2(Symbol.Parse("BTCUSDT").For(Exchange.BinanceSpot));
        var provider = new SimpleSymbolProvider();

        BinanceDepthParser.TryParseDepthUpdate(json, provider, Exchange.BinanceSpot, out var pooled).ShouldBeTrue();
        using (pooled)
        {
            // apply pooled deltas directly
            book.Apply(pooled).ShouldBeTrue();

            var (bb, bq) = book.BestBid();
            var (ba, aq) = book.BestAsk();

            bb.IsEquals(40000.1).ShouldBeTrue(); bq.IsEquals(0.5).ShouldBeTrue();
            ba.IsEquals(40000.2).ShouldBeTrue(); aq.IsEquals(1.0).ShouldBeTrue();
        }
    }
}
