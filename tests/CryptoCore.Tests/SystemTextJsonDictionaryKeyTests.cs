using System.Text.Json;
using CryptoCore.Primitives;
using CryptoCore.Serialization.SystemTextJson;
using Shouldly;

namespace CryptoCore.Tests;

public sealed class SystemTextJsonDictionaryKeyTests
{
    [Fact(DisplayName = "STJ: Dictionary<Symbol, int> roundtrip")]
    public void STJ_Symbol_Key_Roundtrip()
    {
        var dict = new Dictionary<Symbol, int>
        {
            [Symbol.Parse("ETHUSDT").For(Exchange.Binance | Exchange.Spot)] = 1,
            [Symbol.Parse("BTC-USDT@OKXSpot")] = 2,
        };

        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var json = JsonSerializer.Serialize(dict, opts);

        json.ShouldContain("ETHUSDT");
        json.ShouldContain("BTC-USDT");

        var back = JsonSerializer.Deserialize<Dictionary<Symbol, int>>(json, opts)!;
        back.Count.ShouldBe(2);
        back[Symbol.Parse("ETHUSDT").For(Exchange.Binance | Exchange.Spot)].ShouldBe(1);
        back[Symbol.Parse("BTC-USDT@OKXSpot")].ShouldBe(2);
    }

    [Fact(DisplayName = "STJ: Dictionary<Asset, int> roundtrip")]
    public void STJ_Asset_Key_Roundtrip()
    {
        var dict = new Dictionary<Asset, int>
        {
            [Asset.Parse("USDT")] = 1,
            [Asset.Parse("BTC")] = 2,
        };
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var json = JsonSerializer.Serialize(dict, opts);
        json.ShouldContain("\"USDT\"");
        json.ShouldContain("\"BTC\"");

        var back = JsonSerializer.Deserialize<Dictionary<Asset, int>>(json, opts)!;
        back[Asset.Parse("USDT")].ShouldBe(1);
        back[Asset.Parse("BTC")].ShouldBe(2);
    }

    [Fact(DisplayName = "STJ: Dictionary<Exchange, int> roundtrip")]
    public void STJ_Exchange_Key_Roundtrip()
    {
        var dict = new Dictionary<Exchange, int>
        {
            [Exchange.OKXSwap] = 7,
            [Exchange.Binance | Exchange.Spot] = 9,
        };
        var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
        var json = JsonSerializer.Serialize(dict, opts);

        json.ShouldContain("\"okx-swap\"");
        json.ShouldContain("\"binance\"");

        var back = JsonSerializer.Deserialize<Dictionary<Exchange, int>>(json, opts)!;
        back[Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined].ShouldBe(7);
        back[Exchange.Binance | Exchange.Spot].ShouldBe(9);
    }
}
