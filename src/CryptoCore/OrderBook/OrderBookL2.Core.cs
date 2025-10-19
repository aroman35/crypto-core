using System.Runtime.CompilerServices;
using CryptoCore.Extensions;
using CryptoCore.MarketData;
using CryptoCore.Primitives;

namespace CryptoCore.OrderBook;

/// <summary>
/// Mutable in-memory L2 order book, assembled from snapshot and incremental <see cref="L2Update"/> batches
/// (и из <see cref="L2UpdatePooled"/> без аллокаций). Хранит уровни в словарях и отсортированных представлениях,
/// даёт быстрый доступ к лучшим ценам и перечислителям уровней.
/// </summary>
public sealed partial class OrderBookL2
{
    /// <summary>Symbol this book belongs to.</summary>
    public Symbol Symbol { get; }

    /// <summary>Last applied venue update id (Binance-like). 0 when unknown.</summary>
    public ulong LastUpdateId { get; private set; }

    /// <summary>Number of price levels on bid side.</summary>
    public int BidLevels => _bidsSorted.Count;

    /// <summary>Number of price levels on ask side.</summary>
    public int AskLevels => _asksSorted.Count;

    private readonly Dictionary<double, double> _bids = new();
    private readonly Dictionary<double, double> _asks = new();

    private readonly SortedDictionary<double, double> _bidsSorted;
    private readonly SortedDictionary<double, double> _asksSorted;

    private readonly object _cbSync = new();
    private readonly Dictionary<int, Action<OrderBookL2>> _bookCbs = new();
    private readonly Dictionary<int, Action<OrderBookL2>> _topCbs = new();
    private int _cbNextId;

    private static readonly IComparer<double> Descending = Comparer<double>.Create((a, b) => b.CompareTo(a));
    private static readonly IComparer<double> Ascending = Comparer<double>.Default;

    private double _lastBestBidPx;
    private double _lastBestBidQty;
    private double _lastBestAskPx;
    private double _lastBestAskQty;

    /// <summary>Create an empty book for the given symbol.</summary>
    public OrderBookL2(Symbol symbol)
    {
        Symbol = symbol;
        _bidsSorted = new SortedDictionary<double, double>(Descending);
        _asksSorted = new SortedDictionary<double, double>(Ascending);
        LastUpdateId = 0;
    }

    /// <summary>Clears the book contents.</summary>
    public void Clear()
    {
        _bids.Clear();
        _asks.Clear();
        _bidsSorted.Clear();
        _asksSorted.Clear();
        LastUpdateId = 0;
    }

    /// <summary>
    /// Apply an immutable update batch. If <paramref name="update"/> is a snapshot, the book is replaced.
    /// If <see cref="L2Update.PrevLastUpdateId"/> is non-zero, checks Binance-like continuity.
    /// Returns <c>false</c> when the update was rejected by continuity rule.
    /// </summary>
    public bool Apply(in L2Update update)
    {
        if (update.Symbol != Symbol)
            throw new InvalidOperationException("Update symbol mismatch.");

        // снимем «топ» до применения
        var before = SnapshotTop();

        if (update.IsSnapshot)
        {
            Clear();
            ApplyAll(update.Deltas.Span);
            LastUpdateId = update.LastUpdateId;
            NotifyAfterApply(before);
            return true;
        }

        if (update.PrevLastUpdateId != 0 && LastUpdateId != 0 && update.PrevLastUpdateId != LastUpdateId)
            return false;

        ApplyAll(update.Deltas.Span);
        if (update.LastUpdateId != 0)
            LastUpdateId = update.LastUpdateId;

        NotifyAfterApply(before);
        return true;
    }

    /// <summary>
    /// Apply a pooled update batch. If <paramref name="update"/> is a snapshot, the book is replaced.
    /// If <see cref="L2UpdatePooled.PrevLastUpdateId"/> is non-zero, checks Binance-like continuity.
    /// Returns <c>false</c> when the update was rejected by continuity rule.
    /// </summary>
    public bool Apply(in L2UpdatePooled update)
    {
        if (update.Symbol != Symbol)
            throw new InvalidOperationException("Update symbol mismatch.");
        var before = SnapshotTop();

        if (update.IsSnapshot)
        {
            Clear();
            ApplyAll(update.Deltas.Span);
            LastUpdateId = update.LastUpdateId;
            NotifyAfterApply(before);
            return true;
        }

        if (update.PrevLastUpdateId != 0 && LastUpdateId != 0 && update.PrevLastUpdateId != LastUpdateId)
            return false;

        ApplyAll(update.Deltas.Span);
        if (update.LastUpdateId != 0)
            LastUpdateId = update.LastUpdateId;

        NotifyAfterApply(before);
        return true;
    }

    /// <summary>Returns current best bid (price, qty) or (0,0) when empty.</summary>
    public (double Price, double Qty) BestBid()
        => _bidsSorted.Count == 0
            ? (0, 0)
            : (_bidsSorted.Keys.MinBy(_ => -_), _bidsSorted[_bidsSorted.Keys.MinBy(_ => -_)]);

    /// <summary>Returns current best ask (price, qty) or (0,0) when empty.</summary>
    public (double Price, double Qty) BestAsk()
        => _asksSorted.Count == 0 ? (0, 0) : (_asksSorted.Keys.Min(), _asksSorted[_asksSorted.Keys.Min()]);

