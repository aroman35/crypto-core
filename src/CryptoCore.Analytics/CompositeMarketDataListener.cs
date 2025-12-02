using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Storage.Models;

namespace CryptoCore.Analytics;

public sealed class CompositeMarketDataListener(params IMarketDataListener[] listeners) : IMarketDataListener
{
    private readonly IReadOnlyList<IMarketDataListener> _listeners = listeners;

    public void QuoteBatchReceived(long eventTimeMs, in L2UpdatePooled batch, OrderBookL2 book)
    {
        foreach (var l in _listeners)
            l.QuoteBatchReceived(eventTimeMs, in batch, book);
    }

    public void OrderBookUpdated(long eventTimeMs, OrderBookL2 book)
    {
        foreach (var l in _listeners)
            l.OrderBookUpdated(eventTimeMs, book);
    }

    public void TopUpdated(long eventTimeMs, double bestBidPrice, double bestBidQty, double bestAskPrice, double bestAskQty)
    {
        foreach (var l in _listeners)
            l.TopUpdated(eventTimeMs, bestBidPrice, bestBidQty, bestAskPrice, bestAskQty);
    }

    public void TradeReceived(in Trade trade)
    {
        foreach (var l in _listeners)
            l.TradeReceived(in trade);
    }
}
