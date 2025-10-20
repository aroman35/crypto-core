using System.Text.Json;
using CryptoCore.Extensions;
using CryptoCore.Serialization.Newtonsoft;
using CryptoCore.Serialization.SystemTextJson;
using CryptoCore.Primitives;
using Newtonsoft.Json;
using Shouldly;
using JsonException = System.Text.Json.JsonException;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace CryptoCore.Tests.Root;

public class ExchangeJsonConvertersTests
{
    [Fact(DisplayName = "STJ: Exchange serialize to preset when known")]
    public void Stj_Exchange_Serialize_Preset()
    {
        var x = Exchange.BinanceFutures; // Binance | Futures | Perpetual | UsdMargined
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

        var json = JsonSerializer.Serialize(x, opts);
        json.ShouldBe("\"binance-futures\"");
    }

    [Fact(DisplayName = "STJ: Exchange serialize to slug when unknown combo")]
    public void Stj_Exchange_Serialize_Slug()
    {
        var x = Exchange.OKX | Exchange.Futures | Exchange.Delivery | Exchange.UsdMargined; // not in presets
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        Should.Throw<JsonException>(() => JsonSerializer.Serialize(x, opts));
    }

    [Fact(DisplayName = "STJ: Exchange deserialize from preset")]
    public void Stj_Exchange_Deserialize_Preset()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var x = JsonSerializer.Deserialize<Exchange>("\"OKXSwap\"", opts);

        x.IsOKX().ShouldBeTrue();
        x.IsSwap().ShouldBeTrue();
        x.IsPerpetual().ShouldBeTrue();
        x.IsUsdMargined().ShouldBeTrue();
    }

    [Fact(DisplayName = "STJ: Exchange deserialize from slug")]
    public void Stj_Exchange_Deserialize_Slug()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var x = JsonSerializer.Deserialize<Exchange>("\"binance-futures\"", opts);

        x.IsBinance().ShouldBeTrue();
        x.IsFutures().ShouldBeTrue();
        x.IsPerpetual().ShouldBeTrue(); // defaulted
    }

    [Fact(DisplayName = "Newtonsoft: Exchange roundtrip preset")]
    public void Newtonsoft_Exchange_Roundtrip_Preset()
    {
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
        var x = Exchange.OKXFutures;
        var json = JsonConvert.SerializeObject(x, settings);
        json.ShouldBe("\"okx-futures\"");

        var back = JsonConvert.DeserializeObject<Exchange>(json, settings);
        back.ShouldBe(x);
    }

    [Fact(DisplayName = "Newtonsoft: Exchange from slug")]
    public void Newtonsoft_Exchange_From_Slug()
    {
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
        var back = JsonConvert.DeserializeObject<Exchange>("\"okx-swap\"", settings);
        back.IsOKX().ShouldBeTrue();
        back.IsSwap().ShouldBeTrue();
        back.IsUsdMargined().ShouldBeTrue();
        back.IsPerpetual().ShouldBeTrue(); // by rule
    }

    [Fact(DisplayName = "Newtonsoft: Exchange invalid throws")]
    public void Newtonsoft_Exchange_Invalid()
    {
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
        Should.Throw<JsonSerializationException>(() =>
        {
            _ = JsonConvert.DeserializeObject<Exchange>("123", settings);
        });
        Should.Throw<JsonSerializationException>(() =>
        {
            _ = JsonConvert.DeserializeObject<Exchange>("\"nope-nope\"", settings);
        });
    }

    [Fact(DisplayName = "System.Text.Json: Exchange to/from slug")]
    public void STJ_Exchange_Slug_Roundtrip()
    {
        var x = Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined;
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var json = JsonSerializer.Serialize(x, opts);
        json.ShouldBe("\"okx-swap\"");

        var back = JsonSerializer.Deserialize<Exchange>(json, opts);
        back.IsOKX().ShouldBeTrue();
        back.IsSwap().ShouldBeTrue();
        back.IsUsdMargined().ShouldBeTrue();
        back.IsPerpetual().ShouldBeTrue();
    }

    [Fact(DisplayName = "Newtonsoft: Exchange to/from slug")]
    public void Newtonsoft_Exchange_Slug_Roundtrip()
    {
        var x = Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
        var json = JsonConvert.SerializeObject(x, settings);
        json.ShouldBe("\"binance-futures\"");

        var back = JsonConvert.DeserializeObject<Exchange>(json, settings);
        back.IsBinance().ShouldBeTrue();
        back.IsFutures().ShouldBeTrue();
        back.IsPerpetual().ShouldBeTrue();
        back.IsUsdMargined().ShouldBeTrue();
    }
}
