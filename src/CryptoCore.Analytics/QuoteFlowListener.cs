using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Primitives;
using CryptoCore.Storage.Models;

namespace CryptoCore.Analytics;

/// <summary>
/// Computes quote-flow and order-book based features over a sliding time window
/// using L2 update batches and the current order book snapshot.
/// </summary>
public sealed class QuoteFlowListener : IMarketDataListener
{
    private readonly long _windowMs;
    private readonly int _depthLevels;

    private readonly Queue<QuoteSample> _window = new();
    private long _sumCancels;
    private long _sumUpdates;
    private long _sumBuyUpdates;
    private long _sumSellUpdates;
    private double _sumBuyVol;
    private double _sumSellVol;

    private readonly struct QuoteSample
    {
        public readonly long Ts;
        public readonly int Cancels;
        public readonly int Updates;
        public readonly int BuyUpdates;
        public readonly int SellUpdates;
        public readonly double BuyVol;
        public readonly double SellVol;

        public QuoteSample(
            long ts,
            int cancels,
            int updates,
            int buyUpdates,
            int sellUpdates,
            double buyVol,
            double sellVol)
        {
            Ts = ts;
            Cancels = cancels;
            Updates = updates;
            BuyUpdates = buyUpdates;
            SellUpdates = sellUpdates;
            BuyVol = buyVol;
            SellVol = sellVol;
        }
    }

    /// <summary>
    /// Cancellation frequency within the window, measured as cancels per second.
    /// </summary>
    public double CancellationFrequency { get; private set; } // cancels / sec

    /// <summary>
    /// Cancellation rate within the window, defined as cancels divided by all quote updates.
    /// </summary>
    public double CancellationRate { get; private set; } // cancels / all updates

    /// <summary>
    /// Mid-price volume-weighted average price computed from top-N bid/ask levels.
    /// </summary>
    public double VWAP { get; private set; } // mid-VWAP top-N

    /// <summary>
    /// Volume imbalance in the top-N levels, defined as (BidQty - AskQty) / (BidQty + AskQty).
    /// </summary>
    public double Imbalance { get; private set; } // (BidQty-AskQty)/(BidQty+AskQty)

    /// <summary>
    /// Update imbalance, defined as (BuyUpdates - SellUpdates) / (AllUpdates) over the window.
    /// </summary>
    public double UpdateRatio { get; private set; } // (BuyUpdates-SellUpdates)/AllUpdates

    /// <summary>
    /// Order flow imbalance, defined as (BuyVol - SellVol) / (BuyVol + SellVol) over the window.
    /// </summary>
    public double OrderFlow { get; private set; } // (BuyVol-SellVol)/(BuyVol+BuyVol)

    /// <summary>
    /// Creates a new quote-flow listener with a given sliding window and order-book depth.
    /// </summary>
    /// <param name="windowMs">Length of the time window in milliseconds.</param>
    /// <param name="depthLevels">Number of top-of-book levels to use for VWAP and imbalance.</param>
    public QuoteFlowListener(long windowMs, int depthLevels)
    {
        _windowMs = windowMs;
        _depthLevels = depthLevels;
    }

    /// <summary>
    /// Processes a batch of L2 deltas, updates the rolling window statistics
    /// and recomputes all quote-flow features using the current order book.
    /// </summary>
    /// <param name="eventTimeMs">Exchange event timestamp in Unix milliseconds (UTC).</param>
    /// <param name="batch">Batch of L2 deltas applied at this event time.</param>
    /// <param name="book">Current L2 order book state after applying the batch.</param>
    public void QuoteBatchReceived(long eventTimeMs, in L2UpdatePooled batch, OrderBookL2 book)
    {
        var span = batch.Deltas.Span;

        int cancels = 0;
        int updates = span.Length;
        int buyUpdates = 0;
        int sellUpdates = 0;
        double buyVol = 0.0;
        double sellVol = 0.0;

        for (int i = 0; i < span.Length; i++)
        {
            ref readonly var d = ref span[i];

            if (d.IsRemove)
                cancels++;

            if (d.Side == Side.Buy)
            {
                buyUpdates++;
                if (!d.IsRemove)
                    buyVol += d.Quantity;
            }
            else if (d.Side == Side.Sell)
            {
                sellUpdates++;
                if (!d.IsRemove)
                    sellVol += d.Quantity;
            }
        }

        EnqueueSample(new QuoteSample(
            eventTimeMs,
            cancels,
            updates,
            buyUpdates,
            sellUpdates,
            buyVol,
            sellVol));

        RecomputeFeatures(eventTimeMs, book);
    }

