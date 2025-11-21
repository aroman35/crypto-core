using System.Threading.Channels;
using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Common;

public interface IBinanceWebSocketFactory
{
    Task<IBinanceWebSocketConnection> GetConnection(
        Exchange exchange,
        ChannelWriter<byte[]> inbox,
        CancellationToken cancellationToken);
}
