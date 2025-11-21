using CryptoConnector.Binance.Hosting;
using CryptoCore.Extensions;
using CryptoCore.Primitives;
using CryptoCore.Tests.EndToEnd.Fixtures;
using Serilog;
using Shouldly;

namespace CryptoCore.Tests.EndToEnd.Cases.MarketData;

[Collection(BinanceLiveCollection.BINANCE_LIVE_COLLECTION)]
public sealed class OrderBookSubscription
{
    private readonly OrderBookStore _orderBookStore;
    private readonly ILogger _logger;

    public OrderBookSubscription(BinanceLiveContextFixture binanceLiveContextFixture)
    {
        _orderBookStore = binanceLiveContextFixture.OrderBookStore;
        _logger = binanceLiveContextFixture.Logger.ForContext<OrderBookSubscription>();
    }

    [Theory(
        DisplayName = "Connect to exchange and build an order book",
        Timeout = 60_000
        )]
    [InlineData("BTCUSDT", 20)]
    [InlineData("ETHUSDT", 20)]
    public async Task BinanceOrderBookSubscriptionTest(string symbolStr, int updatesLimit)
    {
        var completionSource = new TaskCompletionSource();
        var totalUpdates = 0;
        var symbol = Symbol.Parse(symbolStr).For(Exchange.BinanceFutures);
        var orderBook = await _orderBookStore.GetOrCreateAsync(symbol, CancellationToken.None);

        using var bookSubscription = orderBook.OnTopUpdated(update =>
        {
            totalUpdates++;
            var (bidPx, bidQty) = update.BestBid();
            var (askPx, askQty) = update.BestAsk();
            _logger.Information(
                "Top updated for {Symbol}: Best bid: {BestBidPx}x{BestBidQty} | Best ask: {BestAskPx}x{BestAskQty}",
                update.Symbol,
                bidPx,
                bidQty,
                askPx,
                askQty);

            bidPx.IsLower(askPx).ShouldBeTrue("Best bid price should be lower than best ask price");
            bidQty.IsEquals(0.0D).ShouldBeFalse("Best bid quantity should not be zero");
            askQty.IsEquals(0.0D).ShouldBeFalse("Best ask quantity should not be zero");
            bidPx.IsEquals(0.0D).ShouldBeFalse("Best bid price should not be zero");
            askPx.IsEquals(0.0D).ShouldBeFalse("Best ask price should not be zero");

            if (totalUpdates >= updatesLimit)
                completionSource.SetResult();
        });

        await completionSource.Task;
        totalUpdates.ShouldBeGreaterThan(0);
    }
}
