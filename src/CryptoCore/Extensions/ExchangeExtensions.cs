using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.Marshalling;
using CryptoCore.Primitives;

namespace CryptoCore.Extensions;

/// <summary>
/// Extension helpers for working with <see cref="Exchange"/> bit flags:
/// fast group checks (market/contract/venue), convenience predicates
/// (e.g., <c>IsSpot</c>, <c>IsPerpetual</c>, <c>IsBinance</c>),
/// group extraction, and slug (de)serialization.
/// </summary>
public static class ExchangeExtensions
{
    /// <summary>Short slug for Binance Spot.</summary>
    public const string BINANCE_SPOT_SLUG = "binance";

    /// <summary>Short slug for Binance USD-margined Perpetual Futures.</summary>
    public const string BINANCE_FUTURES_SLUG = "binance-futures";

    /// <summary>Short slug for OKX Spot.</summary>
    public const string OKX_SPOT_SLUG = "okx";

    /// <summary>Short slug for OKX USD-margined Perpetual Futures.</summary>
    public const string OKX_FUTURES_SLUG = "okx-futures";

    /// <summary>Short slug for OKX USD-margined Perpetual Swaps.</summary>
    public const string OKX_SWAP_SLUG = "okx-swap";

    /// <summary>Short slug for KuCoin Spot.</summary>
    public const string KUCOIN_SPOT_SLUG = "kucoin";

    /// <summary>Short slug for KuCoin USD-margined Perpetual Futures.</summary>
    public const string KUCOIN_FUTURES_SLUG = "kucoin-futures";

    /// <summary>
    /// Checks if <paramref name="x"/> includes the specified market-type flag.
    /// Only flags within <see cref="Exchange.MarketMask"/> are considered valid input.
    /// </summary>
    /// <param name="x">The composite <see cref="Exchange"/> value.</param>
    /// <param name="marketFlag">A single market flag (e.g., <see cref="Exchange.Spot"/>).</param>
    /// <returns>True if the flag is set; otherwise false.</returns>
    public static bool HasMarket(this Exchange x, Exchange marketFlag)
        => (marketFlag & Exchange.MarketMask) != 0 && (x & marketFlag) != 0;

    /// <summary>
    /// Checks if <paramref name="x"/> includes the specified contract-attribute flag.
    /// Only flags within <see cref="Exchange.ContractMask"/> are considered valid input.
    /// </summary>
    /// <param name="x">The composite <see cref="Exchange"/> value.</param>
    /// <param name="contractFlag">A single contract flag (e.g., <see cref="Exchange.Perpetual"/>).</param>
    /// <returns>True if the flag is set; otherwise false.</returns>
    public static bool HasContract(this Exchange x, Exchange contractFlag)
        => (contractFlag & Exchange.ContractMask) != 0 && (x & contractFlag) != 0;

    /// <summary>
    /// Checks if <paramref name="x"/> includes the specified venue flag.
    /// Only flags within <see cref="Exchange.VenueMask"/> are considered valid input.
    /// </summary>
    /// <param name="x">The composite <see cref="Exchange"/> value.</param>
    /// <param name="venueFlag">A single venue flag (e.g., <see cref="Exchange.Binance"/>).</param>
    /// <returns>True if the flag is set; otherwise false.</returns>
    public static bool HasVenue(this Exchange x, Exchange venueFlag)
        => (venueFlag & Exchange.VenueMask) != 0 && (x & venueFlag) != 0;

    /// <summary>
    /// Returns true if the market type includes Spot.
    /// </summary>
    public static bool IsSpot(this Exchange x) => x.HasMarket(Exchange.Spot);

    /// <summary>
    /// Returns true if the market type includes Futures.
    /// </summary>
    public static bool IsFutures(this Exchange x) => x.HasMarket(Exchange.Futures);

    /// <summary>
    /// Returns true if the market type includes Options.
    /// </summary>
    public static bool IsOptions(this Exchange x) => x.HasMarket(Exchange.Options);

    /// <summary>
    /// Returns true if the market type includes Swap.
    /// </summary>
    public static bool IsSwap(this Exchange x) => x.HasMarket(Exchange.Swap);

