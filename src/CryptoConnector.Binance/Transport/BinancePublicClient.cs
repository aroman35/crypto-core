using System.Collections.Concurrent;
using System.Text.Json;
using System.Threading.Channels;
using CryptoConnector.Binance.Common;
using CryptoCore.MarketData;
using CryptoCore.Primitives;
using CryptoCore.Serialization;

namespace CryptoConnector.Binance.Transport;

/// <summary>
/// Lightweight WS client for Binance public streams (spot/futures).
/// Handles (re)connect, subscribes to streams, parses messages to <see cref="L2UpdatePooled"/> / <see cref="PublicTrade"/>.
/// </summary>
public sealed class BinancePublicClient : IBinancePublicClient
{
    #region LimitationConstants

    private const int StreamsMaxCount = 1024;
    private const int MaxOutcomingMessagesPerSecond = 10;
    private const int ConnectionTimeToLiveSeconds = 24 * 60 * 60;

    #endregion

    private readonly ISymbolProvider _symbols;
    private readonly Channel<byte[]> _inbox;
    private readonly IMarketDataTransport? _transport;
    private readonly Exchange _exchange;
    private readonly IBinanceWebSocketFactory _binanceWebSocketFactory;
    private readonly TimeProvider _clock;
    private readonly ConcurrentDictionary<Guid, IBinanceWebSocketConnection> _connections = new();
    private volatile bool _isConnected;
    private Task? _parseLoopTask;

    /// <summary>Create client with provided symbol provider.</summary>
    public BinancePublicClient(
        ISymbolProvider symbols,
        IMarketDataTransport transport,
        Exchange exchange,
        IBinanceWebSocketFactory binanceWebSocketFactory,
        TimeProvider clock)
    {
        _transport = transport;
        _symbols = symbols;
        _exchange = exchange;
        _binanceWebSocketFactory = binanceWebSocketFactory;
        _clock = clock;
        _inbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(256)
            { SingleWriter = true, SingleReader = true });
    }

    /// <inheritdoc/>
    public async Task StartAsync(CancellationToken cancellationToken = default)
    {
        if (_isConnected)
            return;

        var client = await _binanceWebSocketFactory.GetConnection(_exchange, _inbox.Writer, cancellationToken);
        _connections.TryAdd(client.Id, client);
        _parseLoopTask = Task.Run(() => ParseLoop(cancellationToken), cancellationToken);
        _isConnected = true;
    }

    private async Task ParseLoop(CancellationToken ct)
    {
        var transport = _transport!;
        await foreach (var msg in _inbox.Reader.ReadAllAsync(ct).ConfigureAwait(false))
        {
            try
            {
                var reader = new Utf8JsonReader(msg, isFinalBlock: true, state: default);
                ReadOnlySpan<byte> type = default;
                ReadOnlySpan<byte> streamName = default;
                ReadOnlySpan<byte> dataSlice = default;
                int dataStart = 0, dataEnd = 0;

                while (reader.Read())
                {
                    if (reader.TokenType == JsonTokenType.PropertyName)
                    {
                        var name = reader.ValueSpan;
                        reader.Read();

                        if (name.SequenceEqual("stream"u8))
                        {
                            if (reader.TokenType == JsonTokenType.String)
                                streamName = reader.ValueSpan;
                        }
                        else if (name.SequenceEqual("data"u8))
                        {
                            dataStart = (int)reader.TokenStartIndex;
                            reader.Skip();
                            dataEnd = (int)reader.BytesConsumed;
                            dataSlice = msg.AsSpan(dataStart, dataEnd - dataStart);
                        }
                        else if (name.SequenceEqual("e"u8))
                        {
                            if (reader.TokenType == JsonTokenType.String)
                                type = reader.ValueSpan;
                        }
                    }
                }

                var payload = dataSlice.IsEmpty ? msg.AsSpan() : dataSlice;

                // trades
                if ((!type.IsEmpty && type.SequenceEqual("trade"u8)) ||
                    (!streamName.IsEmpty && streamName.IndexOf((byte)'@') > 0 && streamName.EndsWith("trade"u8)))
                {
                    if (Parsers.BinanceTradeParser.TryParseTrade(payload, _symbols, _exchange, out var t))
                    {
                        if (!transport.TryPublishTrade(t))
                            await transport.PublishTradeAsync(t, ct).ConfigureAwait(false);
                    }

                    continue;
                }

                // depth
                if ((!type.IsEmpty && type.SequenceEqual("depthUpdate"u8)) ||
                    (!streamName.IsEmpty && streamName.IndexOf((byte)'@') > 0 && streamName.IndexOf("depth"u8) >= 0))
                {
                    if (Parsers.BinanceDepthParser.TryParseDepthUpdate(payload, _symbols, _exchange, out var pooled))
                    {
                        transport.TryPublishDepth(pooled);
                    }
                }
            }
            catch
            {
                // ignored
            }
        }
    }

    /// <inheritdoc />
    public async Task AddSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default)
    {
        var lastKey = _connections.Keys.Last();
        if (_connections.TryGetValue(lastKey, out var client))
        {
            if (client.StreamsCount + streamNames.Length < StreamsMaxCount)
            {
                await client.AddSubscriptionsAsync(streamNames, cancellationToken);
                return;
            }
        }

        client = await _binanceWebSocketFactory.GetConnection(_exchange, _inbox.Writer, cancellationToken);
        await client.AddSubscriptionsAsync(streamNames, cancellationToken);
    }

    /// <inheritdoc />
    public Task RemoveSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default)
    {
        // TODO: match request ids of a subscribe message
        return Task.CompletedTask;
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        foreach (var id in _connections.Keys.ToArray())
        {
            if (_connections.TryRemove(id, out var client))
                await client.DisposeAsync();
        }

        if (_parseLoopTask is not null)
            await _parseLoopTask;
    }
}
