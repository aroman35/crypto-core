using System.Collections.Concurrent;
using CryptoConnector.Binance.Common;
using CryptoCore.MarketData;
using CryptoCore.OrderBook;
using CryptoCore.Primitives;
using CryptoCore.Serialization;

namespace CryptoConnector.Binance.Hosting;

/// <summary>
/// In-memory store of order books. Creates books on demand, fetches snapshots, and applies live L2 updates
/// from a single depth subscription. Provides references to assembled <see cref="OrderBookL2"/>.
/// </summary>
public sealed class OrderBookStore : IAsyncDisposable
{
    private readonly IBinancePublicClient _client;
    private readonly IMarketDataTransport _transport;
    private readonly ISnapshotProvider _snapshots;
    private readonly OrderBookStoreOptions _options;

    // per-symbol state
    private sealed class BookState
    {
        public readonly OrderBookL2 Book;
        public readonly Queue<L2UpdatePooled> Buffer = new();
        public volatile bool SnapshotReady;
        public volatile bool FistCachedUpdateApplied;
        public ulong SnapshotLastUpdateId;
        public int MaxBuffer;

        public BookState(OrderBookL2 book, int maxBuffer)
        {
            Book = book;
            MaxBuffer = maxBuffer;
        }
    }

    private readonly ConcurrentDictionary<Symbol, BookState> _books = new();
    private readonly CancellationTokenSource _cts = new();
    private IMarketDataSubscription<L2UpdatePooled>? _depthSub;
    private Task? _pumpTask;

    public OrderBookStore(IBinancePublicClient client, IMarketDataTransport transport, ISnapshotProvider snapshots, OrderBookStoreOptions? options = null)
    {
        _client = client;
        _transport = transport;
        _snapshots = snapshots;
        _options = options ?? new OrderBookStoreOptions();
    }

    /// <summary>Starts background pump (single depth subscriber) that routes updates into books.</summary>
    public async Task StartAsync(CancellationToken ct = default)
    {
        _depthSub = _transport.SubscribeDepth(); // единственная подписка
        _pumpTask = Task.Run(() => PumpLoop(_cts.Token), _cts.Token);
        await Task.CompletedTask;
    }

    /// <summary>Returns existing or creates a new book for <paramref name="symbol"/>. Triggers snapshot fetch and WS subscription if needed.</summary>
    public async Task<OrderBookL2> GetOrCreateAsync(Symbol symbol, CancellationToken ct = default)
    {
        var st = _books.GetOrAdd(symbol, s => new BookState(new OrderBookL2(s), _options.MaxBufferPerSymbol));

        if (!st.SnapshotReady)
        {
            // 1) subscribe depth with retry
            var stream = BinanceStreams.Depth(symbol.ToString().ToLowerInvariant(), "100ms");
            await WithRetryAsync(() => _client.AddSubscriptionsAsync([stream], ct), ct).ConfigureAwait(false);

            // 2) snapshot with retry
            await WithRetryAsync(async () =>
                {
                    var snap = await _snapshots.GetOrderBookSnapshotAsync(symbol, _options.SnapshotLimit, ct).ConfigureAwait(false);
                    st.Book.Apply(snap);
                    st.SnapshotLastUpdateId = snap.LastUpdateId;

                    // drain buffer
                    while (st.Buffer.TryDequeue(out var cashedUpdate))
                    {
                        using (cashedUpdate)
                        {
                            var orderBookLastUpdateId = st.Book.LastUpdateId;
                            var lastUpdateId = cashedUpdate.LastUpdateId;
                            var firstUpdateId = cashedUpdate.FirstUpdateId;
                            var prevUpdateId = cashedUpdate.PrevLastUpdateId;

                            if (st.FistCachedUpdateApplied &&
                                orderBookLastUpdateId == prevUpdateId)
                            {
                                st.Book.Apply(cashedUpdate);
                                continue;
                            }

                            if (lastUpdateId < orderBookLastUpdateId)
                                continue;

                            if (orderBookLastUpdateId >= firstUpdateId && orderBookLastUpdateId < lastUpdateId)
                            {
                                st.Book.Apply(cashedUpdate);
                                st.FistCachedUpdateApplied = true;
                            }
                        }
                    }
                    st.SnapshotReady = true;
                },
                ct).ConfigureAwait(false);
        }

        return st.Book;
    }

