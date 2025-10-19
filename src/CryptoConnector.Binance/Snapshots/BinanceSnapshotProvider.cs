using System.Buffers.Text;
using System.Text.Json;
using CryptoConnector.Binance.Common;
using CryptoCore.Extensions;
using CryptoCore.MarketData;
using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Snapshots;

/// <summary>
/// Fetches order book snapshots from Binance REST (spot / USD-M futures / COIN-M futures) and converts to L2 snapshot.
/// Chooses endpoint based on <see cref="Exchange"/> flags.
/// </summary>
public sealed class BinanceSnapshotProvider : ISnapshotProvider
{
    private readonly HttpClient _http;

    public BinanceSnapshotProvider(HttpClient http) => _http = http ?? throw new ArgumentNullException(nameof(http));

    /// <inheritdoc />
    public async Task<L2Update> GetOrderBookSnapshotAsync(Symbol symbol, int limit, CancellationToken ct = default)
    {
        var url = BuildDepthUrl(symbol, limit);
        using var resp = await _http.GetAsync(url, ct).ConfigureAwait(false);
        resp.EnsureSuccessStatusCode();
        var json = await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);

        ulong lastUpdateId = 0;
        var deltas = new System.Collections.Generic.List<L2Delta>(limit * 2);

        var reader = new Utf8JsonReader(json, isFinalBlock: true, state: default);
        while (reader.Read())
        {
            if (reader.TokenType == JsonTokenType.PropertyName)
            {
                var name = reader.ValueSpan;
                reader.Read();

                if (name.SequenceEqual("lastUpdateId"u8))
                {
                    if (reader.TokenType == JsonTokenType.Number)
                        reader.TryGetUInt64(out lastUpdateId);
                    else if (reader.TokenType == JsonTokenType.String)
                        Utf8Parser.TryParse(reader.ValueSpan, out lastUpdateId, out _);
                }
                else if (name.SequenceEqual("bids"u8) || name.SequenceEqual("asks"u8))
                {
                    var side = name[0] == (byte)'b' ? Side.Buy : Side.Sell;
                    if (reader.TokenType != JsonTokenType.StartArray)
                        continue;

                    while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                    {
                        if (reader.TokenType != JsonTokenType.StartArray)
                            break;

                        double price = 0, qty = 0;

                        reader.Read(); // price
                        if (reader.TokenType == JsonTokenType.String)
                            Utf8Parser.TryParse(reader.ValueSpan, out price, out _);
                        else if (reader.TokenType == JsonTokenType.Number)
                            reader.TryGetDouble(out price);

                        reader.Read(); // qty
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            Utf8Parser.TryParse(reader.ValueSpan, out qty, out _);
                        }
                        else if (reader.TokenType == JsonTokenType.Number)
                            reader.TryGetDouble(out qty);

                        reader.Read(); // EndArray

                        if (!qty.IsEquals(0))
                            deltas.Add(new L2Delta(side, price, qty));
                    }
                }
            }
        }

        var arr = deltas.ToArray();
        // E = 0 (не знаем), Snapshot=true, ids = lastUpdateId
        return new L2Update(symbol, 0, isSnapshot: true, firstUpdateId: 0, lastUpdateId: lastUpdateId, prevLastUpdateId: 0, deltas: arr);
    }

    private static string BuildDepthUrl(Symbol s, int limit)
    {
        // Выбираем базовый REST в зависимости от пресета (Spot / Futures-USD-M / Futures-COIN-M)
        string baseUrl;
        if (s.Exchange.IsBinance() && s.Exchange.IsSpot())
            baseUrl = "https://api.binance.com/api/v3/depth";
        else if (s.Exchange.IsBinance() && s.Exchange.IsUsdMargined())
            baseUrl = "https://fapi.binance.com/fapi/v1/depth";
        else if (s.Exchange.IsBinance() && s.Exchange.IsCoinMargined())
            baseUrl = "https://dapi.binance.com/dapi/v1/depth";
        else
            baseUrl = "https://api.binance.com/api/v3/depth"; // дефолт

        return $"{baseUrl}?symbol={s.ToString()}&limit={limit}";
    }
}
