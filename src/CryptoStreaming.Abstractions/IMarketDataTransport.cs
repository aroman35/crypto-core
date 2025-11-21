using CryptoCore.MarketData;

namespace CryptoCore.Serialization;

/// <summary>
/// Transport abstraction between connectors and consumers.
/// Connectors publish parsed messages; consumers subscribe and enumerate async streams.
/// </summary>
public interface IMarketDataTransport : IAsyncDisposable
{
    /// <summary>
    /// Subscribe to depth (L2) updates. Only a single subscriber is allowed for pooled L2 to preserve zero allocations
    /// and clear ownership of pooled messages. A second subscription attempt must throw.
    /// </summary>
    IMarketDataSubscription<L2UpdatePooled> SubscribeDepth(int capacity = 4096);

    /// <summary>
    /// Subscribe to trade stream. Multiple subscribers are supported (fan-out).
    /// </summary>
    IMarketDataSubscription<PublicTrade> SubscribeTrades(int capacity = 8192);

    /// <summary>Non-blocking publish for order book updates; returns false if the transport is currently back-pressured.</summary>
    bool TryPublishDepth(L2UpdatePooled update);

    /// <summary>Non-blocking publish for trades; returns false if the transport is currently back-pressured.</summary>
    bool TryPublishTrade(PublicTrade trade);

    /// <summary>Back-pressured publish for order-book (waits until written or cancelled).</summary>
    ValueTask PublishDepthAsync(L2UpdatePooled update, CancellationToken ct = default);

    /// <summary>Back-pressured publish for trades (waits until written or cancelled).</summary>
    ValueTask PublishTradeAsync(PublicTrade trade, CancellationToken ct = default);
}
