namespace CryptoConnector.Binance.Common;

/// <summary>
/// Known public WebSocket endpoints & helpers.
/// </summary>
public static class BinanceStreams
{
    /// <summary>Spot WS base URL.</summary>
    public const string SpotWs = "wss://stream.binance.com:9443/ws";

    /// <summary>USD-M Futures WS base URL.</summary>
    public const string FuturesUsdM = "wss://fstream.binance.com/ws";

    /// <summary>COIN-M Futures WS base URL.</summary>
    public const string FuturesCoinM = "wss://dstream.binance.com/ws";

    /// <summary>
    /// Depth stream name for symbol (lower-case): e.g. "btcusdt@depth@100ms".
    /// </summary>
    public static string Depth(string symbolLower, string interval = "100ms") => $"{symbolLower}@depth@{interval}";

    /// <summary>
    /// Trade stream name (per trade, не aggTrade): e.g. "btcusdt@trade".
    /// </summary>
    public static string Trades(string symbolLower) => $"{symbolLower}@trade";
}