    /// <summary>Gets an existing book by symbol. Returns null if not created.</summary>
    public OrderBookL2? TryGet(Symbol symbol) => _books.TryGetValue(symbol, out var st) ? st.Book : null;

    private async Task FetchAndApplySnapshotAsync(Symbol symbol, BookState st, CancellationToken ct)
    {
        try
        {
            var snap = await _snapshots.GetOrderBookSnapshotAsync(symbol, _options.SnapshotLimit, ct).ConfigureAwait(false);
            st.Book.Apply(snap);
            st.SnapshotLastUpdateId = snap.LastUpdateId;
            st.SnapshotReady = true;

            // применяем буфер по правилам Binance: взять все апдейты, u > lastUpdateId;
            // принять тот, где U <= lastUpdateId+1 <= u
            while (st.Buffer.Count > 0)
            {
                using var u = st.Buffer.Dequeue();
                if (ShouldApplyAfterSnapshot(u, st.SnapshotLastUpdateId))
                {
                    // Принимаем и двигаем lastUpdateId
                    st.Book.Apply(u);
                    if (u.LastUpdateId != 0)
                        st.SnapshotLastUpdateId = u.LastUpdateId;
                }
                // else — просто дроп, u <= lastId
            }
        }
        catch
        {
            // можно добавить ретраи/лог
            throw;
        }
    }

    private static bool ShouldApplyAfterSnapshot(L2UpdatePooled u, ulong lastIdFromSnapshot)
    {
        if (u.LastUpdateId <= lastIdFromSnapshot)
            return false;
        var mustBe = lastIdFromSnapshot + 1;
        return u.FirstUpdateId <= mustBe && u.LastUpdateId >= mustBe;
    }

    private async Task PumpLoop(CancellationToken ct)
    {
        if (_depthSub is null)
            return;

        await foreach (var u in _depthSub.Stream(ct))
        {
            var symbol = u.Symbol;
            if (!_books.TryGetValue(symbol, out var st))
                st = _books.GetOrAdd(symbol, s => new BookState(new OrderBookL2(s), _options.MaxBufferPerSymbol));

            if (!st.SnapshotReady)
            {
                if (st.Buffer.Count >= st.MaxBuffer)
                {
                    using var old = st.Buffer.Dequeue();
                }
                st.Buffer.Enqueue(u);
                continue;
            }

            try
            {
                st.Book.Apply(u);
                // lag мониторинг
                if (_options.LagMonitor is not null)
                {
                    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var lag = TimeSpan.FromMilliseconds(Math.Max(0, nowMs - u.EventTimeMs));
                    _options.LagMonitor(symbol, new LagMetrics(st.Buffer.Count, u.EventTimeMs, lag));
                }
            }
            finally
            {
                u.Dispose();
            }
        }
    }

    private async Task WithRetryAsync(Func<Task> op, CancellationToken ct)
    {
        var delay = _options.InitialBackoff;
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                await op().ConfigureAwait(false);
                return;
            }
            catch when (attempt < _options.MaxRetryAttempts && !ct.IsCancellationRequested)
            {
                await Task.Delay(delay, ct).ConfigureAwait(false);
                // expo backoff + jitter
                var nextMs = Math.Min(delay.TotalMilliseconds * 2, _options.MaxBackoff.TotalMilliseconds);
                var jitter = Random.Shared.Next(0, 100);
                delay = TimeSpan.FromMilliseconds(nextMs + jitter);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        _cts.Cancel();
        if (_depthSub is not null)
            await _depthSub.DisposeAsync();
        if (_pumpTask is not null)
            try
            {
                await _pumpTask;
            }
            catch
            {
                /* ignore */
            }

        // Dispose всего, что ещё осталось в буферах (safety)
        foreach (var kv in _books)
        {
            var st = kv.Value;
            while (st.Buffer.Count > 0)
                using (st.Buffer.Dequeue())
                {
                }
        }
    }
}
