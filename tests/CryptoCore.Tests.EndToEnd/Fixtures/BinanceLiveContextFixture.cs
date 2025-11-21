using CryptoConnector.Binance.Common;
using CryptoConnector.Binance.Hosting;
using CryptoConnector.Binance.Snapshots;
using CryptoConnector.Binance.Transport;
using CryptoCore.Primitives;
using CryptoCore.Serialization;
using CryptoStreaming;
using Serilog;
using Serilog.Sinks.XUnit3;

namespace CryptoCore.Tests.EndToEnd.Fixtures;

public sealed class BinanceLiveContextFixture : IAsyncLifetime
{
    private readonly HttpClient _httpClient = new();

    public BinanceLiveContextFixture()
    {
        Logger = new LoggerConfiguration()
            .WriteTo
            .XUnit3TestOutput()
            .CreateLogger();

        var binanceSnapshotProvider = new BinanceSnapshotProvider(_httpClient);
        var binanceSocketFactory = new BinanceWebSocketFactory(Logger);

        BinancePublicClient = new BinancePublicClient(
            new SimpleSymbolProvider(),
            ChannelMarketDataTransport,
            Exchange.BinanceFutures,
            binanceSocketFactory,
            TimeProvider.System);

        OrderBookStore = new OrderBookStore(BinancePublicClient, ChannelMarketDataTransport, binanceSnapshotProvider);
        TradesStore = new TradesStore(BinancePublicClient, ChannelMarketDataTransport, Logger);
    }

    public IMarketDataTransport ChannelMarketDataTransport { get; } = new ChannelMarketDataTransport();

    public IBinancePublicClient BinancePublicClient { get; }

    public OrderBookStore OrderBookStore { get; }

    public TradesStore TradesStore { get; }

    public ILogger Logger { get; }

    public async ValueTask DisposeAsync()
    {
        await ChannelMarketDataTransport.DisposeAsync();
        await BinancePublicClient.DisposeAsync();
        _httpClient.Dispose();
        await OrderBookStore.DisposeAsync();
    }

    public async ValueTask InitializeAsync()
    {
        await BinancePublicClient.StartAsync(CancellationToken.None);
        await OrderBookStore.StartAsync(CancellationToken.None);
        await TradesStore.StartAsync(CancellationToken.None);
    }
}

[CollectionDefinition(BINANCE_LIVE_COLLECTION)]
public class BinanceLiveCollection : ICollectionFixture<BinanceLiveContextFixture>
{
    public const string BINANCE_LIVE_COLLECTION = "BINANCE_LIVE_COLLECTION";
}
