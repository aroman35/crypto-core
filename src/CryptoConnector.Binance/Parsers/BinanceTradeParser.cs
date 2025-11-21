using System.Buffers.Text;
using System.Text.Json;
using CryptoConnector.Binance.Common;
using CryptoCore.MarketData;
using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Parsers;

/// <summary>
/// Zero-allocation parser for Binance per-trade stream (@trade).
/// Fields: e(event), E(eventTime), s(symbol), p(price), q(quantity), T(tradeTime), m(isBuyerMaker).
/// </summary>
public static class BinanceTradeParser
{
    /// <summary>Parses @trade message into <see cref="PublicTrade"/>.</summary>
    public static bool TryParseTrade(ReadOnlySpan<byte> jsonUtf8, ISymbolProvider symbols, Exchange exchange, out PublicTrade trade)
    {
        trade = default;
        var reader = new Utf8JsonReader(jsonUtf8, isFinalBlock: true, state: default);

        long eventTime = 0, tradeTime = 0;
        ulong tradeId = 0;
        double price = 0, qty = 0;
        var isBuyerMaker = false;
        Symbol symbol = default;
        bool haveSymbol = false, haveP = false, haveQ = false;

        while (reader.Read())
        {
            if (reader.TokenType != JsonTokenType.PropertyName)
                continue;

            var name = reader.ValueSpan;
            reader.Read();

            if (name.SequenceEqual("s"u8))
            {
                if (reader.TokenType == JsonTokenType.String && symbols.TryGet(reader.ValueSpan, out symbol))
                    haveSymbol = true;
                continue;
            }

            if (name.SequenceEqual("p"u8))
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    haveP = Utf8Parser.TryParse(reader.ValueSpan, out price, out _);
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    haveP = reader.TryGetDouble(out price);
                }
                continue;
            }

            if (name.SequenceEqual("q"u8))
            {
                if (reader.TokenType == JsonTokenType.String)
                {
                    haveQ = Utf8Parser.TryParse(reader.ValueSpan, out qty, out _);
                }
                else if (reader.TokenType == JsonTokenType.Number)
                {
                    haveQ = reader.TryGetDouble(out qty);
                }
                continue;
            }

            if (name.SequenceEqual("T"u8))
            {
                if (reader.TokenType == JsonTokenType.Number)
                    reader.TryGetInt64(out tradeTime);
                continue;
            }
            if (name.SequenceEqual("E"u8))
            {
                if (reader.TokenType == JsonTokenType.Number)
                    reader.TryGetInt64(out eventTime);
                continue;
            }

            // id поля разные между spot/futures. Берём "t" (trade id) если есть.
            if (name.SequenceEqual("t"u8))
            {
                if (reader.TokenType == JsonTokenType.Number)
                    reader.TryGetUInt64(out tradeId);
                else if (reader.TokenType == JsonTokenType.String)
                    _ = Utf8Parser.TryParse(reader.ValueSpan, out tradeId, out _);
                continue;
            }

            if (name.SequenceEqual("m"u8))
            {
                if (reader.TokenType == JsonTokenType.True)
                    isBuyerMaker = true;
            }
        }

        if (!haveSymbol || !haveP || !haveQ)
            return false;

        var flags = PublicTrade.TradeAttributes.None;
        // buyer-makes? В Binance "m" = isBuyerMaker (true -> buy was maker -> seller was aggressor).
        flags |= isBuyerMaker ? PublicTrade.TradeAttributes.AggressorSell : PublicTrade.TradeAttributes.AggressorBuy;

        trade = PublicTrade.Create(symbol.For(exchange), tradeId, tradeTime != 0 ? tradeTime : eventTime, price, qty, flags);
        return true;
    }
}
