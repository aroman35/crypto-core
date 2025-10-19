using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using CryptoCore.Extensions;
using CryptoCore.Root;

namespace CryptoCore.Json.SystemTextJson;

/// <summary>
    /// System.Text.Json converter for <see cref="Exchange"/> that serializes to a short preset name
    /// (e.g., "BinanceSpot", "OKXFutures", "OKXSwap", "BybitFutures") when the flag combination is known,
    /// otherwise to a lower-case slug "venue[-market][-attrs]" (e.g., "binance-futures-perpetual-usdm").
    /// Deserializes from either a preset name or a slug. JSON null → <c>default(Exchange)</c>.
    /// </summary>
    public sealed class ExchangeJsonConverter : JsonConverter<Exchange>
    {
        /// <summary>
        /// Reads a JSON string and converts it to <see cref="Exchange"/>. Returns <c>default</c> on JSON null.
        /// Throws <see cref="JsonException"/> on invalid token type or content.
        /// </summary>
        public override Exchange Read(ref Utf8JsonReader reader, Type typeToConvert, JsonSerializerOptions options)
        {
            if (reader.TokenType == JsonTokenType.Null)
                return default;

            if (reader.TokenType != JsonTokenType.String)
                throw new JsonException("Expected string for Exchange.");

            var s = reader.GetString();
            if (string.IsNullOrWhiteSpace(s))
                return default;

            if (TryParsePreset(s, out var ex))
                return ex;

            if (TryParseSlug(s.AsSpan(), out ex))
                return ex;

            throw new JsonException($"Invalid Exchange string: '{s}'.");
        }

        /// <summary>
        /// Writes the <see cref="Exchange"/> as a JSON string, using a preset if available,
        /// otherwise using a lower-case slug.
        /// </summary>
        public override void Write(Utf8JsonWriter writer, Exchange value, JsonSerializerOptions options)
        {
            if (TryGetPreset(value, out var preset))
            {
                writer.WriteStringValue(preset);
                return;
            }

            // slug form
            var slug = BuildSlug(value);
            writer.WriteStringValue(slug);
        }

        /// <summary>Returns true when the flag combination maps to a short preset name.</summary>
        internal static bool TryGetPreset(Exchange x, out string name)
        {
            if (x == (Exchange.Binance | Exchange.Spot)) { name = "BinanceSpot";
            return true; }
            if (x == (Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined)) { name = "BinanceFutures";
            return true; }

            if (x == (Exchange.OKX | Exchange.Spot)) { name = "OKXSpot";
            return true; }
            if (x == (Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined)) { name = "OKXFutures";
            return true; }
            if (x == (Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined)) { name = "OKXSwap";
            return true; }

            if (x == (Exchange.KuCoin | Exchange.Spot)) { name = "KuCoinSpot";
            return true; }
            if (x == (Exchange.KuCoin | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined)) { name = "KuCoinFutures";
            return true; }

            if (x == (Exchange.Bybit | Exchange.Spot)) { name = "BybitSpot";
            return true; }
            if (x == (Exchange.Bybit | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined)) { name = "BybitFutures";
            return true; }

            if (x == (Exchange.Deribit | Exchange.Options)) { name = "DeribitOptions";
            return true; }

            if (x == (Exchange.Bitget | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined)) { name = "BitgetFutures";
            return true; }

            name = string.Empty;
            return false;
        }

        /// <summary>Parses a preset name like "BinanceFutures" or "OKXSwap". Case-sensitive.</summary>
        internal static bool TryParsePreset(ReadOnlySpan<char> s, out Exchange x)
        {
            if (s is "BinanceSpot") { x = Exchange.Binance | Exchange.Spot;
            return true; }
            if (s is "BinanceFutures") { x = Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true; }

            if (s is "OKXSpot") { x = Exchange.OKX | Exchange.Spot;
            return true; }
            if (s is "OKXFutures") { x = Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true; }
            if (s is "OKXSwap") { x = Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined;
            return true; }

            if (s is "KuCoinSpot") { x = Exchange.KuCoin | Exchange.Spot;
            return true; }
            if (s is "KuCoinFutures") { x = Exchange.KuCoin | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true; }

            if (s is "BybitSpot") { x = Exchange.Bybit | Exchange.Spot;
            return true; }
            if (s is "BybitFutures") { x = Exchange.Bybit | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true; }

            if (s is "DeribitOptions") { x = Exchange.Deribit | Exchange.Options;
            return true; }

            if (s is "BitgetFutures") { x = Exchange.Bitget | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true; }

            x = Exchange.None;
            return false;
        }

        /// <summary>Parses a lower-case slug "venue[-market][-attrs]". Case-insensitive.</summary>
        internal static bool TryParseSlug(ReadOnlySpan<char> s, out Exchange x)
        {
            x = Exchange.None;

            // split by '-'
            Span<Range> parts = stackalloc Range[6];
            int count = 0, start = 0;
            for (int i = 0; i <= s.Length; i++)
            {
                if (i == s.Length || s[i] == '-')
                {
                    if (i > start)
                        parts[count++] = new Range(start, i);
                    start = i + 1;
                    if (count >= parts.Length)
                        break;
                }
            }

            if (count == 0)
                return false;

            var venue = s[parts[0]];
            if (venue.Equals("binance".AsSpan(), StringComparison.OrdinalIgnoreCase))
                x |= Exchange.Binance;
            else if (venue.Equals("okx".AsSpan(), StringComparison.OrdinalIgnoreCase) || venue.Equals("okex".AsSpan(), StringComparison.OrdinalIgnoreCase))
                x |= Exchange.OKX;
            else if (venue.Equals("kucoin".AsSpan(), StringComparison.OrdinalIgnoreCase))
                x |= Exchange.KuCoin;
            else if (venue.Equals("bybit".AsSpan(), StringComparison.OrdinalIgnoreCase))
                x |= Exchange.Bybit;
            else if (venue.Equals("deribit".AsSpan(), StringComparison.OrdinalIgnoreCase))
                x |= Exchange.Deribit;
            else if (venue.Equals("bitget".AsSpan(), StringComparison.OrdinalIgnoreCase))
                x |= Exchange.Bitget;
            else
                return false;

            for (int i = 1; i < count; i++)
            {
                var t = s[parts[i]];
                if (t.Equals("spot".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.Spot;
                else if (t.Equals("futures".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.Futures;
                else if (t.Equals("options".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.Options;
                else if (t.Equals("swap".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.Swap;
                else if (t.Equals("perpetual".AsSpan(), StringComparison.OrdinalIgnoreCase) || t.Equals("perp".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.Perpetual;
                else if (t.Equals("delivery".AsSpan(), StringComparison.OrdinalIgnoreCase) || t.Equals("quarterly".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.Delivery;
                else if (t.Equals("usdm".AsSpan(), StringComparison.OrdinalIgnoreCase) || t.Equals("usd-m".AsSpan(), StringComparison.OrdinalIgnoreCase) || t.Equals("usd".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.UsdMargined;
                else if (t.Equals("coinm".AsSpan(), StringComparison.OrdinalIgnoreCase) || t.Equals("coin-m".AsSpan(), StringComparison.OrdinalIgnoreCase) || t.Equals("coin".AsSpan(), StringComparison.OrdinalIgnoreCase))
                    x |= Exchange.CoinMargined;
            }

            // ✅ Defaulting rule:
            // - If "futures" OR "swap" is present, and neither "perpetual" nor "delivery" was specified,
            //   default to Perpetual (matches OKX/Binance semantics for swaps/perps).
            if ((x.IsFutures() || x.IsSwap()) && !x.IsPerpetual() && !x.IsDelivery())
                x |= Exchange.Perpetual;

            return true;
        }

        /// <summary>Builds a lower-case slug for an exchange.</summary>
        internal static string BuildSlug(Exchange x)
        {
            var sb = new StringBuilder(48);

            var venue = x.TryGetSingleVenue();
            if (venue == Exchange.Binance)
                sb.Append("binance");
            else if (venue == Exchange.OKX)
                sb.Append("okx");
            else if (venue == Exchange.KuCoin)
                sb.Append("kucoin");
            else if (venue == Exchange.Bybit)
                sb.Append("bybit");
            else if (venue == Exchange.Deribit)
                sb.Append("deribit");
            else if (venue == Exchange.Bitget)
                sb.Append("bitget");

            // market
            if (x.IsSpot())
                sb.Append("-spot");
            else if (x.IsFutures())
                sb.Append("-futures");
            else if (x.IsSwap())
                sb.Append("-swap");
            else if (x.IsOptions())
                sb.Append("-options");

            // contract attrs
            if (x.IsPerpetual())
                sb.Append("-perpetual");
            else if (x.IsDelivery())
                sb.Append("-delivery");

            if (x.IsUsdMargined())
                sb.Append("-usdm");
            else if (x.IsCoinMargined())
                sb.Append("-coinm");

            return sb.Length == 0 ? "none" : sb.ToString();
        }
    }
