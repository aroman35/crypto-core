using CryptoCore.Extensions;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Root;

public class SymbolTests
{
    [Fact(DisplayName = "Parse: delimiter-less Binance-style 'BTCUSDT' splits by stable suffix")]
    public void Parse_BinanceStyle_NoSeparator()
    {
        var s = Symbol.Parse("BTCUSDT");
        s.BaseAsset.ToString().ShouldBe("BTC");
        s.QuoteAsset.ToString().ShouldBe("USDT");
        s.Exchange.ShouldBe(Exchange.BinanceSpot);

        // Привязали к BinanceFutures → нативная форма слитно
        var bnf = s.For(Exchange.BinanceFutures);
        bnf.ToString().ShouldBe("BTCUSDT");
    }

    [Fact(DisplayName = "Stablecoin registry: add at runtime and parse 'BTCFDUSD'")]
    public void Stablecoin_Add_And_Parse()
    {
        Symbol.AddStablecoin("FDUSD");
        var s = Symbol.Parse("BTCFDUSD");
        s.BaseAsset.ToString().ShouldBe("BTC");
        s.QuoteAsset.ToString().ShouldBe("FDUSD");

        var spot = s.For(Exchange.BinanceSpot);
        spot.ToString().ShouldBe("BTCFDUSD");
    }

    [Fact(DisplayName = "Parse: OKX spot 'BTC-USDT' recognized and formats as 'BASE-QUOTE'")]
    public void Parse_Okx_Spot()
    {
        var s = Symbol.Parse("BTC-USDT");
        s.Exchange.ShouldBe(Exchange.OKX | Exchange.Spot);
        s.ToString().ShouldBe("BTC-USDT");
    }

    [Fact(DisplayName = "Parse: OKX perpetual 'BTC-USDT-SWAP' recognized and formats with '-SWAP'")]
    public void Parse_Okx_Swap()
    {
        var s = Symbol.Parse("BTC-USDT-SWAP");
        s.Exchange.IsOKX().ShouldBeTrue();
        (s.Exchange.IsPerpetual() || s.Exchange.IsSwap()).ShouldBeTrue();
        s.ToString().ShouldBe("BTC-USDT-SWAP");
    }

    [Fact(DisplayName = "Parse: OKX delivery 'BTC-USD-20241227' sets Delivery + UsdMargined")]
    public void Parse_Okx_Delivery()
    {
        var s = Symbol.Parse("BTC-USD-20241227");
        s.Exchange.IsOKX().ShouldBeTrue();
        s.Exchange.IsFutures().ShouldBeTrue();
        s.Exchange.IsDelivery().ShouldBeTrue();
        s.Exchange.IsUsdMargined().ShouldBeTrue();
        s.BaseAsset.ToString().ShouldBe("BTC");
        s.QuoteAsset.ToString().ShouldBe("USD"); // по контракту парсера
    }

    [Fact(DisplayName = "Parse: explicit preset 'ETH-USDT@BinanceFutures' → Binance native format")]
    public void Parse_ExplicitPreset()
    {
        var s = Symbol.Parse("ETH-USDT@BinanceFutures");
        s.Exchange.ShouldBe(Exchange.BinanceFutures);
        s.BaseAsset.ToString().ShouldBe("ETH");
        s.QuoteAsset.ToString().ShouldBe("USDT");
        s.ToString().ShouldBe("ETHUSDT"); // нативный бинансовский формат
    }

    [Fact(DisplayName = "Parse: generic 'BASE/QUOTE' accepted and defaults to no exchange")]
    public void Parse_Generic_Separator()
    {
        var s = Symbol.Parse("SOL/USDC");
        s.Exchange.ShouldBe(Exchange.None);
        s.ToString().ShouldBe("SOL-USDC"); // дефолтный вывод BASE-QUOTE
    }

    [Fact(DisplayName = "For(exchange): rebind preset and use exchange-native formatting")]
    public void For_Rebind_Formats_Natively()
    {
        var s = Symbol.Parse("BTC-USDT"); // распознается как OKX Spot
        var reb = s.For(Exchange.BinanceFutures);
        reb.ToString().ShouldBe("BTCUSDT");

        var reb2 = s.For(Exchange.OKXSwap);
        reb2.ToString().ShouldBe("BTC-USDT-SWAP");
    }

    [Fact(DisplayName = "TryFormat: writes native form into provided buffer without allocations")]
    public void TryFormat_Writes()
    {
        var s = Symbol.Parse("BTCUSDT").For(Exchange.OKXSwap);
        Span<char> buf = stackalloc char[32];
        s.TryFormat(buf, out var written).ShouldBeTrue();
        var str = new string(buf[..written]);
        str.ShouldBe("BTC-USDT-SWAP");
    }

