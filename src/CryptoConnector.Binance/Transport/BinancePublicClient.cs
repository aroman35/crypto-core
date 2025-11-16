using System.Buffers;
using System.Net.WebSockets;
using System.Text;
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
    private readonly ISymbolProvider _symbols;
    private readonly Channel<byte[]> _inbox;
    private readonly HashSet<string> _streams = new(StringComparer.Ordinal);
    private Exchange _exchange;
    private ClientWebSocket? _ws;
    private IMarketDataTransport? _transport;
    private bool _combinedMode;
    private string? _baseUrl;

    /// <summary>Create client with provided symbol provider.</summary>
    public BinancePublicClient(ISymbolProvider symbols)
    {
        _symbols = symbols;
        _inbox = Channel.CreateBounded<byte[]>(new BoundedChannelOptions(256)
            { SingleWriter = true, SingleReader = true });
    }

    /// <inheritdoc/>
    public async Task StartAsync(Exchange exchange, string[] streamNames, IMarketDataTransport transport, CancellationToken ct = default)
    {
        _combinedMode = false;
        _exchange = exchange;
        _baseUrl = BinanceStreams.GetBaseUrl(_exchange);
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _streams.Clear();
        foreach (var s in streamNames)
            _streams.Add(s);

        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(_baseUrl), ct).ConfigureAwait(false);

        if (_streams.Count > 0)
        {
            var sub = BuildSubscribe(_streams.ToArray());
            var subBytes = Encoding.UTF8.GetBytes(sub);
            await _ws.SendAsync(subBytes, WebSocketMessageType.Text, endOfMessage: true, ct).ConfigureAwait(false);
        }

        _ = Task.Run(() => ReceiveLoop(ct), ct);
        _ = Task.Run(() => ParseLoop(ct), ct);
    }

    /// <inheritdoc/>
    public async Task StartCombinedAsync(string combinedBaseUrl, string[] streamNames, IMarketDataTransport transport, CancellationToken ct = default)
    {
        _combinedMode = true;
        _transport = transport ?? throw new ArgumentNullException(nameof(transport));
        _baseUrl = combinedBaseUrl.TrimEnd('/'); // e.g. wss://stream.binance.com:9443/stream?streams=
        _streams.Clear();
        foreach (var s in streamNames)
            _streams.Add(s);

        var url = BuildCombinedUrl(_baseUrl!, _streams);
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);

        _ = Task.Run(() => ReceiveLoop(ct), ct);
        _ = Task.Run(() => ParseLoop(ct), ct);
    }

    private async Task ReceiveLoop(CancellationToken ct)
    {
        var ws = _ws!;
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 14);

        try
        {
            while (!ct.IsCancellationRequested && ws.State == WebSocketState.Open)
            {
                var mem = new ArraySegment<byte>(buffer);
                var result = await ws.ReceiveAsync(mem, ct).ConfigureAwait(false);
                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                var count = result.Count;
                while (!result.EndOfMessage)
                {
                    if (count >= buffer.Length)
                    {
                        var old = buffer;
                        buffer = ArrayPool<byte>.Shared.Rent(old.Length * 2);
                        Array.Copy(old, buffer, old.Length);
                        ArrayPool<byte>.Shared.Return(old);
                    }

                    var next = new ArraySegment<byte>(buffer, count, buffer.Length - count);
                    result = await ws.ReceiveAsync(next, ct).ConfigureAwait(false);
                    count += result.Count;
                }

                var msg = new byte[count];
                Buffer.BlockCopy(buffer, 0, msg, 0, count);
                await _inbox.Writer.WriteAsync(msg, ct).ConfigureAwait(false);
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _inbox.Writer.TryComplete();
        }
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
                    if (Parsers.BinanceTradeParser.TryParseTrade(payload, _symbols, out var t))
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
            finally
            {
                // msg array — отдаём GC (копия на каждый кадр)
            }
        }
    }

    private static string BuildSubscribe(string[] streams)
        => $"{{\"method\":\"SUBSCRIBE\",\"params\":[{string.Join(',', streams.Select(s => $"\"{s}\""))}],\"id\":1}}";

    private static string BuildUnsubscribe(string[] streams)
        => $"{{\"method\":\"UNSUBSCRIBE\",\"params\":[{string.Join(',', streams.Select(s => $"\"{s}\""))}],\"id\":1}}";

    private static string BuildCombinedUrl(string baseUrl, IEnumerable<string> names)
        => $"{baseUrl}/stream?streams={string.Join('/', names)}";

    /// <inheritdoc />
    public async Task AddSubscriptionsAsync(string[] streamNames, CancellationToken ct = default)
    {
        foreach (var s in streamNames)
            _streams.Add(s);

        if (_combinedMode)
        {
            await ReconnectCombinedAsync(ct).ConfigureAwait(false);
        }
        else
        {
            var msg = Encoding.UTF8.GetBytes(BuildSubscribe(streamNames));
            await _ws!.SendAsync(msg, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
    }

    private async Task ReconnectCombinedAsync(CancellationToken ct)
    {
        // закрываем и открываем с новым списком стримов
        if (_ws is { } ws)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "reconnect", ct);
            }
            catch
            {
                // ignored
            }

            ws.Dispose();
        }
        var url = BuildCombinedUrl(_baseUrl!, _streams);
        _ws = new ClientWebSocket();
        await _ws.ConnectAsync(new Uri(url), ct).ConfigureAwait(false);

        // перезапускаем лупы при необходимости (упрощённо можно оставить прежние, если они сами завершатся от Close)
        _ = Task.Run(() => ReceiveLoop(ct), ct);
        _ = Task.Run(() => ParseLoop(ct), ct);
    }

    /// <inheritdoc />
    public async Task RemoveSubscriptionsAsync(string[] streamNames, CancellationToken ct = default)
    {
        foreach (var s in streamNames)
            _streams.Remove(s);

        if (_combinedMode)
        {
            await ReconnectCombinedAsync(ct).ConfigureAwait(false);
        }
        else
        {
            var msg = Encoding.UTF8.GetBytes(BuildUnsubscribe(streamNames));
            await _ws!.SendAsync(msg, WebSocketMessageType.Text, true, ct).ConfigureAwait(false);
        }
    }

    /// <inheritdoc/>
    public async ValueTask DisposeAsync()
    {
        if (_ws is { } ws)
        {
            try
            {
                await ws.CloseAsync(WebSocketCloseStatus.NormalClosure, "bye", CancellationToken.None);
            }
            catch
            {
                /* ignore */
            }

            ws.Dispose();
        }
    }
}
