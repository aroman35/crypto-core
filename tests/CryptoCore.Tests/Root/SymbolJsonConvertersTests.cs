using System.Text.Json;
using CryptoCore.Extensions;
using CryptoCore.Serialization.Newtonsoft;
using CryptoCore.Serialization.SystemTextJson;
using CryptoCore.Primitives;
using Newtonsoft.Json;
using Shouldly;

namespace CryptoCore.Tests.Root;

public sealed class SymbolJsonConvertersTests
{
    private sealed class Wrapper
    {
        public Symbol Sym { get; set; }
        public Symbol? NullableSym { get; set; }
    }

    [Fact(DisplayName = "System.Text.Json: serialize Symbol as string (native format)")]
    public void Stj_Serialize_Symbol_As_String()
    {
        var s = Symbol.Create(Asset.BTC, Asset.USDT, Exchange.BinanceFutures); // Binance: BTCUSDT
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

        var json = System.Text.Json.JsonSerializer.Serialize(s, opts);
        json.ShouldBe("\"BTCUSDT\"");
    }

    [Fact(DisplayName = "System.Text.Json: deserialize Symbol from Binance style")]
    public void Stj_Deserialize_Binance()
    {
        var json = "\"ETHUSDC\"";
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

        var sym = System.Text.Json.JsonSerializer.Deserialize<Symbol>(json, opts);
        sym.ShouldNotBe(default);
        sym.BaseAsset.ToString().ShouldBe("ETH");
        sym.QuoteAsset.ToString().ShouldBe("USDC");
    }

    [Fact(DisplayName = "System.Text.Json: deserialize Symbol from OKX swap")]
    public void Stj_Deserialize_OKX_Swap()
    {
        var json = "\"BTC-USDT-SWAP\"";
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

        var sym = System.Text.Json.JsonSerializer.Deserialize<Symbol>(json, opts);
        sym.ShouldNotBe(default);
        sym.Exchange.IsOKX().ShouldBeTrue();
        sym.Exchange.IsSwap().ShouldBeTrue();
        sym.Exchange.IsPerpetual().ShouldBeTrue();
        sym.ToString().ShouldBe("BTC-USDT-SWAP");
    }

    [Fact(DisplayName = "System.Text.Json: wrapper round-trip with nullable and non-nullable")]
    public void Stj_Wrapper_Roundtrip()
    {
        var w = new Wrapper
        {
            Sym = Symbol.Create(Asset.BNB, Asset.USDT, Exchange.OKX | Exchange.Spot),
            NullableSym = Symbol.Create(Asset.ETH, Asset.USDT, Exchange.Binance | Exchange.Spot)
        };

        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

        var json = System.Text.Json.JsonSerializer.Serialize(w, opts);
        // OKX spot prints with dash; Binance spot prints concatenated form
        json.ShouldContain("\"Sym\":\"BNB-USDT\"");
        json.ShouldContain("\"NullableSym\":\"ETHUSDT\"");

        var back = System.Text.Json.JsonSerializer.Deserialize<Wrapper>(json, opts);
        back.ShouldNotBeNull();
        back.Sym.ToString().ShouldBe("BNB-USDT");
        back.NullableSym!.Value.ToString().ShouldBe("ETHUSDT");
    }

    [Fact(DisplayName = "System.Text.Json: null → default(Symbol)")]
    public void Stj_Null_To_Default()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var json = "null";
        var sym = System.Text.Json.JsonSerializer.Deserialize<Symbol>(json, opts);
        sym.ShouldBe(default);
    }

    [Fact(DisplayName = "System.Text.Json: invalid token throws")]
    public void Stj_Invalid_Throws()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var json = "123";
        Should.Throw<System.Text.Json.JsonException>(() =>
        {
            _ = System.Text.Json.JsonSerializer.Deserialize<Symbol>(json, opts);
        });
    }

    [Fact(DisplayName = "System.Text.Json: invalid string throws")]
    public void Stj_Invalid_String_Throws()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var json = "\"@not-a-symbol\"";
        Should.Throw<System.Text.Json.JsonException>(() =>
        {
            _ = System.Text.Json.JsonSerializer.Deserialize<Symbol>(json, opts);
        });
    }

    [Fact(DisplayName = "Newtonsoft: serialize Symbol as string (native format)")]
    public void Newtonsoft_Serialize_Symbol_As_String()
    {
        var s = Symbol.Create(Asset.BTC, Asset.USDT, Exchange.OKXSwap);
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();

        var json = JsonConvert.SerializeObject(s, settings);
        json.ShouldBe("\"BTC-USDT-SWAP\"");
    }

    [Fact(DisplayName = "Newtonsoft: deserialize Symbol from generic 'BASE-QUOTE@Preset'")]
    public void Newtonsoft_Deserialize_Generic_With_Preset()
    {
        var json = "\"SOL-USDC@BinanceSpot\"";
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();

        var sym = JsonConvert.DeserializeObject<Symbol>(json, settings);
        sym.ShouldNotBe(default);
        sym.BaseAsset.ToString().ShouldBe("SOL");
        sym.QuoteAsset.ToString().ShouldBe("USDC");
        // Binance spot prints without a dash
        sym.ToString().ShouldBe("SOLUSDC");
    }

    [Fact(DisplayName = "Newtonsoft: wrapper round-trip with nulls")]
    public void Newtonsoft_Wrapper_Roundtrip_With_Nulls()
    {
        var w = new Wrapper
        {
            Sym = Symbol.Create(Asset.ETH, Asset.USDT, Exchange.OKXSpot),
            NullableSym = null
        };

        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();

        var json = JsonConvert.SerializeObject(w, settings);
        json.ShouldContain("\"Sym\":\"ETH-USDT\"");
        json.ShouldContain("\"NullableSym\":null");

        var back = JsonConvert.DeserializeObject<Wrapper>(json, settings);
        back.ShouldNotBeNull();
        back.Sym.ToString().ShouldBe("ETH-USDT");
        back.NullableSym.ShouldBeNull();
    }

    [Fact(DisplayName = "Newtonsoft: invalid content throws")]
    public void Newtonsoft_Invalid_Throws()
    {
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
        Should.Throw<JsonSerializationException>(() =>
        {
            _ = JsonConvert.DeserializeObject<Symbol>("123", settings);
        });

        Should.Throw<JsonSerializationException>(() =>
        {
            _ = JsonConvert.DeserializeObject<Symbol>("\"***\"", settings);
        });
    }
}
