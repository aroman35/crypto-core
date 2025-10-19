namespace CryptoCore.Serialization;

/// <summary>
/// Active subscription handle. The stream is exposed as <see cref="IAsyncEnumerable{T}"/>.
/// Disposing unsubscribes and releases any buffered resources.
/// </summary>
public interface IMarketDataSubscription<out T> : IAsyncDisposable
{
    /// <summary>
    /// Asynchronous stream of items. The provided <paramref name="ct"/> cancels enumeration.
    /// </summary>
    IAsyncEnumerable<T> Stream(CancellationToken ct = default);
}
