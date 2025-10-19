using System.Text.Json;
using CryptoCore.Extensions;
using CryptoCore.Json.Newtonsoft;
using CryptoCore.Json.SystemTextJson;
using CryptoCore.Root;
using Newtonsoft.Json;
using Shouldly;

namespace CryptoCore.Tests.Root;

public class AssetJsonConvertersTests
    {
        [Fact(DisplayName = "STJ: Asset serialize to string")]
        public void Stj_Asset_Serialize()
        {
            var a = Asset.Parse("eth");
            var opts = new JsonSerializerOptions().AddCryptoCoreConverters();

            var json = System.Text.Json.JsonSerializer.Serialize(a, opts);
            json.ShouldBe("\"ETH\"");
        }

        [Fact(DisplayName = "STJ: Asset deserialize from string")]
        public void Stj_Asset_Deserialize()
        {
            var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
            var a = System.Text.Json.JsonSerializer.Deserialize<Asset>("\"usdt\"", opts);
            a.ToString().ShouldBe("USDT");
        }

        [Fact(DisplayName = "STJ: Asset null → default")]
        public void Stj_Asset_Null()
        {
            var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
            var a = System.Text.Json.JsonSerializer.Deserialize<Asset>("null", opts);
            a.ShouldBe(default);
        }

        [Fact(DisplayName = "STJ: Asset invalid throws")]
        public void Stj_Asset_Invalid()
        {
            var opts = new JsonSerializerOptions().AddCryptoCoreConverters();
            Should.Throw<System.Text.Json.JsonException>(() =>
            {
                _ = System.Text.Json.JsonSerializer.Deserialize<Asset>("123", opts);
            });
        }

        [Fact(DisplayName = "Newtonsoft: Asset serialize/deserialize")]
        public void Newtonsoft_Asset_Roundtrip()
        {
            var a = Asset.Parse("Btc");
            var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
            var json = JsonConvert.SerializeObject(a, settings);
            json.ShouldBe("\"BTC\"");

            var back = JsonConvert.DeserializeObject<Asset>(json, settings);
            back.ToString().ShouldBe("BTC");
        }

        [Fact(DisplayName = "Newtonsoft: Asset invalid throws")]
        public void Newtonsoft_Asset_Invalid()
        {
            var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
            Should.Throw<JsonSerializationException>(() =>
            {
                _ = JsonConvert.DeserializeObject<Asset>("123", settings);
            });
        }
    }
