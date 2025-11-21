using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
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
        private readonly TaskCompletionSource _streamDataReceived = new();
        public readonly OrderBookL2 Book;
        public readonly ConcurrentQueue<L2UpdatePooled> Buffer = new();
        public readonly int MaxBuffer;
        public volatile bool SnapshotReady;
        public volatile bool FistCachedUpdateApplied;
        public Task WaitForStreamUpdates => _streamDataReceived.Task;

        public BookState(OrderBookL2 book, int maxBuffer)
        {
            Book = book;
            MaxBuffer = maxBuffer;
        }

        [MethodImpl(MethodImplOptions.Synchronized)]
        public void DataReceived(L2UpdatePooled update)
        {
            if (!_streamDataReceived.Task.IsCompleted)
            {
                _streamDataReceived.TrySetResult();
            }
            Buffer.Enqueue(update);
        }
    }

    private readonly ConcurrentDictionary<Symbol, BookState> _books = new();
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
    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _depthSub = _transport.SubscribeDepth(); // единственная подписка
        _pumpTask = Task.Run(() => PumpLoop(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>Returns existing or creates a new book for <paramref name="symbol"/>. Triggers snapshot fetch and WS subscription if needed.</summary>
    public async Task<OrderBookL2> GetOrCreateAsync(Symbol symbol, CancellationToken cancellationToken = default)
    {
        var bookState = _books.GetOrAdd(symbol, s => new BookState(new OrderBookL2(s), _options.MaxBufferPerSymbol));

        if (!bookState.SnapshotReady)
        {
            // 1) subscribe depth with retry
            var stream = BinanceStreams.Depth(symbol.ToString().ToLowerInvariant());
            await WithRetryAsync(() => _client.AddSubscriptionsAsync([stream], cancellationToken), cancellationToken);

            // 2) snapshot with retry
            await WithRetryAsync(async () =>
                {
                    await bookState.WaitForStreamUpdates.WaitAsync(cancellationToken);
                    var snapshot = await _snapshots.GetOrderBookSnapshotAsync(symbol, _options.SnapshotLimit, cancellationToken);
                    bookState.Book.Apply(snapshot);
                    // drain buffer

                    while (bookState.Buffer.TryDequeue(out var cashedUpdate))
                    {
                        using (cashedUpdate)
                        {
                            var orderBookLastUpdateId = bookState.Book.LastUpdateId;
                            var lastUpdateId = cashedUpdate.LastUpdateId;
                            var firstUpdateId = cashedUpdate.FirstUpdateId;
                            var prevUpdateId = cashedUpdate.PrevLastUpdateId;

                            if (bookState.FistCachedUpdateApplied &&
                                orderBookLastUpdateId == prevUpdateId)
                            {
                                bookState.Book.Apply(cashedUpdate);
                                continue;
                            }

                            if (lastUpdateId < orderBookLastUpdateId)
                                continue;

                            if (orderBookLastUpdateId >= firstUpdateId && orderBookLastUpdateId < lastUpdateId)
                            {
                                bookState.Book.Apply(cashedUpdate, true);
                                bookState.FistCachedUpdateApplied = true;
                            }
                        }
                    }
                    bookState.SnapshotReady = true;
                },
            cancellationToken);
        }

        return bookState.Book;
    }

    /// <summary>Gets an existing book by symbol. Returns null if not created.</summary>
    public OrderBookL2? TryGet(Symbol symbol) => _books.TryGetValue(symbol, out var st) ? st.Book : null;

    private async Task PumpLoop(CancellationToken cancellationToken)
    {
        if (_depthSub is null)
            return;

        await foreach (var update in _depthSub.Stream(cancellationToken))
        {
            var symbol = update.Symbol;
            if (!_books.TryGetValue(symbol, out var bookState))
                bookState = _books.GetOrAdd(symbol, s => new BookState(new OrderBookL2(s), _options.MaxBufferPerSymbol));

            if (!bookState.SnapshotReady)
            {
                if (bookState.Buffer.Count >= bookState.MaxBuffer)
                {
                    bookState.Buffer.TryDequeue(out var old);
                    using (old)
                    {
                    }
                }
                bookState.DataReceived(update);
                continue;
            }

            try
            {
                if (!bookState.FistCachedUpdateApplied &&
                    bookState.Book.LastUpdateId >= update.FirstUpdateId &&
                    bookState.Book.LastUpdateId < update.LastUpdateId)
                {
                    // Если попали сюда, то мы не успели собрать стакан из кэша, ждем первый апдейт
                    bookState.Book.Apply(update, true);
                    bookState.FistCachedUpdateApplied = true;
                    continue;
                }
                bookState.Book.Apply(update);
                // lag мониторинг
                if (_options.LagMonitor is not null)
                {
                    var nowMs = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    var lag = TimeSpan.FromMilliseconds(Math.Max(0, nowMs - update.EventTimeMs));
                    _options.LagMonitor(symbol, new LagMetrics(bookState.Buffer.Count, update.EventTimeMs, lag));
                }
            }
            finally
            {
                update.Dispose();
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
                await op();
                return;
            }
            catch when (attempt < _options.MaxRetryAttempts && !ct.IsCancellationRequested)
            {
                await Task.Delay(delay, ct);
                // expo backoff + jitter
                var nextMs = Math.Min(delay.TotalMilliseconds * 2, _options.MaxBackoff.TotalMilliseconds);
                var jitter = Random.Shared.Next(0, 100);
                delay = TimeSpan.FromMilliseconds(nextMs + jitter);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
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
            while (!st.Buffer.IsEmpty)
            {
                if (st.Buffer.TryDequeue(out var update))
                {
                    using (update)
                    {
                    }
                }
            }
        }
    }
}
