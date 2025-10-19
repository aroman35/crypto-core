using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Common;

/// <summary>
/// Provides <see cref="Symbol"/> from an UTF-8 symbol span (e.g., "BTCUSDT") without allocations.
/// Implement with a cache or venue-specific logic.
/// </summary>
public interface ISymbolProvider
{
    /// <summary>Tries to resolve a symbol from an UTF-8 span.</summary>
    bool TryGet(ReadOnlySpan<byte> utf8Symbol, out Symbol symbol);
}
