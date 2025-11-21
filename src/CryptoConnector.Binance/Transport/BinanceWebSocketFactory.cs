using System.Threading.Channels;
using CryptoConnector.Binance.Common;
using CryptoCore.Primitives;
using Serilog;

namespace CryptoConnector.Binance.Transport;

public class BinanceWebSocketFactory(ILogger logger) : IBinanceWebSocketFactory
{
    public Task<IBinanceWebSocketConnection> GetConnection(
        Exchange exchange,
        ChannelWriter<byte[]> inbox,
        CancellationToken cancellationToken)
    {
        var client = BinanceWebSocketWrapper.Create(exchange, inbox, logger, cancellationToken);
        return client;
    }
}
