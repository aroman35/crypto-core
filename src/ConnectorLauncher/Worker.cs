using CryptoConnector.Binance.Common;
using CryptoConnector.Binance.Hosting;
using CryptoConnector.Binance.Snapshots;
using CryptoConnector.Binance.Transport;
using CryptoCore.Primitives;
using CryptoStreaming;

namespace ConnectorLauncher;

public class Worker : BackgroundService
{
    private readonly ILogger<Worker> _logger;

    public Worker(ILogger<Worker> logger)
    {
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await using var transport = new ChannelMarketDataTransport();
        await using var client = new BinancePublicClient(new SimpleSymbolProvider());
        var symbol = Symbol.Parse("BTCUSDT").For(Exchange.BinanceFutures);
        await client.StartAsync(
            BinanceStreams.FuturesUsdM,
            [BinanceStreams.Depth(symbol.ToString().ToLowerInvariant())],
            transport,
            stoppingToken);
        using var http = new HttpClient();
        var snapshots = new BinanceSnapshotProvider(http);

        await using var store = new OrderBookStore(client, transport, snapshots, new OrderBookStoreOptions { SnapshotLimit = 1000 });
        await store.StartAsync(stoppingToken);
        await store.GetOrCreateAsync(symbol, CancellationToken.None);

        var orderBook = store.TryGet(symbol);
        ArgumentNullException.ThrowIfNull(orderBook);
        using var bookSubscription = orderBook.OnBookUpdated(update =>
        {
            var (bidPx, bidQty) = update.BestBid();
            var (askPx, askQty) = update.BestAsk();
            _logger.LogInformation(
                "Top updated: Best bid: {BestBidPx}x{BestBidQty} | Best ask: {BestAskPx}x{BestAskQty}",
                bidPx,
                bidQty, askPx, askQty);
        });
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(5));
        do
        {
            _logger.LogInformation("Last update id: {Li}", orderBook.LastUpdateId);
        } while (!stoppingToken.IsCancellationRequested && await timer.WaitForNextTickAsync(stoppingToken));
    }
}
