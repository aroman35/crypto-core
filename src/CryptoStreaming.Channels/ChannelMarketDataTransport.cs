using System.Runtime.CompilerServices;
using System.Threading.Channels;
using CryptoCore.MarketData;
using CryptoCore.Serialization;

namespace CryptoStreaming;

/// <summary>
/// Channel-based transport with subscription handles. Depth (L2) supports a single subscriber (pooled ownership),
/// trades support multiple subscribers (fan-out).
/// </summary>
public sealed class ChannelMarketDataTransport : IMarketDataTransport
{
    // --- Trades (multi-subscriber fan-out) ---
    private readonly object _tradeSync = new();
    private readonly List<Channel<PublicTrade>> _tradeChannels = new();
    // --- Depth (single subscriber, pooled ownership) ---
    private Channel<L2UpdatePooled>? _depthChannel;
    private bool _hasDepthSubscriber;

    /// <inheritdoc />
    public IMarketDataSubscription<L2UpdatePooled> SubscribeDepth(int capacity = 4096)
    {
        if (_hasDepthSubscriber)
            throw new InvalidOperationException("Depth stream already has a subscriber (pooled ownership).");

        _depthChannel = Channel.CreateBounded<L2UpdatePooled>(new BoundedChannelOptions(capacity)
        {
            SingleWriter = true,
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait
        });
        _hasDepthSubscriber = true;

        var reader = _depthChannel.Reader;
        return new DepthSubscription(this, reader);
    }

    /// <inheritdoc />
    public IMarketDataSubscription<PublicTrade> SubscribeTrades(int capacity = 8192)
    {
        Channel<PublicTrade> ch;
        lock (_tradeSync)
        {
            ch = Channel.CreateBounded<PublicTrade>(new BoundedChannelOptions(capacity)
            {
                SingleWriter = true,
                SingleReader = false,
                FullMode = BoundedChannelFullMode.Wait
            });
            _tradeChannels.Add(ch);
        }
        return new TradeSubscription(this, ch, ch.Reader);
    }

    /// <inheritdoc />
    public bool TryPublishDepth(L2UpdatePooled update)
        => _depthChannel is not null && _depthChannel.Writer.TryWrite(update);

    /// <inheritdoc />
    public async ValueTask PublishDepthAsync(L2UpdatePooled update, CancellationToken ct = default)
    {
        var ch = _depthChannel ?? throw new InvalidOperationException("No depth subscriber.");
        while (!ch.Writer.TryWrite(update))
        {
            if (!await ch.Writer.WaitToWriteAsync(ct).ConfigureAwait(false))
                throw new OperationCanceledException(ct);
        }
    }

    /// <inheritdoc />
    public bool TryPublishTrade(PublicTrade trade)
    {
        var any = false;
        lock (_tradeSync)
        {
            foreach (var ch in _tradeChannels.ToArray())
            {
                any = true;
                ch.Writer.TryWrite(trade); // best-effort; backpressure handled in async path if needed
            }
        }
        return any;
    }

    /// <inheritdoc />
    public async ValueTask PublishTradeAsync(PublicTrade trade, CancellationToken ct = default)
    {
        List<Channel<PublicTrade>> snapshot;
        lock (_tradeSync)
            snapshot = new List<Channel<PublicTrade>>(_tradeChannels);

        foreach (var ch in snapshot)
        {
            while (!ch.Writer.TryWrite(trade))
            {
                if (!await ch.Writer.WaitToWriteAsync(ct).ConfigureAwait(false))
                    throw new OperationCanceledException(ct);
            }
        }
    }

    /// <inheritdoc />
    public async ValueTask DisposeAsync()
    {
        // Complete depth channel, drain & dispose pooled items if reader never consumes them
        if (_depthChannel is not null)
        {
            _depthChannel.Writer.TryComplete();
            while (await _depthChannel.Reader.WaitToReadAsync().ConfigureAwait(false))
            {
                while (_depthChannel.Reader.TryRead(out var u))
                    u.Dispose();
            }
        }

        lock (_tradeSync)
        {
            foreach (var ch in _tradeChannels)
                ch.Writer.TryComplete();
            _tradeChannels.Clear();
        }
    }

    // --------- subscription implementations ----------
    private sealed class DepthSubscription : IMarketDataSubscription<L2UpdatePooled>
    {
        private readonly ChannelMarketDataTransport _owner;
        private readonly ChannelReader<L2UpdatePooled> _reader;
        private bool _disposed;

        public DepthSubscription(ChannelMarketDataTransport owner, ChannelReader<L2UpdatePooled> reader)
        {
            _owner = owner;
            _reader = reader;
        }

        public async IAsyncEnumerable<L2UpdatePooled> Stream([EnumeratorCancellation] CancellationToken ct = default)
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (_reader.TryRead(out var item))
                    yield return item; // ВАЖНО: потребитель обязан Dispose() каждого элемента
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return default;
            _disposed = true;

            // Завершаем канал и дренируем оставшиеся элементы (Dispose pooled)
            if (_owner._depthChannel is not null)
            {
                _owner._depthChannel.Writer.TryComplete();
                _ = DrainLeftovers(_owner._depthChannel);
                _owner._depthChannel = null;
            }
            _owner._hasDepthSubscriber = false;
            return default;

            static async Task DrainLeftovers(Channel<L2UpdatePooled> ch)
            {
                while (await ch.Reader.WaitToReadAsync().ConfigureAwait(false))
                    while (ch.Reader.TryRead(out var u))
                        u.Dispose();
            }
        }
    }

    private sealed class TradeSubscription : IMarketDataSubscription<PublicTrade>
    {
        private readonly ChannelMarketDataTransport _owner;
        private readonly Channel<PublicTrade> _channel;
        private readonly ChannelReader<PublicTrade> _reader;
        private bool _disposed;

        public TradeSubscription(ChannelMarketDataTransport owner, Channel<PublicTrade> channel, ChannelReader<PublicTrade> reader)
        {
            _owner = owner;
            _channel = channel;
            _reader = reader;
        }

        public async IAsyncEnumerable<PublicTrade> Stream([EnumeratorCancellation] CancellationToken ct = default)
        {
            while (await _reader.WaitToReadAsync(ct).ConfigureAwait(false))
            {
                while (_reader.TryRead(out var item))
                    yield return item;
            }
        }

        public ValueTask DisposeAsync()
        {
            if (_disposed)
                return default;
            _disposed = true;

            _channel.Writer.TryComplete();
            lock (_owner._tradeSync)
                _owner._tradeChannels.Remove(_channel);

            return default;
        }
    }
}
