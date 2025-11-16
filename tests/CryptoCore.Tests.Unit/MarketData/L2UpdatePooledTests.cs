using System.Runtime.CompilerServices;
using CryptoConnector.Binance.Common;
using CryptoConnector.Binance.Parsers;
using CryptoCore.MarketData;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Unit.MarketData;

public class L2UpdatePooledTests
{
    [Fact(DisplayName = "Pooled TryRead returns rented deltas and can be disposed")]
    public void Pooled_Read_Dispose()
    {
        var s = Symbol.Parse("BTCUSDT").For(Exchange.BinanceSpot);
        var deltas = new[] { new L2Delta(Side.Buy, 1, 2), new L2Delta(Side.Sell, 3, 4) };
        var u = new L2Update(s, 10, false, 7, 8, 6, deltas);

        Span<byte> buf = stackalloc byte[Unsafe.SizeOf<Symbol>() + 8 + 1 + 8 + 8 + 8 + 4 + deltas.Length * L2Update.DeltaSize];
        u.TryWrite(buf, out var written).ShouldBeTrue();

        L2UpdatePooled.TryRead(buf[..written], out var pooled).ShouldBeTrue();
        using (pooled)
        {
            pooled.Symbol.ShouldBe(s);
            pooled.Deltas.Span.Length.ShouldBe(2);
        }
    }

    [Fact(DisplayName = "Binance parser (spot/futures) without allocations")]
    public void Binance_Parser()
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

        var provider = new SimpleSymbolProvider();
        BinanceDepthParser.TryParseDepthUpdate(json, provider, Exchange.BinanceFutures, out var pooled).ShouldBeTrue();

        using (pooled)
        {
            pooled.FirstUpdateId.ShouldBe(100UL);
            pooled.LastUpdateId.ShouldBe(102UL);
            pooled.PrevLastUpdateId.ShouldBe(99UL);
            pooled.Deltas.Span.Length.ShouldBe(3);
            pooled.Deltas.Span[1].IsRemove.ShouldBeTrue();
        }
    }
}
