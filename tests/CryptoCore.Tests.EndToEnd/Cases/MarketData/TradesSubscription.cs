using CryptoConnector.Binance.Hosting;
using CryptoCore.Extensions;
using CryptoCore.Primitives;
using CryptoCore.Tests.EndToEnd.Fixtures;
using Serilog;
using Shouldly;

namespace CryptoCore.Tests.EndToEnd.Cases.MarketData;

[Collection(BinanceLiveCollection.BINANCE_LIVE_COLLECTION)]
public sealed class TradesSubscription
{
    private readonly TradesStore _tradesStore;
    private readonly ILogger _logger;

    public TradesSubscription(BinanceLiveContextFixture binanceLiveContextFixture)
    {
        _logger = binanceLiveContextFixture.Logger.ForContext<TradesSubscription>();
        _tradesStore = binanceLiveContextFixture.TradesStore;
    }

    [Theory(
        DisplayName = "Connect to exchange and get trades",
        Timeout = 60_000
    )]
    [InlineData("BTCUSDT", 20)]
    [InlineData("ETHUSDT", 20)]
    public async Task BinanceTradesSubscriptionTest(string symbolStr, int updatesLimit)
    {
        var completionSource = new TaskCompletionSource();
        var totalUpdates = 0;

        var symbol = Symbol.Parse(symbolStr).For(Exchange.BinanceFutures);
        using var subscription = await _tradesStore.OnTradeReceived(symbol, trade =>
        {
            totalUpdates++;
            _logger.Information("Trade received for {Symbol}: {Price}x{Quantity}", symbol, trade.Price, trade.Quantity);
            trade.Symbol.ShouldBe(symbol);
            trade.Price.IsEquals(0.0D).ShouldBeFalse("Quantity should not be zero");
            trade.Quantity.IsEquals(0.0D).ShouldBeFalse("Price should not be zero");
            if (totalUpdates >= updatesLimit)
                completionSource.SetResult();
        });

        await completionSource.Task;
        totalUpdates.ShouldBeGreaterThan(0);
    }
}
