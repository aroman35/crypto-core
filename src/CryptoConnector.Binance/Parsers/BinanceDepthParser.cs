using System.Buffers.Text;
using System.Text.Json;
using CryptoConnector.Binance.Common;
using CryptoCore.MarketData;
using CryptoCore.Primitives;

namespace CryptoConnector.Binance.Parsers;

/// <summary>
/// Zero-allocation parser for Binance depth update WS payloads (spot and futures).
/// Produces a pooled <see cref="L2UpdatePooled"/> with deltas and Binance-like ids.
/// </summary>
public static class BinanceDepthParser
{
    /// <summary>
    /// Parses a Binance "depthUpdate" JSON message into <see cref="L2UpdatePooled"/>.
    /// Expects fields: "E" (event time), "s" (symbol), "U" (firstU), "u" (lastU), optional "pu" (prevLastU),
    /// "b" (bids), "a" (asks). Each price/qty is a string; parsed with <see cref="Utf8Parser"/>.
    /// </summary>
    public static bool TryParseDepthUpdate(ReadOnlySpan<byte> jsonUtf8, ISymbolProvider symbols, Exchange exchange, out L2UpdatePooled pooled)
    {
        pooled = default!;
        var reader = new Utf8JsonReader(jsonUtf8, isFinalBlock: true, state: default);

        long eventTime = 0;
        ulong firstU = 0, lastU = 0, prevU = 0;
        var hasPrevU = false;
        Symbol symbol = default;

        L2UpdatePooled? builder = null;
        try
        {
            while (reader.Read())
            {
                if (reader.TokenType == JsonTokenType.PropertyName)
                {
                    // Property name
                    var name = reader.ValueSpan;
                    reader.Read(); // move to value

                    // E: event time
                    if (name.SequenceEqual("E"u8))
                    {
                        if (reader.TokenType == JsonTokenType.Number)
                            reader.TryGetInt64(out eventTime);
                        continue;
                    }

                    // s: symbol
                    if (name.SequenceEqual("s"u8))
                    {
                        if (reader.TokenType == JsonTokenType.String)
                        {
                            var val = reader.ValueSpan; // UTF-8
                            if (!symbols.TryGet(val, out symbol))
                                return false;
                        }
                        continue;
                    }

                    // U: first update id
                    if (name.SequenceEqual("U"u8))
                    {
                        if (reader.TokenType == JsonTokenType.Number)
                            reader.TryGetUInt64(out firstU);
                        _ = reader.TokenType == JsonTokenType.String &&
                            Utf8Parser.TryParse(reader.ValueSpan, out firstU, out _);
                        continue;
                    }

                    // u: last update id
                    if (name.SequenceEqual("u"u8))
                    {
                        if (reader.TokenType == JsonTokenType.Number)
                            reader.TryGetUInt64(out lastU);
                        _ = reader.TokenType == JsonTokenType.String &&
                            Utf8Parser.TryParse(reader.ValueSpan, out lastU, out _);
                        continue;
                    }

                    // pu: prev last update id (futures)
                    if (name.SequenceEqual("pu"u8))
                    {
                        hasPrevU = true;
                        if (reader.TokenType == JsonTokenType.Number)
                            reader.TryGetUInt64(out prevU);
                        _ = reader.TokenType == JsonTokenType.String &&
                            Utf8Parser.TryParse(reader.ValueSpan, out prevU, out _);
                        continue;
                    }

                    // b or a: deltas array
                    if (name.SequenceEqual("b"u8) || name.SequenceEqual("a"u8))
                    {
                        var side = name[0] == (byte)'b' ? Side.Buy : Side.Sell;
                        if (reader.TokenType != JsonTokenType.StartArray)
                            return false;

                        if (builder is null)
                            builder = new L2UpdatePooled(initialCapacity: 64);

                        // iterate outer array
                        while (reader.Read() && reader.TokenType != JsonTokenType.EndArray)
                        {
                            if (reader.TokenType != JsonTokenType.StartArray)
                                return false;

                            // inner array: [price, qty]
                            double price = 0, qty = 0;

                            // price
                            reader.Read();
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                if (!Utf8Parser.TryParse(reader.ValueSpan, out price, out _))
                                    return false;
                            }
                            else if (reader.TokenType == JsonTokenType.Number)
                            {
                                reader.TryGetDouble(out price);
                            }
                            else
                                return false;

                            // qty
                            reader.Read();
                            if (reader.TokenType == JsonTokenType.String)
                            {
                                if (!Utf8Parser.TryParse(reader.ValueSpan, out qty, out _))
                                    return false;
                            }
                            else if (reader.TokenType == JsonTokenType.Number)
                            {
                                reader.TryGetDouble(out qty);
                            }
                            else
                                return false;

                            // end of inner array
                            reader.Read(); // should be EndArray

                            builder.AddDelta(new L2Delta(side, price, qty));
                        }
                    }

                    // other fields are ignored
                }
            }

            if (builder is null)
                return false;

            builder.SetHeader(
                symbol.For(exchange),
                eventTime,
                isSnapshot: false,
                first: firstU,
                last: lastU,
                prev: hasPrevU ? prevU : 0
            );
            pooled = builder;
            return true;
        }
        catch
        {
            builder?.Dispose();
            return false;
        }
    }
}
