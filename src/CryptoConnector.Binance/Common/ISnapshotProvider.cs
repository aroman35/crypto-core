using CryptoCore.MarketData;
using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Common;

/// <summary>
/// Provides order book snapshots for a given symbol (depth), as an <see cref="L2Update"/> with <see cref="L2Update.IsSnapshot"/> = true.
/// </summary>
public interface ISnapshotProvider
{
    /// <summary>Fetches a snapshot; <paramref name="limit"/> is the max number of price levels per side.</summary>
    Task<L2Update> GetOrderBookSnapshotAsync(Symbol symbol, int limit, CancellationToken ct = default);
}
