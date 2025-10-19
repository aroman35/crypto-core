using System.Text.Json;
using CryptoCore.Extensions;
using CryptoCore.Json.Newtonsoft;
using CryptoCore.Json.SystemTextJson;
using CryptoCore.Root;
using Newtonsoft.Json;
using Shouldly;

namespace CryptoCore.Tests.Root;

public class ExchangeJsonConvertersTests
{
    [Fact(DisplayName = "STJ: Exchange serialize to preset when known")]
    public void Stj_Exchange_Serialize_Preset()
    {
        var x = Exchange.BinanceFutures; // Binance | Futures | Perpetual | UsdMargined
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

        var json = System.Text.Json.JsonSerializer.Serialize(x, opts);
        json.ShouldBe("\"BinanceFutures\"");
    }

    [Fact(DisplayName = "STJ: Exchange serialize to slug when unknown combo")]
    public void Stj_Exchange_Serialize_Slug()
    {
        var x = Exchange.OKX | Exchange.Futures | Exchange.Delivery | Exchange.UsdMargined; // not in presets
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

        var json = System.Text.Json.JsonSerializer.Serialize(x, opts);
        json.ShouldBe("\"okx-futures-delivery-usdm\"");
    }

    [Fact(DisplayName = "STJ: Exchange deserialize from preset")]
    public void Stj_Exchange_Deserialize_Preset()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var x = System.Text.Json.JsonSerializer.Deserialize<Exchange>("\"OKXSwap\"", opts);

        x.IsOKX().ShouldBeTrue();
        x.IsSwap().ShouldBeTrue();
        x.IsPerpetual().ShouldBeTrue();
        x.IsUsdMargined().ShouldBeTrue();
    }

    [Fact(DisplayName = "STJ: Exchange deserialize from slug")]
    public void Stj_Exchange_Deserialize_Slug()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var x = System.Text.Json.JsonSerializer.Deserialize<Exchange>("\"binance-futures\"", opts);

        x.IsBinance().ShouldBeTrue();
        x.IsFutures().ShouldBeTrue();
        x.IsPerpetual().ShouldBeTrue(); // defaulted
    }

    [Fact(DisplayName = "STJ: Exchange null → default(Exchange)")]
    public void Stj_Exchange_Null()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var x = System.Text.Json.JsonSerializer.Deserialize<Exchange>("null", opts);
        x.ShouldBe(Exchange.None);
    }

    [Fact(DisplayName = "STJ: Exchange invalid throws")]
    public void Stj_Exchange_Invalid()
    {
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        Should.Throw<System.Text.Json.JsonException>(() =>
        {
            _ = System.Text.Json.JsonSerializer.Deserialize<Exchange>("123", opts);
        });
        Should.Throw<System.Text.Json.JsonException>(() =>
        {
            _ = System.Text.Json.JsonSerializer.Deserialize<Exchange>("\"unknown\"", opts);
        });
    }

    [Fact(DisplayName = "Newtonsoft: Exchange roundtrip preset")]
    public void Newtonsoft_Exchange_Roundtrip_Preset()
    {
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
        var x = Exchange.OKXFutures;
        var json = JsonConvert.SerializeObject(x, settings);
        json.ShouldBe("\"OKXFutures\"");

        var back = JsonConvert.DeserializeObject<Exchange>(json, settings);
        back.ShouldBe(x);
    }

    [Fact(DisplayName = "Newtonsoft: Exchange from slug")]
    public void Newtonsoft_Exchange_From_Slug()
    {
        var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
        var back = JsonConvert.DeserializeObject<Exchange>("\"okx-swap-usdm\"", settings);
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
}
