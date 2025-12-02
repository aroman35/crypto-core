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

    // private readonly Dictionary<double, double> _bids = new();
    // private readonly Dictionary<double, double> _asks = new();

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
    public bool Apply(in L2UpdatePooled update, bool force = false)
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

        if (!force &&
            update.PrevLastUpdateId != 0 &&
            LastUpdateId != 0 &&
            update.PrevLastUpdateId != LastUpdateId)
        {
            return false;
        }

        ApplyAll(update.Deltas.Span);
        if (update.LastUpdateId != 0)
            LastUpdateId = update.LastUpdateId;

        NotifyAfterApply(before);
        return true;
    }

    public (double Price, double Qty) CachedBestBid => (_lastBestBidPx, _lastBestBidQty);
    public (double Price, double Qty) CachedBestAsk => (_lastBestAskPx, _lastBestAskQty);

    /// <summary>Returns current best bid (price, qty) or (0,0) when empty.</summary>
    public (double Price, double Qty) BestBid()
    {
        if (_bidsSorted.Count == 0)
            return (0, 0);

        using var e = _bidsSorted.GetEnumerator();
        if (!e.MoveNext())
            return (0, 0);

        var kv = e.Current;
        return (kv.Key, kv.Value);
    }

    /// <summary>Returns current best ask (price, qty) or (0,0) when empty.</summary>
    public (double Price, double Qty) BestAsk()
    {
        if (_asksSorted.Count == 0)
            return (0, 0);

        using var e = _asksSorted.GetEnumerator();
        if (!e.MoveNext())
            return (0, 0);

        var kv = e.Current;
        return (kv.Key, kv.Value);
    }

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
    private void ApplyOne(in L2Delta delta)
    {
        if (delta.Side == Side.Buy)
        {
            if (delta.IsRemove || delta.Quantity.IsEquals(0.0))
            {
                if (_bidsSorted.Remove(delta.Price, out var bidPrevQty))
                {
                    OnLevelRemoved(Side.Buy, delta.Price, bidPrevQty);
                }
            }
            else
            {
                _bidsSorted[delta.Price] = delta.Quantity;
                OnLevelUpserted(Side.Buy, delta.Price, delta.Quantity);
            }
        }
        else if (delta.Side == Side.Sell)
        {
            if (delta.IsRemove || delta.Quantity.IsEquals(0.0))
            {
                if (_asksSorted.Remove(delta.Price, out var askPrevQty))
                {
                    OnLevelRemoved(Side.Sell, delta.Price, askPrevQty);
                }
            }
            else
            {
                _asksSorted[delta.Price] = delta.Quantity;
                OnLevelUpserted(Side.Sell, delta.Price, delta.Quantity);
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
        var id = Interlocked.Increment(ref _cbNextId);
        _bookCbs[id] = onUpdate;
        return new CallbackUnsubscriber(this, id, isTop: false);
    }

    /// <summary>
    /// Subscribe to notifications when best bid/ask (price or qty) changes. Returns a disposable to unsubscribe.
    /// </summary>
    public IDisposable OnTopUpdated(Action<OrderBookL2> onUpdate)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        var id = Interlocked.Increment(ref _cbNextId);
        _topCbs[id] = onUpdate;
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
        // быстрый early-exit: в твоём тесте подписчик есть, но это всё равно дешёво
        if (_topCbs.Count == 0 && _bookCbs.Count == 0)
            return;

        var (bbPx, bbQty) = BestBid();
        var (baPx, baQty) = BestAsk();

        var topChanged =
            !bbPx.IsEquals(before.BbPx) || !bbQty.IsEquals(before.BbQty) ||
            !baPx.IsEquals(before.BaPx) || !baQty.IsEquals(before.BaQty);

        _lastBestBidPx = bbPx;
        _lastBestBidQty = bbQty;
        _lastBestAskPx = baPx;
        _lastBestAskQty = baQty;

        if (topChanged)
        {
            lock (_cbSync)
            {
                foreach (var cb in _topCbs.Values)
                {
                    try
                    {
                        cb(this);
                    }
                    catch
                    {
                        // Ignored
                    }
                }
            }
        }

        lock (_cbSync)
        {
            foreach (var cb in _bookCbs.Values)
            {
                try
                {
                    cb(this);
                }
                catch
                {
                    // Ignored
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
