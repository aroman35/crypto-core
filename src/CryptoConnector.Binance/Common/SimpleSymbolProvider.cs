using System.Text;
using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Common;

/// <inheritdoc />
public sealed class SimpleSymbolProvider : ISymbolProvider
{
    /// <inheritdoc />
    public bool TryGet(ReadOnlySpan<byte> utf8Symbol, out Symbol symbol)
    {
        // Warning: allocates; replace with your zero-alloc resolver if needed.
        var s = Encoding.ASCII.GetString(utf8Symbol);
        symbol = Symbol.Get(s);
        return true;
    }
}
