namespace CryptoConnector.Binance.Common;

public interface IBinanceWebSocketConnection : IAsyncDisposable
{
    public Guid Id { get; }
    public IReadOnlySet<string> Streams { get; }
    public DateTime ConnectedAt { get; }
    public int StreamsCount { get; }
    Task AddSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default);
    Task RemoveSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default);
}