    /// <summary>
    /// Returns true if the market type includes Margin capability.
    /// </summary>
    public static bool IsMargin(this Exchange x) => x.HasMarket(Exchange.Margin);

    /// <summary>
    /// Returns true if the contract attributes include Perpetual.
    /// </summary>
    public static bool IsPerpetual(this Exchange x) => x.HasContract(Exchange.Perpetual);

    /// <summary>
    /// Returns true if the contract attributes include Delivery (dated futures).
    /// </summary>
    public static bool IsDelivery(this Exchange x) => x.HasContract(Exchange.Delivery);

    /// <summary>
    /// Returns true if the contract is coin-margined.
    /// </summary>
    public static bool IsCoinMargined(this Exchange x) => x.HasContract(Exchange.CoinMargined);

    /// <summary>
    /// Returns true if the contract is USD-margined.
    /// </summary>
    public static bool IsUsdMargined(this Exchange x) => x.HasContract(Exchange.UsdMargined);

    /// <summary>
    /// Returns true if the venue includes Binance.
    /// </summary>
    public static bool IsBinance(this Exchange x) => x.HasVenue(Exchange.Binance);

    /// <summary>
    /// Returns true if the venue includes OKX.
    /// </summary>
    public static bool IsOKX(this Exchange x) => x.HasVenue(Exchange.OKX);

    /// <summary>
    /// Returns true if the venue includes KuCoin.
    /// </summary>
    public static bool IsKuCoin(this Exchange x) => x.HasVenue(Exchange.KuCoin);

    /// <summary>
    /// Returns true if the venue includes Bybit.
    /// </summary>
    public static bool IsBybit(this Exchange x) => x.HasVenue(Exchange.Bybit);

    /// <summary>
    /// Returns true if the venue includes Deribit.
    /// </summary>
    public static bool IsDeribit(this Exchange x) => x.HasVenue(Exchange.Deribit);

    /// <summary>
    /// Returns true if the venue includes Bitget.
    /// </summary>
    public static bool IsBitget(this Exchange x) => x.HasVenue(Exchange.Bitget);

    /// <summary>
    /// Extracts only the market-type bits from the composite value.
    /// </summary>
    public static Exchange MarketPart(this Exchange x) => x & Exchange.MarketMask;

    /// <summary>
    /// Extracts only the contract-attribute bits from the composite value.
    /// </summary>
    public static Exchange ContractPart(this Exchange x) => x & Exchange.ContractMask;

    /// <summary>
    /// Extracts only the venue bits from the composite value.
    /// </summary>
    public static Exchange VenuePart(this Exchange x) => x & Exchange.VenueMask;

    /// <summary>
    /// Returns the venue if exactly one venue bit is set; otherwise <see cref="Exchange.None"/>.
    /// Useful when a single concrete venue is expected.
    /// </summary>
    /// <param name="x">The composite <see cref="Exchange"/> value.</param>
    /// <returns>A single-venue flag or <see cref="Exchange.None"/>.</returns>
    public static Exchange TryGetSingleVenue(this Exchange x)
    {
        var v = x.VenuePart();
        return v != 0 && (v & (v - 1)) == 0 ? v : Exchange.None;
    }

    /// <summary>
    /// Enumerates all venue flags set within the composite value.
    /// </summary>
    /// <param name="x">The composite <see cref="Exchange"/> value.</param>
    /// <returns>Sequence of individual venue flags.</returns>
    public static IEnumerable<Exchange> EnumerateVenues(this Exchange x)
    {
        var v = x.VenuePart();
        foreach (var bit in VenueBits)
            if ((v & bit) != 0)
                yield return bit;
    }

