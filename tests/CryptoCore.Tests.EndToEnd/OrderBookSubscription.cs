using System.Globalization;
using CryptoConnector.Binance.Common;
using CryptoConnector.Binance.Hosting;
using CryptoConnector.Binance.Snapshots;
using CryptoConnector.Binance.Transport;
using CryptoCore.Primitives;
using CryptoStreaming;
using Serilog;
using Serilog.Events;
using Xunit.Abstractions;

namespace CryptoCore.Tests.EndToEnd;

public class OrderBookSubscription(ITestOutputHelper testOutputHelper)
{
    private readonly ILogger _logger = testOutputHelper.CreateTestLogger(
        LogEventLevel.Debug,
        formatProvider: CultureInfo.InvariantCulture);

    [Fact(
        DisplayName = "Connect to exchange and build an order book",
        Timeout = 60_000
        )]
    public async Task BinanceOrderBookSubscriptionTest()
    {
        using var cancellationTokenSource = new CancellationTokenSource();
        cancellationTokenSource.CancelAfter(TimeSpan.FromSeconds(59));
        var hits = 0;
        try
        {
            await using var transport = new ChannelMarketDataTransport();
            await using var client = new BinancePublicClient(new SimpleSymbolProvider());
            var symbol = Symbol.Parse("BTCUSDT").For(Exchange.BinanceFutures);
            await client.StartAsync(
                Exchange.BinanceFutures,
                [BinanceStreams.Depth(symbol.ToString().ToLowerInvariant())],
                transport,
                cancellationTokenSource.Token);
            using var http = new HttpClient();
            var snapshots = new BinanceSnapshotProvider(http);

            await using var store = new OrderBookStore(client, transport, snapshots,
                new OrderBookStoreOptions { SnapshotLimit = 1000 });
            await store.StartAsync(cancellationTokenSource.Token);
            var orderBook = await store.GetOrCreateAsync(symbol, CancellationToken.None);
            ArgumentNullException.ThrowIfNull(orderBook);

            using var bookSubscription = orderBook.OnTopUpdated(update =>
            {
                hits++;
                var (bidPx, bidQty) = update.BestBid();
                var (askPx, askQty) = update.BestAsk();
                _logger.Information(
                    "Top updated: Best bid: {BestBidPx}x{BestBidQty} | Best ask: {BestAskPx}x{BestAskQty}",
                    bidPx,
                    bidQty,
                    askPx,
                    askQty);
            });
            await Task.Delay(TimeSpan.FromSeconds(60), cancellationTokenSource.Token);
        }
        catch (TaskCanceledException)
        {
            _logger.Information("Finished by timeout with {HitsCount} updates", hits);
        }
    }
}
