using System.Buffers;
using System.Net.WebSockets;
using System.Text;
using System.Threading.Channels;
using CryptoConnector.Binance.Common;
using CryptoCore.Primitives;
using Serilog;

namespace CryptoConnector.Binance.Transport;

public sealed class BinanceWebSocketWrapper : IBinanceWebSocketConnection
{
    private readonly ClientWebSocket _webSocket;
    private readonly ChannelWriter<byte[]> _inbox;
    private readonly HashSet<string> _streams = new(StringComparer.Ordinal);
    private readonly ILogger _logger;

    private Task? _receiveLoopTask;
    private static uint _requestId;

    public Guid Id { get; } = Guid.NewGuid();
    public IReadOnlySet<string> Streams => _streams;
    public DateTime ConnectedAt { get; private set; }
    public int StreamsCount { get; private set; }

    private BinanceWebSocketWrapper(
        ClientWebSocket clientWebSocket,
        ChannelWriter<byte[]> inbox,
        ILogger logger)
    {
        _webSocket = clientWebSocket;
        _inbox = inbox;
        _logger = logger.ForContext<BinanceWebSocketWrapper>();
    }

    public static async Task<IBinanceWebSocketConnection> Create(
        Exchange exchange,
        ChannelWriter<byte[]> inbox,
        ILogger logger,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var webSocketClient = new ClientWebSocket();
            var client = new BinanceWebSocketWrapper(webSocketClient, inbox, logger);
            await webSocketClient.ConnectAsync(new Uri(BinanceStreams.GetBaseUrl(exchange)), cancellationToken);
            client._receiveLoopTask = Task.Run(() => client.ReceiveLoop(cancellationToken), cancellationToken);
            client._logger.Information("Websocket {Id} created", client.Id);
            return client;
        }
        catch (Exception exception)
        {
            logger.ForContext<BinanceWebSocketWrapper>().Error(exception, "Unable to create websocket client");
            throw;
        }
    }

    private async Task ReceiveLoop(CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(1 << 14);

        try
        {
            while (!cancellationToken.IsCancellationRequested && _webSocket.State == WebSocketState.Open)
            {
                var mem = new ArraySegment<byte>(buffer);
                var result = await _webSocket.ReceiveAsync(mem, cancellationToken).ConfigureAwait(false);
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
                    result = await _webSocket.ReceiveAsync(next, cancellationToken).ConfigureAwait(false);
                    count += result.Count;
                }

                var msg = new byte[count];
                Buffer.BlockCopy(buffer, 0, msg, 0, count);
                await _inbox.WriteAsync(msg, cancellationToken).ConfigureAwait(false);
            }
        }
        catch (Exception exception)
        {
            _logger.Error(exception, "Unable to handle incoming message");
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
            _inbox.TryComplete();
        }
    }

    private static string BuildSubscribe(IReadOnlyList<string> streams)
        => $"{{\"method\":\"SUBSCRIBE\",\"params\":[{string.Join(',', streams.Select(s => $"\"{s}\""))}],\"id\":{++_requestId}}}";

    private static string BuildUnsubscribe(IReadOnlyList<string> streams)
        => $"{{\"method\":\"UNSUBSCRIBE\",\"params\":[{string.Join(',', streams.Select(s => $"\"{s}\""))}],\"id\":1}}";

    /// <inheritdoc />
    public async Task AddSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default)
    {
        var streamsToSubscribe = new List<string>();
        foreach (var streamName in streamNames)
        {
            if (_streams.Add(streamName))
            {
                StreamsCount++;
                streamsToSubscribe.Add(streamName);
                _logger.Information("Added subscription: {SubscriptionName}", streamName);
            }
        }

        var msg = Encoding.UTF8.GetBytes(BuildSubscribe(streamsToSubscribe));
        await _webSocket.SendAsync(msg, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    /// <inheritdoc />
    public async Task RemoveSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default)
    {
        foreach (var s in streamNames)
        {
            _streams.Remove(s);
            StreamsCount--;
        }

        var msg = Encoding.UTF8.GetBytes(BuildUnsubscribe(streamNames));
        await _webSocket.SendAsync(msg, WebSocketMessageType.Text, true, cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        _webSocket.Dispose();
        if (_receiveLoopTask is not null)
            await _receiveLoopTask;
        _logger.Information("WebSocket {Id} disposed", Id);
    }
}