    /// <summary>
    /// Returns the short slug (from <see cref="DescriptionAttribute"/>) for a known preset,
    /// or an empty string if <paramref name="x"/> is not one of the presets.
    /// </summary>
    /// <param name="x">Exchange flags.</param>
    /// <returns>Short slug like "okx-swap", or empty string for non-preset combinations.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string ToSlug(this Exchange x)
    {
        if (x == (Exchange.Binance | Exchange.Spot))
        {
            return BINANCE_SPOT_SLUG;
        }

        if (x == (Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            return BINANCE_FUTURES_SLUG;
        }

        if (x == (Exchange.OKX | Exchange.Spot))
        {
            return OKX_SPOT_SLUG;
        }

        if (x == (Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            return OKX_FUTURES_SLUG;
        }

        if (x == (Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined))
        {
            return OKX_SWAP_SLUG;
        }

        if (x == (Exchange.KuCoin | Exchange.Spot))
        {
            return KUCOIN_SPOT_SLUG;
        }

        if (x == (Exchange.KuCoin | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            return KUCOIN_FUTURES_SLUG;
        }

        return string.Empty;
    }

    /// <summary>
    /// Parses a slug like "binance", "binance-futures", "okx-swap", case-insensitive.
    /// Returns <see cref="Exchange.None"/> on failure rather than throwing.
    /// If a futures market is specified without <c>perpetual</c> or <c>delivery</c>,
    /// <c>perpetual</c> is assumed by default.
    /// Recognized margin tokens: "usdm"/"usd-m"/"usd", "coinm"/"coin-m"/"coin".
    /// </summary>
    /// <param name="slug">Slug to parse.</param>
    /// <returns>Composite <see cref="Exchange"/> flags.</returns>
    public static Exchange ParseSlug(string slug)
    {
        if (string.IsNullOrWhiteSpace(slug))
            return Exchange.None;

        if (!TryParseSlug(slug, out var exchange))
            throw new InvalidEnumArgumentException($"Exchange slug is not configured for {slug}");
        return exchange;
    }

    /// <summary>
    /// Tries to parse a short slug (from <see cref="DescriptionAttribute"/>) into <see cref="Exchange"/>.
    /// Accepts only known preset slugs like "okx-swap".
    /// </summary>
    /// <param name="slug">Short slug (case-insensitive).</param>
    /// <param name="result">Parsed value.</param>
    /// <returns>True on success.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool TryParseSlug(ReadOnlySpan<char> slug, out Exchange result)
    {
        slug = slug.Trim();
        if (slug.Equals(BINANCE_SPOT_SLUG.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            result = Exchange.Binance | Exchange.Spot;
            return true;
        }

        if (slug.Equals(BINANCE_FUTURES_SLUG.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            result = Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (slug.Equals(OKX_SPOT_SLUG.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            result = Exchange.OKX | Exchange.Spot;
            return true;
        }

        if (slug.Equals(OKX_FUTURES_SLUG.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            result = Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (slug.Equals(OKX_SWAP_SLUG.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            result = Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (slug.Equals(KUCOIN_SPOT_SLUG.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            result = Exchange.KuCoin | Exchange.Spot;
            return true;
        }

        if (slug.Equals(KUCOIN_FUTURES_SLUG.AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            result = Exchange.KuCoin | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        result = Exchange.None;
        return false;
    }

    /// <summary>
    /// Tries to parse either a short slug (Description) or an enum name into <see cref="Exchange"/>.
    /// Enum names are matched case-insensitively (e.g., "OKXSwap", "BinanceFutures").
    /// </summary>
    /// <param name="text">Input text.</param>
    /// <param name="result">Parsed value.</param>
    /// <returns>True on success.</returns>
    public static bool TryParsePreset(ReadOnlySpan<char> text, out Exchange result)
    {
        // 1) Try slug first (no allocations).
        if (TryParseSlug(text, out result))
        {
            return true;
        }

        // 2) Try enum name (requires string for Enum.TryParse).
        if (text.Length == 0)
        {
            result = Exchange.None;
            return false;
        }

        var s = text.ToString();
        if (Enum.TryParse(s, ignoreCase: true, out Exchange x))
        {
            result = x;
            return true;
        }

        result = Exchange.None;
        return false;
    }

    // ---------- Internal helpers ----------

    private static readonly Exchange[] VenueBits =
    {
        Exchange.Binance, Exchange.OKX, Exchange.KuCoin,
        Exchange.Bybit, Exchange.Deribit, Exchange.Bitget
    };
}
