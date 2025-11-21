namespace CryptoConnector.Binance.Common;

/// <summary>
/// Minimal Binance WS public client publishing parsed updates with zero allocations.
/// </summary>
public interface IBinancePublicClient : IAsyncDisposable
{
    /// <summary>Starts WS connection and subscribes to given streams.</summary>
    Task StartAsync(CancellationToken cancellationToken = default);

    /// <summary>Subscribes to additional streams at runtime.</summary>
    Task AddSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default);

    /// <summary>Unsubscribes from streams at runtime.</summary>
    Task RemoveSubscriptionsAsync(string[] streamNames, CancellationToken cancellationToken = default);
}
