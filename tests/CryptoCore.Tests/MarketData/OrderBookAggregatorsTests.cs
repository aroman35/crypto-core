using CryptoCore.Extensions;
using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.MarketData;

public class OrderBookAggregatorsTests
{
    [Fact(DisplayName = "VWAP and Imbalance over top-N")]
    public void Aggregators()
    {
        var s = Symbol.Parse("ETHUSDT").For(Exchange.BinanceSpot);
        var book = new OrderBookL2(s);

        var snap = L2Update.Snapshot(s, 0, new[]
        {
            new L2Delta(Side.Buy, 100, 5),
            new L2Delta(Side.Buy,  99, 2),
            new L2Delta(Side.Sell,101, 3),
            new L2Delta(Side.Sell,102, 1),
        });
        book.Apply(snap).ShouldBeTrue();

        var (vwapBid, qb) = book.ComputeVwap(Side.Buy, topLevels: 2);
        qb.IsEquals(7).ShouldBeTrue();
        vwapBid.IsEquals(((100*5)+(99*2))/7.0).ShouldBeTrue();

        var (vwapAsk, qa) = book.ComputeVwap(Side.Sell, topLevels: 1);
        qa.IsEquals(3).ShouldBeTrue();
        vwapAsk.IsEquals(101).ShouldBeTrue();

        var imb1 = book.ComputeTopImbalance(1); // (5 - 3)/(5+3) = 0.25
        imb1.IsEquals(0.25).ShouldBeTrue();
    }

    [Fact(DisplayName = "Cancellation counters increment on true removals")]
    public void Cancellation_Counters()
    {
        var s = Symbol.Parse("BTCUSDT").For(Exchange.BinanceSpot);
        var book = new OrderBookL2(s);

        book.Apply(L2Update.Snapshot(s, 0, new[]
        {
            new L2Delta(Side.Buy,  10, 1),
            new L2Delta(Side.Sell, 11, 1),
        })).ShouldBeTrue();

        var add = new L2Update(s, 1, false, 0, 0, 0, new[]
        {
            new L2Delta(Side.Buy,  10, 0),  // remove bid
            new L2Delta(Side.Sell, 12, 0),  // remove non-existing ask → не считаем
            new L2Delta(Side.Sell, 11, 0),  // remove ask
        });
        book.Apply(add).ShouldBeTrue();

        var (cb, ca) = book.CancellationCounters;
        cb.ShouldBe(1);
        ca.ShouldBe(1);

        book.ResetCancellationCounters();
        (cb, ca) = book.CancellationCounters;
        cb.ShouldBe(0);
        ca.ShouldBe(0);
    }
}