    public void OrderBookUpdated(long eventTimeMs, OrderBookL2 book) { }
    public void TopUpdated(long eventTimeMs, double bbPx, double bbQty, double baPx, double baQty) { }
    public void TradeReceived(in Trade trade) { }

    private void EnqueueSample(in QuoteSample s)
    {
        _window.Enqueue(s);
        _sumCancels += s.Cancels;
        _sumUpdates += s.Updates;
        _sumBuyUpdates += s.BuyUpdates;
        _sumSellUpdates += s.SellUpdates;
        _sumBuyVol += s.BuyVol;
        _sumSellVol += s.SellVol;
    }

    private void RecomputeFeatures(long nowMs, OrderBookL2 book)
    {
        var cutoff = nowMs - _windowMs;
        while (_window.Count > 0 && _window.Peek().Ts < cutoff)
        {
            var old = _window.Dequeue();
            _sumCancels -= old.Cancels;
            _sumUpdates -= old.Updates;
            _sumBuyUpdates -= old.BuyUpdates;
            _sumSellUpdates -= old.SellUpdates;
            _sumBuyVol -= old.BuyVol;
            _sumSellVol -= old.SellVol;
        }

        var windowSec = _windowMs / 1000.0;

        CancellationFrequency = windowSec > 0
            ? _sumCancels / windowSec
            : 0.0;

        CancellationRate = _sumUpdates > 0
            ? (double)_sumCancels / _sumUpdates
            : 0.0;

        var totalVol = _sumBuyVol + _sumSellVol;
        OrderFlow = totalVol > 0.0
            ? (_sumBuyVol - _sumSellVol) / totalVol
            : 0.0;

        UpdateRatio = _sumUpdates > 0
            ? (double)(_sumBuyUpdates - _sumSellUpdates) / _sumUpdates
            : 0.0;

        // VWAP + Imbalance по топ-N
        const int MAX_DEPTH = 64;
        var depth = Math.Min(_depthLevels, MAX_DEPTH);

        Span<double> bidPx = stackalloc double[depth];
        Span<double> bidQty = stackalloc double[depth];
        Span<double> askPx = stackalloc double[depth];
        Span<double> askQty = stackalloc double[depth];

        var nb = book.CopyTopBids(bidPx, bidQty, depth);
        var na = book.CopyTopAsks(askPx, askQty, depth);

        double vwapBid = 0.0, vwapAsk = 0.0;
        double sumBid = 0.0, sumAsk = 0.0;

        for (int i = 0; i < nb; i++)
        {
            var q = bidQty[i];
            sumBid += q;
            vwapBid += bidPx[i] * q;
        }

        for (int i = 0; i < na; i++)
        {
            var q = askQty[i];
            sumAsk += q;
            vwapAsk += askPx[i] * q;
        }

        if (sumBid > 0.0)
            vwapBid /= sumBid;
        if (sumAsk > 0.0)
            vwapAsk /= sumAsk;

        if (sumBid > 0.0 && sumAsk > 0.0)
            VWAP = 0.5 * (vwapBid + vwapAsk);
        else if (sumBid > 0.0)
            VWAP = vwapBid;
        else if (sumAsk > 0.0)
            VWAP = vwapAsk;
        else
            VWAP = 0.0;

        var qtySum = sumBid + sumAsk;
        Imbalance = qtySum > 0.0
            ? (sumBid - sumAsk) / qtySum
            : 0.0;
    }
}
