using CryptoCore.Extensions;
using CryptoCore.MarketData;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Unit.MarketData;

public class PublicTradeTests
{
    [Fact(DisplayName = "Create trade exposes Side=Buy via Aggressor")]
    public void Basic_Create()
    {
        var s = Symbol.Parse("BTCUSDT").For(Exchange.BinanceFutures);
        var t = PublicTrade.Create(s, 123456789UL, 1700000000000L, 42000.5, 0.75,
            PublicTrade.TradeAttributes.AggressorBuy | PublicTrade.TradeAttributes.Maker);

        t.Aggressor.ShouldBe(Side.Buy);
        (t.Attributes & PublicTrade.TradeAttributes.Maker).ShouldNotBe(PublicTrade.TradeAttributes.None);
    }

    [Fact(DisplayName = "Binary round-trip preserves Aggressor side")]
    public void Binary_RoundTrip()
    {
        var s = Symbol.Parse("ETH-USDT@OKXSwap");
        var t = PublicTrade.Create(s, 42, 1700000100000L, 3500.0, 1.25, PublicTrade.TradeAttributes.AggressorSell);

        Span<byte> buf = stackalloc byte[PublicTrade.BinarySize];
        t.TryWrite(buf, out _).ShouldBeTrue();

        PublicTrade.TryRead(buf, out var back).ShouldBeTrue();
        back.Aggressor.ShouldBe(Side.Sell);
    }

    [Fact(DisplayName = "WithAggressor sets flags and side consistently")]
    public void WithAggressor()
    {
        var s = Symbol.Parse("SOL-USDC@OKXSpot");
        var t = PublicTrade.Create(s, 7, 1, 100.123456, 0.001, PublicTrade.TradeAttributes.None);

        var tBuy = t.WithAggressor(Side.Buy);
        tBuy.Aggressor.ShouldBe(Side.Buy);
        (tBuy.Attributes & PublicTrade.TradeAttributes.AggressorBuy).ShouldNotBe(PublicTrade.TradeAttributes.None);

        var tSell = tBuy.WithAggressor(Side.Sell);
        tSell.Aggressor.ShouldBe(Side.Sell);
        (tSell.Attributes & PublicTrade.TradeAttributes.AggressorSell).ShouldNotBe(PublicTrade.TradeAttributes.None);
        (tSell.Attributes & PublicTrade.TradeAttributes.AggressorBuy).ShouldBe(PublicTrade.TradeAttributes.None);
    }

    [Fact(DisplayName = "Side arithmetic: position += side * qty")]
    public void Side_Arithmetic()
    {
        double pos = 0;
        pos += Side.Buy.Mul(2.5);   // +2.5
        pos += Side.Sell.Mul(1.0);  // -1.0
        pos.IsEquals(1.5).ShouldBeTrue();
    }

    [Fact(DisplayName = "TryWrite fails when buffer too small")]
    public void Binary_BufferTooSmall()
    {
        var s = Symbol.Parse("SOL-USDC@OKXSpot");
        var t = PublicTrade.Create(s, 7, 1, 100.123456, 0.001, PublicTrade.TradeAttributes.None);

        Span<byte> tiny = stackalloc byte[PublicTrade.BinarySize - 1];
        t.TryWrite(tiny, out var written).ShouldBeFalse();
        written.ShouldBe(0);
    }

    [Fact(DisplayName = "WithFlags and WithSymbol produce modified copies")]
    public void Withers()
    {
        var a = Symbol.Parse("BTCUSDT");
        var b = Symbol.Parse("BTC-USDT@OKXSpot");
        var t = PublicTrade.Create(a, 1, 2, 3.0, 4.0, PublicTrade.TradeAttributes.None);

        var t2 = t.WithFlags(PublicTrade.TradeAttributes.Liquidation | PublicTrade.TradeAttributes.AggressorSell);
        (t2.Attributes & PublicTrade.TradeAttributes.Liquidation).ShouldNotBe(PublicTrade.TradeAttributes.None);
        t2.Aggressor.ShouldBe(Side.Sell);

        var t3 = t2.WithSymbol(b);
        t3.Symbol.ShouldBe(b);
    }

    [Fact(DisplayName = "Equality and GetHashCode are consistent")]
    public void Equality_Hash()
    {
        var s = Symbol.Parse("BNB-USDT@OKXSpot");
        var t1 = PublicTrade.Create(s, 100, 200, 300.0, 0.5, PublicTrade.TradeAttributes.Maker);
        var t2 = PublicTrade.Create(s, 100, 200, 300.0, 0.5, PublicTrade.TradeAttributes.Maker);

        t1.ShouldBe(t2);
        t1.Equals(t2).ShouldBeTrue();
        t1.GetHashCode().ShouldBe(t2.GetHashCode());
    }
}
