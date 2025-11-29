using System.Globalization;
using System.Runtime.InteropServices;
using CryptoCore.Primitives;
using CryptoCore.Storage.Models.Enums;

namespace CryptoCore.Storage.Models;

/// <summary>
/// Logical identifier of a market data stream:
/// symbol (exchange + ticker), trading date, and feed type.
/// Used both for file naming and as a part of the file header.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MarketDataHash(Symbol symbol, DateOnly date, FeedType feed) : IEquatable<MarketDataHash>
{
    /// <summary>
    /// Instrument symbol (typically includes exchange and ticker).
    /// </summary>
    public readonly Symbol Symbol = symbol;

    /// <summary>
    /// Trading date this file belongs to (UTC calendar date).
    /// </summary>
    public readonly DateOnly Date = date;

    /// <summary>
    /// Feed type (e.g. trades, L2, ticker, options, etc.).
    /// </summary>
    public readonly FeedType Feed = feed;

    /// <inheritdoc />
    public bool Equals(MarketDataHash other)
        => Feed == other.Feed && Date.Equals(other.Date) && Symbol.Equals(other.Symbol);

    /// <inheritdoc />
    public override bool Equals(object? obj)
        => obj is MarketDataHash other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
        => HashCode.Combine(Symbol, Date, (int)Feed);

    /// <summary>
    /// Define path for storing the data
    /// </summary>
    /// <param name="directory">Source directory</param>
    /// <param name="fileExtension">File Extension</param>
    /// <returns></returns>
    /// <exception cref="ArgumentException">Feed type must be defined in this struct</exception>
    public string FilePath(string? directory, string fileExtension = ".dat")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (Feed is FeedType.Unknown)
            throw new ArgumentException($"Unknown feed type: {Feed}");

        var exchangeDirectory = Path.Combine(directory, Symbol.Exchange.ToString());
        if (!Directory.Exists(exchangeDirectory))
            Directory.CreateDirectory(exchangeDirectory);

        var tickerPath = Path.Combine(exchangeDirectory, Symbol.ToString());

        if (!Directory.Exists(tickerPath))
            Directory.CreateDirectory(tickerPath);

        var filePath = Path.Combine(tickerPath, Date.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)) + $"_{Feed.ToString().ToLowerInvariant()}{fileExtension}";
        return filePath;
    }

    public static bool operator ==(MarketDataHash left, MarketDataHash right) => left.Equals(right);
    public static bool operator !=(MarketDataHash left, MarketDataHash right) => !left.Equals(right);

    /// <summary>
    /// Returns a human-readable representation in the form:
    /// <c>SYMBOL[dd.MM.yyyy] FEED</c>.
    /// </summary>
    public override string ToString()
    {
        return Feed == FeedType.Unknown
            ? $"{Symbol}[{Date:dd.MM.yyyy}]"
            : $"{Symbol}[{Date:dd.MM.yyyy}] {Feed}";
    }
}