    /// <summary>Enumerates bids (desc) up to <paramref name="maxLevels"/> (0 → no limit).</summary>
    public IEnumerable<(double Price, double Qty)> EnumerateBids(int maxLevels = 0)
    {
        var i = 0;
        foreach (var kv in _bidsSorted)
        {
            yield return (kv.Key, kv.Value);
            if (maxLevels != 0 && ++i >= maxLevels)
                yield break;
        }
    }

    /// <summary>Enumerates asks (asc) up to <paramref name="maxLevels"/> (0 → no limit).</summary>
    public IEnumerable<(double Price, double Qty)> EnumerateAsks(int maxLevels = 0)
    {
        var i = 0;
        foreach (var kv in _asksSorted)
        {
            yield return (kv.Key, kv.Value);
            if (maxLevels != 0 && ++i >= maxLevels)
                yield break;
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyAll(ReadOnlySpan<L2Delta> deltas)
    {
        for (var i = 0; i < deltas.Length; i++)
            ApplyOne(in deltas[i]);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ApplyOne(in L2Delta d)
    {
        if (d.Side == Side.Buy)
        {
            if (d.IsRemove || d.Quantity.IsEquals(0.0))
            {
                if (_bids.Remove(d.Price))
                {
                    var prevQty = _bidsSorted[d.Price];
                    _bidsSorted.Remove(d.Price);
                    OnLevelRemoved(Side.Buy, d.Price, prevQty);
                }
            }
            else
            {
                _bids[d.Price] = d.Quantity;
                _bidsSorted[d.Price] = d.Quantity;
                OnLevelUpserted(Side.Buy, d.Price, d.Quantity);
            }
        }
        else if (d.Side == Side.Sell)
        {
            if (d.IsRemove || d.Quantity.IsEquals(0.0))
            {
                if (_asks.Remove(d.Price))
                {
                    var prevQty = _asksSorted[d.Price];
                    _asksSorted.Remove(d.Price);
                    OnLevelRemoved(Side.Sell, d.Price, prevQty);
                }
            }
            else
            {
                _asks[d.Price] = d.Quantity;
                _asksSorted[d.Price] = d.Quantity;
                OnLevelUpserted(Side.Sell, d.Price, d.Quantity);
            }
        }
    }

    // --- hooks for aggregates (implemented in the partial class with aggregates) ---
    partial void OnLevelRemoved(Side side, double price, double prevQty);
    partial void OnLevelUpserted(Side side, double price, double qty);

    /// <summary>
    /// Subscribe to notifications after any batch is applied. Returns a disposable to unsubscribe.
    /// </summary>
    public IDisposable OnBookUpdated(Action<OrderBookL2> onUpdate)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        int id;
        lock (_cbSync)
        {
            id = ++_cbNextId;
            _bookCbs[id] = onUpdate;
        }
        return new CallbackUnsubscriber(this, id, isTop: false);
    }

    /// <summary>
    /// Subscribe to notifications when best bid/ask (price or qty) changes. Returns a disposable to unsubscribe.
    /// </summary>
    public IDisposable OnTopUpdated(Action<OrderBookL2> onUpdate)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        int id;
        lock (_cbSync)
        {
            id = ++_cbNextId;
            _topCbs[id] = onUpdate;
        }
        return new CallbackUnsubscriber(this, id, isTop: true);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private (double BbPx, double BbQty, double BaPx, double BaQty) SnapshotTop()
    {
        var (bbPx, bbQty) = BestBid();
        var (baPx, baQty) = BestAsk();
        return (bbPx, bbQty, baPx, baQty);
    }

    private void NotifyAfterApply((double BbPx, double BbQty, double BaPx, double BaQty) before)
    {
        var (bbPx, bbQty) = BestBid();
        var (baPx, baQty) = BestAsk();

        var topChanged =
            !bbPx.IsEquals(before.BbPx) || !bbQty.IsEquals(before.BbQty) ||
            !baPx.IsEquals(before.BaPx) || !baQty.IsEquals(before.BaQty);

        // обновим кэш
        _lastBestBidPx = bbPx;
        _lastBestBidQty = bbQty;
        _lastBestAskPx = baPx;
        _lastBestAskQty = baQty;

        // уведомления: сперва top (если изменился), потом book
        if (topChanged)
        {
            Action<OrderBookL2>[] copy;
            lock (_cbSync)
                copy = _topCbs.Values.ToArray();
            foreach (var cb in copy)
            {
                try
                {
                    cb(this);
                }
                catch
                { /* swallow */
                }
            }
        }

        {
            Action<OrderBookL2>[] copy;
            lock (_cbSync)
                copy = _bookCbs.Values.ToArray();
            foreach (var cb in copy)
            {
                try
                {
                    cb(this);
                }
                catch
                { /* swallow */
                }
            }
        }
    }

    private sealed class CallbackUnsubscriber : IDisposable
    {
        private readonly OrderBookL2 _owner;
        private readonly int _id;
        private readonly bool _isTop;
        private bool _disposed;

        public CallbackUnsubscriber(OrderBookL2 owner, int id, bool isTop)
        {
            _owner = owner;
            _id = id;
            _isTop = isTop;
        }

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            lock (_owner._cbSync)
            {
                if (_isTop)
                    _owner._topCbs.Remove(_id);
                else
                    _owner._bookCbs.Remove(_id);
            }
        }
    }
}
