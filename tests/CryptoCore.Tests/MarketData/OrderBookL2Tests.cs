using System.Runtime.CompilerServices;
using CryptoCore.Extensions;
using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.MarketData;

public class OrderBookL2Tests
{
    [Fact(DisplayName = "L2Update: binary round-trip")]
    public void Update_Binary()
    {
        var s = Symbol.Parse("BTCUSDT").For(Exchange.BinanceFutures);
        var deltas = new[]
        {
            new L2Delta(Side.Buy, 40000.0, 1.5),
            new L2Delta(Side.Sell, 40010.0, 2.0),
            new L2Delta(Side.Sell, 40020.0, 0.0), // remove
        };
        var u = new L2Update(s, 1700, false, 101, 102, 100, deltas);

        Span<byte> buf = stackalloc byte[Unsafe.SizeOf<Symbol>() + 8 + 1 + 8 + 8 + 8 + 4 + deltas.Length * L2Update.DeltaSize];
        u.TryWrite(buf, out var written).ShouldBeTrue();

        L2Update.TryRead(buf[..written], out var back).ShouldBeTrue();
        back.Symbol.ShouldBe(s);
        back.FirstUpdateId.ShouldBe(101UL);
        back.LastUpdateId.ShouldBe(102UL);
        back.PrevLastUpdateId.ShouldBe(100UL);
        back.IsSnapshot.ShouldBeFalse();
        back.Deltas.ToArray().Length.ShouldBe(3);
        back.Deltas.Span[2].IsRemove.ShouldBeTrue();
    }

    [Fact(DisplayName = "OrderBook: apply snapshot then deltas (Binance continuity)")]
    public void Book_Snapshot_Then_Deltas()
    {
        var s = Symbol.Parse("ETHUSDT").For(Exchange.BinanceSpot);
        var book = new OrderBookL2(s);

        // snapshot: 2x bid, 2x ask
        var snap = L2Update.Snapshot(s, 1000, new[]
        {
            new L2Delta(Side.Buy, 3500.0, 5.0),
            new L2Delta(Side.Buy, 3499.5, 2.0),
            new L2Delta(Side.Sell, 3500.5, 3.0),
            new L2Delta(Side.Sell, 3501.0, 1.0),
        });

        book.Apply(snap).ShouldBeTrue();
        book.BidLevels.ShouldBe(2);
        book.AskLevels.ShouldBe(2);

        var (bb, bq) = book.BestBid();
        var (ba, aq) = book.BestAsk();
        bb.IsEquals(3500.0).ShouldBeTrue(); bq.IsEquals(5.0).ShouldBeTrue();
        ba.IsEquals(3500.5).ShouldBeTrue(); aq.IsEquals(3.0).ShouldBeTrue();

        // delta with continuity ok (PrevLast == book.Last == 0 → passes)
        var d1 = new L2Update(s, 1010, false, 1, 2, 0, new[]
        {
            new L2Delta(Side.Buy, 3500.0, 0.0), // remove best bid
            new L2Delta(Side.Sell, 3500.4, 2.5) // new best ask
        });
        book.Apply(d1).ShouldBeTrue();

        book.BidLevels.ShouldBe(1);
        (bb, bq) = book.BestBid();
        bb.IsEquals(3499.5).ShouldBeTrue();

        (ba, aq) = book.BestAsk();
        ba.IsEquals(3500.4).ShouldBeTrue();
        aq.IsEquals(2.5).ShouldBeTrue();

        // continuity check: set book.LastUpdateId and test mismatch
        typeof(OrderBookL2).GetProperty(nameof(OrderBookL2.LastUpdateId))!
            .SetValue(book, (ulong) 2); // emulate last applied id

        var wrong = new L2Update(s, 1020, false, 3, 4, prevLastUpdateId: 1, deltas: new[]
        {
            new L2Delta(Side.Buy, 3499.8, 4.0)
        });
        book.Apply(wrong).ShouldBeFalse(); // rejected

        var ok = new L2Update(s, 1020, false, 3, 4, prevLastUpdateId: 2, deltas: new[]
        {
            new L2Delta(Side.Buy, 3499.8, 4.0)
        });
        book.Apply(ok).ShouldBeTrue();
        (bb, bq) = book.BestBid();
        bb.IsEquals(3499.8).ShouldBeTrue();
    }

    [Fact(DisplayName = "OrderBook: Tardis first batches as snapshot")]
    public void Book_Tardis_Snapshot_Flag()
    {
        var s = Symbol.Parse("BTC-USDT@OKXSpot");
        var book = new OrderBookL2(s);

        // Tardis: first updates have isSnapshot=true
        var tSnap = L2Update.Snapshot(s, 2000, new[]
        {
            new L2Delta(Side.Buy, 40000.0, 1.0),
            new L2Delta(Side.Sell, 40001.0, 2.0),
        });
        book.Apply(tSnap).ShouldBeTrue();

        var add = new L2Update(s, 2010, false, 0, 0, 0, new[]
        {
            new L2Delta(Side.Sell, 40001.0, 0.5), // reduce best ask
            new L2Delta(Side.Buy, 39999.5, 3.0)  // new bid level
        });
        book.Apply(add).ShouldBeTrue();

        book.BidLevels.ShouldBe(2);
        book.AskLevels.ShouldBe(1);
    }
}
