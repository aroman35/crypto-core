using CryptoConnector.Binance.Common;
using CryptoConnector.Binance.Extensions;
using CryptoCore.MarketData;
using CryptoCore.Primitives;
using CryptoCore.Serialization;
using Serilog;

namespace CryptoConnector.Binance.Hosting;

public sealed class TradesStore : IAsyncDisposable
{
    private readonly IBinancePublicClient _client;
    private readonly IMarketDataTransport _transport;
    private readonly Dictionary<int, Action<PublicTrade>> _updatesSubscribers = new();
    private readonly Dictionary<Symbol, HashSet<int>> _symbolsToSubscribersIdx = new();
    private readonly Lock _subscribersLock = new();
    private readonly ILogger _logger;
    private int _nextSubscribeId;
    private IMarketDataSubscription<PublicTrade>? _tradesSubscription;

    private Task? _readTradesLoop;

    public TradesStore(IBinancePublicClient client, IMarketDataTransport transport, ILogger logger)
    {
        _client = client;
        _transport = transport;
        _logger = logger.ForContext<TradesStore>();
    }

    public Task StartAsync(CancellationToken cancellationToken = default)
    {
        _tradesSubscription = _transport.SubscribeTrades();
        _readTradesLoop = Task.Run(() => PumpLoop(cancellationToken), cancellationToken);
        return Task.CompletedTask;
    }

    public async Task<IDisposable> OnTradeReceived(Symbol symbol, Action<PublicTrade> onUpdate)
    {
        ArgumentNullException.ThrowIfNull(onUpdate);
        var stream = BinanceStreams.Trades(symbol.ToString().ToLowerInvariant());
        await _client.AddSubscriptionsAsync([stream]);
        var id = Interlocked.Increment(ref _nextSubscribeId);
        using (_subscribersLock.EnterScope())
        {
            _updatesSubscribers[id] = onUpdate;
            var subscribers = _symbolsToSubscribersIdx.GetOrAdd(symbol, _ => []);
            subscribers.Add(id);
            return new CallbackUnsubscriber(this, id);
        }
    }

    private async Task PumpLoop(CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(_tradesSubscription);
        await foreach (var trade in _tradesSubscription.Stream(cancellationToken))
        {
            if (_symbolsToSubscribersIdx.TryGetValue(trade.Symbol, out var subscribers))
            {
                using (_subscribersLock.EnterScope())
                {
                    foreach (var subscriberId in subscribers)
                    {
                        if (_updatesSubscribers.TryGetValue(subscriberId, out var subscriber))
                        {
                            try
                            {
                                subscriber.Invoke(trade);
                            }
                            catch (Exception e)
                            {
                                _logger.Warning(e, "Error in trades subscriber handler");
                            }
                        }
                    }
                }
            }
        }
    }

    private sealed class CallbackUnsubscriber(TradesStore owner, int id) : IDisposable
    {
        private bool _disposed;

        public void Dispose()
        {
            if (_disposed)
                return;
            _disposed = true;
            using (owner._subscribersLock.EnterScope())
            {
                owner._updatesSubscribers.Remove(id);
            }
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_tradesSubscription is not null)
            await _tradesSubscription.DisposeAsync();
        if (_readTradesLoop is not null)
            await _readTradesLoop;
    }
}
