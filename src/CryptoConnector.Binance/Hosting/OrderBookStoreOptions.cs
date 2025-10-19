using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Hosting;

/// <summary>Configuration for <see cref="OrderBookStore"/>.</summary>
public sealed class OrderBookStoreOptions
{
    /// <summary>Max queued L2 updates per symbol before snapshot is applied.</summary>
    public int MaxBufferPerSymbol { get; init; } = 4096;

    /// <summary>Max depth levels requested in snapshot.</summary>
    public int SnapshotLimit { get; init; } = 1000;

    /// <summary>Retry attempts for snapshot fetch and subscribe calls.</summary>
    public int MaxRetryAttempts { get; init; } = 5;

    /// <summary>Initial backoff delay.</summary>
    public TimeSpan InitialBackoff { get; init; } = TimeSpan.FromMilliseconds(250);

    /// <summary>Max backoff delay.</summary>
    public TimeSpan MaxBackoff { get; init; } = TimeSpan.FromSeconds(5);

    /// <summary>Optional lag monitor callback invoked on each processed L2 update.</summary>
    public Action<Symbol, LagMetrics>? LagMonitor { get; init; }
}

/// <summary>Lag info reported by the store.</summary>
public readonly record struct LagMetrics(
    int BufferedCount,
    long LastEventTimeMs,
    TimeSpan IngestLag
);
