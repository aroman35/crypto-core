using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Storage.Models;

namespace CryptoCore.Analytics;

/// <summary>
/// Listener for market data replay events.
/// Implementations consume order book and trade events
/// to build research features, labels and metrics.
/// </summary>
public interface IMarketDataListener
{
    /// <summary>
    /// Called when a batch of L2 deltas is read from the replay source.
    /// This represents a single logical update step in the order book stream.
    /// </summary>
    /// <param name="eventTimeMs">
    /// Exchange event timestamp in Unix milliseconds (UTC) associated with this batch.
    /// </param>
    /// <param name="batch">
    /// Aggregated batch of L2 updates (bids/asks) for the current event time.
    /// </param>
    /// <param name="book">
    /// Current state of the L2 order book at the moment this batch is processed.
    /// Implementations must treat this instance as read-only.
    /// </param>
    void QuoteBatchReceived(long eventTimeMs, in L2UpdatePooled batch, OrderBookL2 book);

    /// <summary>
    /// Called after any change to the order book has been applied
    /// (i.e. after processing a batch of L2 updates).
    /// </summary>
    /// <param name="eventTimeMs">
    /// Exchange event timestamp in Unix milliseconds (UTC).
    /// </param>
    /// <param name="book">
    /// Updated state of the L2 order book. Implementations must not mutate it.
    /// </param>
    void OrderBookUpdated(long eventTimeMs, OrderBookL2 book);

    /// <summary>
    /// Called when the top of book changes (best bid and/or best ask).
    /// </summary>
    /// <param name="eventTimeMs">
    /// Exchange event timestamp in Unix milliseconds (UTC).
    /// </param>
    /// <param name="bestBidPrice">Best bid price after the update.</param>
    /// <param name="bestBidQty">Aggregated quantity at the best bid price.</param>
    /// <param name="bestAskPrice">Best ask price after the update.</param>
    /// <param name="bestAskQty">Aggregated quantity at the best ask price.</param>
    void TopUpdated(
        long eventTimeMs,
        double bestBidPrice,
        double bestBidQty,
        double bestAskPrice,
        double bestAskQty);

    /// <summary>
    /// Called when a trade print is received from the replay source.
    /// </summary>
    /// <param name="trade">
    /// Executed trade (timestamp, side, price, quantity).
    /// </param>
    void TradeReceived(in Trade trade);
}
