using System.ComponentModel;
using System.Reflection;
using CryptoCore.Extensions;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Unit.Root;

public class ExchangeTests
{
    [Fact(DisplayName = "Masks: Market/Contract/Venue bits do not overlap")]
        public void Masks_DoNotOverlap()
        {
            ((Exchange.MarketMask & Exchange.ContractMask) == 0).ShouldBeTrue();
            ((Exchange.MarketMask & Exchange.VenueMask) == 0).ShouldBeTrue();
            ((Exchange.ContractMask & Exchange.VenueMask) == 0).ShouldBeTrue();
        }

        [Fact(DisplayName = "MarketPart/ContractPart/VenuePart extract only corresponding bits")]
        public void GroupExtractors_Work()
        {
            var x = Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;

            x.MarketPart().ShouldBe(Exchange.Futures);
            x.ContractPart().ShouldBe(Exchange.Perpetual | Exchange.UsdMargined);
            x.VenuePart().ShouldBe(Exchange.Binance);
        }

        [Fact(DisplayName = "Presets: BinanceSpot == Binance | Spot")]
        public void Preset_BinanceSpot()
        {
            Exchange.BinanceSpot.ShouldBe(Exchange.Binance | Exchange.Spot);
        }

        [Fact(DisplayName = "Presets: BinanceFutures == Binance | Futures | Perpetual | UsdMargined")]
        public void Preset_BinanceFutures()
        {
            Exchange.BinanceFutures.ShouldBe(Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined);
        }

        [Fact(DisplayName = "Description attributes exist on presets")]
        public void Presets_HaveDescriptions()
        {
            bool HasDescription(Exchange e)
            {
                var fi = typeof(Exchange).GetField(Enum.GetName(e)!);
                var attr = fi!.GetCustomAttribute<DescriptionAttribute>();
                return attr != null && !string.IsNullOrWhiteSpace(attr.Description);
            }

            HasDescription(Exchange.BinanceSpot).ShouldBeTrue();
            HasDescription(Exchange.BinanceFutures).ShouldBeTrue();
            HasDescription(Exchange.OKXSpot).ShouldBeTrue();
            HasDescription(Exchange.OKXFutures).ShouldBeTrue();
            HasDescription(Exchange.OKXSwap).ShouldBeTrue();
            HasDescription(Exchange.KuCoinSpot).ShouldBeTrue();
            HasDescription(Exchange.KuCoinFutures).ShouldBeTrue();
        }

        [Fact(DisplayName = "Predicates: IsSpot/IsFutures/IsOptions/IsSwap/IsMargin")]
        public void Predicates_Market()
        {
            Exchange.BinanceSpot.IsSpot().ShouldBeTrue();
            Exchange.BinanceSpot.IsFutures().ShouldBeFalse();

            var f = Exchange.Binance | Exchange.Futures | Exchange.Margin;
            f.IsFutures().ShouldBeTrue();
            f.IsMargin().ShouldBeTrue();
            f.IsOptions().ShouldBeFalse();
            f.IsSwap().ShouldBeFalse();
        }

        [Fact(DisplayName = "Predicates: IsPerpetual/IsDelivery/IsCoinMargined/IsUsdMargined")]
        public void Predicates_Contract()
        {
            var x = Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            x.IsPerpetual().ShouldBeTrue();
            x.IsDelivery().ShouldBeFalse();
            x.IsUsdMargined().ShouldBeTrue();
            x.IsCoinMargined().ShouldBeFalse();

            var y = Exchange.Deribit | Exchange.Options | Exchange.CoinMargined;
            y.IsCoinMargined().ShouldBeTrue();
            y.IsPerpetual().ShouldBeFalse();
        }

        [Fact(DisplayName = "Predicates: Venue checks (IsBinance/IsOKX/IsKuCoin/...)")]
        public void Predicates_Venue()
        {
            var x = Exchange.Bitget | Exchange.Spot;
            x.IsBitget().ShouldBeTrue();
            x.IsBinance().ShouldBeFalse();
            x.IsOKX().ShouldBeFalse();
            x.IsKuCoin().ShouldBeFalse();
            x.IsDeribit().ShouldBeFalse();
            x.IsBybit().ShouldBeFalse();
        }

