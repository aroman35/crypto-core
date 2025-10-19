using CryptoCore.Root;

namespace CryptoCore.Extensions;

/// <summary>
/// Extension helpers for working with <see cref="Exchange"/> bit flags:
/// fast group checks (market/contract/venue), convenience predicates
/// (e.g., <c>IsSpot</c>, <c>IsPerpetual</c>, <c>IsBinance</c>),
/// group extraction, and slug (de)serialization.
/// </summary>
public static class ExchangeExtensions
{
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
    /// Builds a human-friendly slug:
    /// e.g., "binance-futures-perpetual-usdm", "okx-spot", or multiple venues joined by commas.
    /// Missing venue yields "unknown" prefix.
    /// </summary>
    /// <param name="x">The composite <see cref="Exchange"/> value.</param>
    /// <returns>Slug string for logging/serialization.</returns>
    public static string ToSlug(this Exchange x)
    {
        var venues = x.EnumerateVenues().ToList();
        if (venues.Count == 0)
            return BuildSlug(Exchange.None, x);

        return string.Join(",",
            venues.Select(v => BuildSlug(v, x)));
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

        var tokens = slug.Trim().ToLowerInvariant().Split('-', StringSplitOptions.RemoveEmptyEntries);
        var venue = tokens.FirstOrDefault();
        var rest = tokens.Skip(1).ToHashSet();

        var x = venue switch
        {
            "binance" => Exchange.Binance,
            "okx" or "okex" => Exchange.OKX,
            "kucoin" => Exchange.KuCoin,
            "bybit" => Exchange.Bybit,
            "deribit" => Exchange.Deribit,
            "bitget" => Exchange.Bitget,
            _ => Exchange.None
        };

        if (x == Exchange.None)
            return Exchange.None;

        if (rest.Contains("spot"))
            x |= Exchange.Spot;
        if (rest.Contains("futures"))
            x |= Exchange.Futures;
        if (rest.Contains("options"))
            x |= Exchange.Options;
        if (rest.Contains("swap"))
            x |= Exchange.Swap;

        if (rest.Contains("perpetual") || rest.Contains("perp"))
            x |= Exchange.Perpetual;
        if (rest.Contains("delivery") || rest.Contains("quarterly"))
            x |= Exchange.Delivery;

        if (rest.Contains("coin-m") || rest.Contains("coinm") || rest.Contains("coin"))
            x |= Exchange.CoinMargined;
        if (rest.Contains("usdm") || rest.Contains("usd-m") || rest.Contains("usd"))
            x |= Exchange.UsdMargined;

        if (x.IsFutures() && !x.IsPerpetual() && !x.IsDelivery())
            x |= Exchange.Perpetual;

        return x;
    }
    // ---------- Internal helpers ----------

    private static readonly Exchange[] VenueBits =
    {
        Exchange.Binance, Exchange.OKX, Exchange.KuCoin,
        Exchange.Bybit, Exchange.Deribit, Exchange.Bitget
    };

    /// <summary>
    /// Builds a slug for a single venue combined with the market and contract bits from <paramref name="x"/>.
    /// </summary>
    private static string BuildSlug(Exchange venue, Exchange x)
    {
        var parts = new List<string>();

        // venue
        if (venue == Exchange.None)
        {
            parts.Add("unknown");
        }
        else
        {
            parts.Add(venue switch
            {
                Exchange.Binance => "binance",
                Exchange.OKX => "okx",
                Exchange.KuCoin => "kucoin",
                Exchange.Bybit => "bybit",
                Exchange.Deribit => "deribit",
                Exchange.Bitget => "bitget",
                _ => "unknown"
            });
        }

        // market
        if (x.IsSpot())
            parts.Add("spot");
        else if (x.IsFutures())
            parts.Add("futures");
        else if (x.IsSwap())
            parts.Add("swap");
        else if (x.IsOptions())
            parts.Add("options");

        // contract attrs
        if (x.IsPerpetual())
            parts.Add("perpetual");
        else if (x.IsDelivery())
            parts.Add("delivery");

        if (x.IsUsdMargined())
            parts.Add("usdm");
        else if (x.IsCoinMargined())
            parts.Add("coinm");

        return string.Join("-", parts);
    }
}