    [Fact(DisplayName = "CompareTo: order by Base, then Quote, then preset name")]
    public void CompareTo_Order()
    {
        var a = Symbol.Parse("AAA-USDT@BinanceSpot");
        var b = Symbol.Parse("AAA-USDT@OKXSpot");
        var c = Symbol.Parse("AAA-USD@BinanceSpot");
        var d = Symbol.Parse("AAB-USDT@BinanceSpot");

        a.CompareTo(b).ShouldBeLessThan(0); // BinanceSpot < OKXSpot (лексикографически)
        c.CompareTo(a).ShouldBeLessThan(0); // USD < USDT
        d.CompareTo(a).ShouldBeGreaterThan(0); // AAB > AAA
    }

    [Fact(DisplayName = "ToString cache: repeated calls return same string instance")]
    public void ToString_Cache()
    {
        var s = Symbol.Parse("ETHUSDT").For(Exchange.BinanceSpot);
        var s1 = s.ToString();
        var s2 = s.ToString();
        ReferenceEquals(s1, s2).ShouldBeTrue();
    }

    [Fact(DisplayName = "Parse: invalid inputs are rejected")]
    public void Parse_Invalid()
    {
        Should.Throw<FormatException>(() => Symbol.Parse("")); // empty
        Should.Throw<FormatException>(() => Symbol.Parse("BTC-")); // missing quote
        Should.Throw<FormatException>(() => Symbol.Parse("BTCUSDX")); // unknown stable suffix, no sep
        Should.Throw<FormatException>(() => Symbol.Parse("BTC-USDT@Unknown")); // unknown preset
    }

    [Fact(DisplayName = "Symbol: Delimiter-less split by stable suffix; ToString generic")]
    public void Symbol_Split_Stable_Suffix_Generic_ToString()
    {
        var s = Symbol.Parse("BTCUSDT");
        s.BaseAsset.ToString().ShouldBe("BTC");
        s.QuoteAsset.ToString().ShouldBe("USDT");
        // Exchange not set -> generic
        s.ToString().ShouldBe("BTCUSDT");
    }

    [Fact(DisplayName = "Symbol: Binance native ToString when Exchange set")]
    public void Symbol_Binance_Native_ToString()
    {
        var s = Symbol.Parse("BTCUSDT").For(Exchange.Binance | Exchange.Spot);
        s.ToString().ShouldBe("BTCUSDT");
    }

    [Fact(DisplayName = "Symbol: OKX Spot and SWAP parsing/formatting")]
    public void Symbol_OKX_Spot_Swap()
    {
        var s1 = Symbol.Parse("ETH-USDT@OKXSpot");
        s1.Exchange.IsOKX().ShouldBeTrue();
        s1.Exchange.IsSpot().ShouldBeTrue();
        s1.ToString().ShouldBe("ETH-USDT");

        var s2 = Symbol.Parse("BTC-USDT-SWAP");
        s2.Exchange.IsOKX().ShouldBeTrue();
        s2.Exchange.IsSwap().ShouldBeTrue();
        s2.Exchange.IsPerpetual().ShouldBeTrue();
        s2.Exchange.IsUsdMargined().ShouldBeTrue();
        s2.ToString().ShouldBe("BTC-USDT-SWAP");
    }

    [Fact(DisplayName = "Symbol: AddStablecoin extends suffix registry")]
    public void Symbol_AddStablecoin_Runtime()
    {
        // Before adding custom suffix, parsing without delimiter must fail.
        Symbol.TryParse("FOOXYZ".AsSpan(), out _).ShouldBeFalse();

        // After registering runtime suffix — success.
        Symbol.AddStablecoin("XYZ");
        var s2 = Symbol.Parse("FOOXYZ");
        s2.BaseAsset.ToString().ShouldBe("FOO");
        s2.QuoteAsset.ToString().ShouldBe("XYZ");
    }

    [Fact(DisplayName = "Symbol: TryFormat success and failure")]
    public void Symbol_TryFormat()
    {
        var s = Symbol.Parse("SOLUSDC").For(Exchange.Binance | Exchange.Spot);
        Span<char> buf = stackalloc char[7];
        s.TryFormat(buf, out var w).ShouldBeTrue();
        new string(buf[..w]).ShouldBe("SOLUSDC");

        Span<char> small = stackalloc char[3];
        s.TryFormat(small, out _).ShouldBeFalse();
    }
}