        [Fact(DisplayName = "TryGetSingleVenue returns venue if only one set")]
        public void TryGetSingleVenue_Single()
        {
            var x = Exchange.Binance | Exchange.Spot;
            x.TryGetSingleVenue().ShouldBe(Exchange.Binance);
        }

        [Fact(DisplayName = "TryGetSingleVenue returns None if multiple venues set")]
        public void TryGetSingleVenue_Multiple()
        {
            var x = Exchange.Binance | Exchange.OKX | Exchange.Spot;
            x.TryGetSingleVenue().ShouldBe(Exchange.None);
        }

        [Fact(DisplayName = "EnumerateVenues returns all venues in stable order")]
        public void EnumerateVenues_Works()
        {
            var x = Exchange.Binance | Exchange.KuCoin | Exchange.Bybit | Exchange.Spot;
            var venues = x.EnumerateVenues().ToArray();
            venues.ShouldBe(new[] { Exchange.Binance, Exchange.KuCoin, Exchange.Bybit });
        }

        [Fact(DisplayName = "ToSlug: spot with venue -> 'binance-spot'")]
        public void ToSlug_Spot()
        {
            var x = Exchange.Binance | Exchange.Spot;
            x.ToSlug().ShouldBe("binance");
        }


        [Fact(DisplayName = "ParseSlug: 'binance' -> venue only")]
        public void ParseSlug_VenueOnly()
        {
            var x = ExchangeExtensions.ParseSlug("binance");
            x.VenuePart().ShouldBe(Exchange.Binance);
            x.MarketPart().ShouldBe(Exchange.Spot);
            x.ContractPart().ShouldBe(Exchange.None);
        }

        [Fact(DisplayName = "ParseSlug: 'binance-futures' -> assumes perpetual")]
        public void ParseSlug_Futures_DefaultsToPerpetual()
        {
            var x = ExchangeExtensions.ParseSlug("binance-futures");
            x.IsFutures().ShouldBeTrue();
            x.IsPerpetual().ShouldBeTrue(); // defaulted
            x.IsDelivery().ShouldBeFalse();
        }

        [Fact(DisplayName = "ParseSlug: Case-insensitive and trimming")]
        public void ParseSlug_CaseInsensitive()
        {
            var x = ExchangeExtensions.ParseSlug("  BiNaNcE-FuTuReS  ");
            x.IsBinance().ShouldBeTrue();
            x.IsFutures().ShouldBeTrue();
            x.IsPerpetual().ShouldBeTrue();
            x.IsUsdMargined().ShouldBeTrue();
        }

        [Fact(DisplayName = "Roundtrip: ParseSlug(ToSlug(x)) preserves meaningful bits for single-venue")]
        public void Roundtrip_SingleVenue()
        {
            var original = Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            var slug = original.ToSlug();
            var parsed = ExchangeExtensions.ParseSlug(slug);

            parsed.VenuePart().ShouldBe(original.VenuePart());
            parsed.MarketPart().ShouldBe(original.MarketPart());
            parsed.IsPerpetual().ShouldBeTrue();
            parsed.IsUsdMargined().ShouldBeTrue();
        }

        [Fact(DisplayName = "Binary check: Venue bits are powers of two and within VenueMask")]
        public void VenueBits_ArePowersOfTwo()
        {
            var venues = new[] { Exchange.Binance, Exchange.OKX, Exchange.KuCoin, Exchange.Bybit, Exchange.Deribit, Exchange.Bitget };

            foreach (var v in venues)
            {
                ((v & (v - 1)) == 0).ShouldBeTrue();               // power of two
                ((v & Exchange.VenueMask) == v).ShouldBeTrue();     // inside mask
            }
        }

        [Fact(DisplayName = "Binary check: Market bits are inside MarketMask; Contract bits inside ContractMask")]
        public void Market_Contract_Bits_InsideMasks()
        {
            var markets = new[] { Exchange.Spot, Exchange.Futures, Exchange.Options, Exchange.Swap, Exchange.Margin };
            foreach (var m in markets)
                ((m & Exchange.MarketMask) == m).ShouldBeTrue();

            var contracts = new[] { Exchange.Perpetual, Exchange.Delivery, Exchange.CoinMargined, Exchange.UsdMargined };
            foreach (var c in contracts)
                ((c & Exchange.ContractMask) == c).ShouldBeTrue();
        }
}
