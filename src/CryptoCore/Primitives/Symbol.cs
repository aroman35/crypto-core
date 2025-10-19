using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CryptoCore.Extensions;

namespace CryptoCore.Primitives;

/// <summary>
/// Trading symbol: <see cref="BaseAsset"/> + <see cref="QuoteAsset"/> + <see cref="Exchange"/>.
/// Parses common exchange-native forms (Binance/OKX/KuCoin/Bybit-like) and a generic "BASE-QUOTE@Preset".
/// Emits exchange-native string in <see cref="ToString"/> when <see cref="Exchange"/> is set; otherwise "BASE-QUOTE".
/// Supports delimiter-less tickers (e.g., "BTCUSDT") via a stable-coin suffix registry.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public struct Symbol : IEquatable<Symbol>, IEquatable<string>, IComparable<Symbol>
{
    /// <summary>Maximum allowed length for base and quote asset (ASCII only).</summary>
    public const int MaxAssetLength = Asset.MaxLength;

    private Asset _base;
    private Asset _quote;
    private Exchange _exchange;

    private static readonly ConcurrentDictionary<Symbol, string> SymbolToString = new();
    private static readonly ConcurrentDictionary<string, Symbol> StringToSymbol = new(StringComparer.Ordinal);

    // Stable-coin registry (UPPER ASCII keys). Used for suffix-splitting "BASE{STABLE}".
    private static readonly HashSet<string> StableCoins = new(StringComparer.Ordinal)
    {
        "USDT", "USDC", "BUSD", "TUSD", "USDP", "DAI", "FDUSD", "USD"
    };

    /// <summary>Adds a stable-coin to the suffix registry at runtime (case-insensitive for ASCII).</summary>
    public static void AddStablecoin(string coin)
    {
        if (string.IsNullOrWhiteSpace(coin))
            return;
        if (!Asset.TryParse(coin.AsSpan(), out var a))
            return;
        StableCoins.Add(a.ToString());
    }

    /// <summary>Adds a stable-coin to the suffix registry at runtime.</summary>
    public static void AddStablecoin(Asset coin) => StableCoins.Add(coin.ToString());

    /// <summary>Base asset (left part).</summary>
    public readonly Asset BaseAsset => _base;

    /// <summary>Quote asset (right part).</summary>
    public readonly Asset QuoteAsset => _quote;

    /// <summary>Exchange flags (can be <see cref="Exchange.None"/>).</summary>
    public readonly Exchange Exchange => _exchange;

    /// <summary>Create from components.</summary>
    public static Symbol Create(Asset baseAsset, Asset quoteAsset, Exchange exchange)
        => new Symbol { _base = baseAsset, _quote = quoteAsset, _exchange = exchange };

    /// <summary>Rebinds exchange preset for the same asset pair.</summary>
    public readonly Symbol For(Exchange exchange) =>
        new Symbol { _base = _base, _quote = _quote, _exchange = exchange };

    /// <summary>
    /// Returns an exchange-native string for a known exchange (e.g., Binance: "BTCUSDT"; OKX swap: "BTC-USDT-SWAP"),
    /// otherwise "BASE-QUOTE".
    /// </summary>
    public override readonly string ToString()
    {
        if (SymbolToString.TryGetValue(this, out var cached))
            return cached;

        var s = FormatNative();
        return SymbolToString.GetOrAdd(this, s);
    }

    /// <summary>
    /// Parses common forms:
    /// - "BASE-QUOTE@Preset" (explicit preset), "BASE/QUOTE@Preset", "BASE_QUOTE@Preset";
    /// - OKX-style: "BASE-QUOTE", "BASE-QUOTE-SWAP", "BASE-USD-YYYYMMDD";
    /// - Binance/Bybit/KuCoin-style delimiter-less: "BASEUSDT", "ETHUSDC" (split via stable-coin suffix);
    /// - Generic: "BASE-QUOTE" / "BASE/QUOTE" / "BASE_QUOTE".
    /// </summary>
    public static Symbol Parse(string s)
    {
        if (TryParse(s.AsSpan(), out var sym))
            return sym;
        throw new FormatException("Invalid symbol format.");
    }

    /// <summary>Tries to parse the symbol from a span using the rules described in <see cref="Parse(string)"/>.</summary>
    public static bool TryParse(ReadOnlySpan<char> text, out Symbol symbol)
    {
        symbol = default;
        if (text.IsEmpty)
            return false;

        // 1) Explicit preset "BASE-QUOTE@Preset"
        var at = text.LastIndexOf('@');
        if (at >= 0)
        {
            if (!TryParsePresetName(text[(at + 1)..], out var ex))
                return false;
            if (!TryParseBaseQuote(text[..at], out var ba, out var qa))
                return false;
            symbol = new Symbol { _base = ba, _quote = qa, _exchange = ex };
            return true;
        }

        // 2) OKX-like forms: "BASE-QUOTE[-SWAP]" or "BASE-USD-YYYYMMDD"
        if (TryParseOkxLike(text, out var okxSym))
        {
            symbol = okxSym;
            return true;
        }

        // 3) Delimiter-less with stable-coin suffix: "BASE{STABLE}"
        if (TrySplitByStableSuffix(text, out var baseSpan, out var quoteSpan))
        {
            if (!Asset.TryParse(baseSpan, out var ba))
                return false;
            if (!Asset.TryParse(quoteSpan, out var qa))
                return false;

            // Default to Binance spot for delimiter-less symbols so round-trips preserve native form.
            // (Binance/Bybit используют слитный формат; при необходимости можно сделать это настраиваемым.)
            symbol = new Symbol { _base = ba, _quote = qa, _exchange = Exchange.Binance | Exchange.Spot };
            return true;
        }

        // 4) Generic "BASE[ -/_ ]QUOTE"
        if (TryParseBaseQuote(text, out var b, out var q))
        {
            symbol = new Symbol { _base = b, _quote = q, _exchange = Exchange.None };
            return true;
        }

        return false;
    }

    /// <summary>Gets or adds from cache (parsing first if required).</summary>
    public static Symbol Get(ReadOnlySpan<char> value)
    {
        if (!TryParse(value, out var s))
            throw new FormatException("Invalid symbol format.");
        return StringToSymbol.GetOrAdd(s.ToString(), _ => s);
    }

    /// <summary>Lexicographic compare: base, quote, then normalized preset name.</summary>
    public readonly int CompareTo(Symbol other)
    {
        var c = CompareAssets(_base, other._base);
        if (c != 0)
            return c;
        c = CompareAssets(_quote, other._quote);
        if (c != 0)
            return c;

        var hasA = TryGetPresetName(_exchange, out var pa);
        var hasB = TryGetPresetName(other._exchange, out var pb);
        if (hasA && hasB)
            return string.CompareOrdinal(pa, pb);
        if (hasA)
            return 1;
        if (hasB)
            return -1;
        return 0;
    }

    /// <summary>Determines value equality with another <see cref="Symbol"/> (components and exchange flags).</summary>
    public readonly bool Equals(Symbol other)
        => _exchange == other._exchange && _base.Equals(other._base) && _quote.Equals(other._quote);

    /// <summary>Determines equality with a string by parsing its canonical form.</summary>
    public readonly bool Equals(string? s)
        => !string.IsNullOrEmpty(s) && TryParse(s.AsSpan(), out var sym) && Equals(sym);

    /// <summary>Determines equality with an arbitrary object.</summary>
    public override readonly bool Equals(object? obj) => obj is Symbol s && Equals(s);

    /// <summary>Returns a hash code based on components.</summary>
    public override readonly int GetHashCode()
    {
        unchecked
        {
            var h = 17;
            h = (h * 31) + _base.GetHashCode();
            h = (h * 31) + _quote.GetHashCode();
            h = (h * 31) + _exchange.GetHashCode();
            return h;
        }
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Symbol left, Symbol right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Symbol left, Symbol right) => !left.Equals(right);

    /// <summary>Equality against a string.</summary>
    public static bool operator ==(Symbol left, string right) => left.Equals(right);

    /// <summary>Inequality against a string.</summary>
    public static bool operator !=(Symbol left, string right) => !left.Equals(right);

    /// <summary>Returns <c>true</c> if <paramref name="left"/> is less than <paramref name="right"/> (lexicographic).</summary>
    public static bool operator <(Symbol left, Symbol right) => left.CompareTo(right) < 0;

    /// <summary>Returns <c>true</c> if <paramref name="left"/> is greater than <paramref name="right"/>.</summary>
    public static bool operator >(Symbol left, Symbol right) => left.CompareTo(right) > 0;

    /// <summary>Returns <c>true</c> if <paramref name="left"/> is less than or equal to <paramref name="right"/>.</summary>
    public static bool operator <=(Symbol left, Symbol right) => left.CompareTo(right) <= 0;

    /// <summary>Returns <c>true</c> if <paramref name="left"/> is greater than or equal to <paramref name="right"/>.</summary>
    public static bool operator >=(Symbol left, Symbol right) => left.CompareTo(right) >= 0;

    // ===================== native formatting =====================

    /// <summary>
    /// Measures the native (exchange-specific) string length for this symbol.
    /// </summary>
    private readonly bool MeasureNative(out int length)
    {
        var bl = _base.AsciiBytes.Length;
        var ql = _quote.AsciiBytes.Length;
        if (bl <= 0 || ql <= 0)
        {
            length = 0;
            return false;
        }

        // Exchange-specific
        if (TryGetPresetName(_exchange, out _))
        {
            // Binance/Bybit: "BASEQUOTE"
            if (_exchange.IsBinance() || _exchange.IsBybit())
            {
                length = bl + ql;
                return true;
            }

            // OKX spot: "BASE-QUOTE"
            if (_exchange.IsOKX() && _exchange.IsSpot())
            {
                length = bl + 1 + ql;
                return true;
            }

            // OKX perp/swap: "BASE-QUOTE-SWAP"
            if (_exchange.IsOKX() && (_exchange.IsPerpetual() || _exchange.IsSwap()))
            {
                length = bl + 1 + ql + 1 + 4; // "-SWAP"
                return true;
            }

            // KuCoin spot: "BASE-QUOTE"
            if (_exchange.IsKuCoin() && _exchange.IsSpot())
            {
                length = bl + 1 + ql;
                return true;
            }
        }

        // Default generic: "BASE-QUOTE"
        length = bl + 1 + ql;
        return true;
    }

    /// <summary>
    /// Formats this symbol to its native (exchange) string. First call allocates and is cached.
    /// </summary>
    private readonly string FormatNative()
    {
        if (!MeasureNative(out var len))
            return string.Empty;

        // Pass the whole struct as state (no single-element tuples)
        return string.Create(len, this, static (dst, self) =>
        {
            var pos = 0;

            // BASE
            var b = self._base.AsciiBytes;
            for (var i = 0; i < b.Length; i++)
                dst[pos++] = (char)b[i];

            // Separator policy
            var needSep =
                !TryGetPresetName(self._exchange, out _) // generic
                || (self._exchange.IsOKX() && (self._exchange.IsSpot() || self._exchange.IsPerpetual() || self._exchange.IsSwap()))
                || (self._exchange.IsKuCoin() && self._exchange.IsSpot());

            if (needSep && !(self._exchange.IsBinance() || self._exchange.IsBybit()))
                dst[pos++] = '-';

            // QUOTE
            var q = self._quote.AsciiBytes;
            for (var i = 0; i < q.Length; i++)
                dst[pos++] = (char)q[i];

            // OKX "-SWAP" suffix
            if (self._exchange.IsOKX() && (self._exchange.IsPerpetual() || self._exchange.IsSwap()))
            {
                dst[pos++] = '-';
                dst[pos++] = 'S';
                dst[pos++] = 'W';
                dst[pos++] = 'A';
                dst[pos] = 'P';
            }
        });
    }

    /// <summary>
    /// Formats into caller-provided buffer (native form) without allocations.
    /// </summary>
    public readonly bool TryFormat(Span<char> destination, out int written)
    {
        if (!MeasureNative(out var count) || destination.Length < count)
        {
            written = 0;
            return false;
        }

        WriteNative(destination);
        written = count;
        return true;
    }

    /// <summary>
    /// Writes native (exchange) form into the given span. Assumes the span is large enough.
    /// </summary>
    private readonly void WriteNative(Span<char> dst)
    {
        var pos = 0;

        // BASE
        var b = _base.AsciiBytes;
        for (var i = 0; i < b.Length; i++)
            dst[pos++] = (char)b[i];

        var needSep =
            !TryGetPresetName(_exchange, out _)
            || (_exchange.IsOKX() && (_exchange.IsSpot() || _exchange.IsPerpetual() || _exchange.IsSwap()))
            || (_exchange.IsKuCoin() && _exchange.IsSpot());

        if (needSep && !(_exchange.IsBinance() || _exchange.IsBybit()))
            dst[pos++] = '-';

        // QUOTE
        var q = _quote.AsciiBytes;
        for (var i = 0; i < q.Length; i++)
            dst[pos++] = (char)q[i];

        // OKX "-SWAP" suffix
        if (_exchange.IsOKX() && (_exchange.IsPerpetual() || _exchange.IsSwap()))
        {
            dst[pos++] = '-';
            dst[pos++] = 'S';
            dst[pos++] = 'W';
            dst[pos++] = 'A';
            dst[pos] = 'P';
        }
    }

    // ===================== parsing helpers =====================

    private static bool TryParseBaseQuote(ReadOnlySpan<char> span, out Asset @base, out Asset quote)
    {
        var sep = IndexOfAny(span, '-', '/', '_');
        if (sep <= 0 || sep >= span.Length - 1)
        {
            @base = default;
            quote = default;
            return false;
        }

        quote = default;
        return Asset.TryParse(span[..sep], out @base) && Asset.TryParse(span[(sep + 1)..], out quote);
    }

    // OKX-like: "BASE-QUOTE" or "BASE-QUOTE-SWAP" or "BASE-USD-YYYYMMDD"
    private static bool TryParseOkxLike(ReadOnlySpan<char> span, out Symbol sym)
    {
        sym = default;
        var first = span.IndexOf('-');
        if (first <= 0 || first >= span.Length - 1)
            return false;

        var second = span.Slice(first + 1).IndexOf('-');
        if (second < 0)
        {
            // "BASE-QUOTE" → spot
            if (!Asset.TryParse(span[..first], out var ba))
                return false;
            if (!Asset.TryParse(span[(first + 1)..], out var qa))
                return false;
            sym = new Symbol { _base = ba, _quote = qa, _exchange = Exchange.OKX | Exchange.Spot };
            return true;
        }

        second += first + 1;
        // "BASE-QUOTE-SUFFIX"
        var baseSpan = span[..first];
        var quoteSpan = span.Slice(first + 1, second - first - 1);
        var suffix = span[(second + 1)..];

        if (!Asset.TryParse(baseSpan, out var b))
            return false;
        if (!Asset.TryParse(quoteSpan, out var q))
            return false;

        // Detect "SWAP"
        if (suffix.Equals("SWAP".AsSpan(), StringComparison.OrdinalIgnoreCase))
        {
            sym = new Symbol
            {
                _base = b, _quote = q,
                _exchange = Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined
            };
            return true;
        }

        // Detect date-like YYYYMMDD for delivery futures
        if (suffix.Length == 8 && IsAllDigits(suffix))
        {
            sym = new Symbol
            {
                _base = b, _quote = Asset.Parse("USD"),
                _exchange = Exchange.OKX | Exchange.Futures | Exchange.Delivery | Exchange.UsdMargined
            };
            return true;
        }

        return false;
    }

    // Delimiter-less BASE{STABLE} → split by the longest matching stable-coin suffix
    private static bool TrySplitByStableSuffix(ReadOnlySpan<char> s, out ReadOnlySpan<char> basePart, out ReadOnlySpan<char> quotePart)
    {
        basePart = default;
        quotePart = default;

        // NEW: if a delimiter exists, this is not a delimiter-less ticker → bail out
        if (IndexOfAny(s, '-', '/', '_') >= 0)
            return false;

        if (s.Length < 4)
            return false;

        // Iterate over stable set; prefer the longest match
        ReadOnlySpan<char> best = default;
        foreach (var st in StableCoins)
        {
            var t = st.AsSpan(); // already upper
            if (t.Length >= s.Length)
                continue;

            if (s.EndsWith(t, StringComparison.OrdinalIgnoreCase))
            {
                if (t.Length > best.Length)
                    best = t;
            }
        }

        if (best.IsEmpty)
            return false;

        basePart = s[..(s.Length - best.Length)];
        quotePart = s[(s.Length - best.Length)..];
        return basePart.Length > 0;
    }

    // ===================== presets =====================

    private static bool TryGetPresetName(Exchange x, out string name)
    {
        if (x == (Exchange.Binance | Exchange.Spot))
        {
            name = "BinanceSpot";
            return true;
        }

        if (x == (Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            name = "BinanceFutures";
            return true;
        }

        if (x == (Exchange.OKX | Exchange.Spot))
        {
            name = "OKXSpot";
            return true;
        }

        if (x == (Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            name = "OKXFutures";
            return true;
        }

        if (x == (Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined))
        {
            name = "OKXSwap";
            return true;
        }

        if (x == (Exchange.KuCoin | Exchange.Spot))
        {
            name = "KuCoinSpot";
            return true;
        }

        if (x == (Exchange.KuCoin | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            name = "KuCoinFutures";
            return true;
        }

        if (x == (Exchange.Bybit | Exchange.Spot))
        {
            name = "BybitSpot";
            return true;
        }

        if (x == (Exchange.Bybit | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            name = "BybitFutures";
            return true;
        }

        if (x == (Exchange.Bitget | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined))
        {
            name = "BitgetFutures";
            return true;
        }

        name = string.Empty;
        return false;
    }

    private static bool TryParsePresetName(ReadOnlySpan<char> name, out Exchange x)
    {
        if (name.SequenceEqual("BinanceSpot"))
        {
            x = Exchange.Binance | Exchange.Spot;
            return true;
        }

        if (name.SequenceEqual("BinanceFutures"))
        {
            x = Exchange.Binance | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (name.SequenceEqual("OKXSpot"))
        {
            x = Exchange.OKX | Exchange.Spot;
            return true;
        }

        if (name.SequenceEqual("OKXFutures"))
        {
            x = Exchange.OKX | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (name.SequenceEqual("OKXSwap"))
        {
            x = Exchange.OKX | Exchange.Swap | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (name.SequenceEqual("KuCoinSpot"))
        {
            x = Exchange.KuCoin | Exchange.Spot;
            return true;
        }

        if (name.SequenceEqual("KuCoinFutures"))
        {
            x = Exchange.KuCoin | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (name.SequenceEqual("BybitSpot"))
        {
            x = Exchange.Bybit | Exchange.Spot;
            return true;
        }

        if (name.SequenceEqual("BybitFutures"))
        {
            x = Exchange.Bybit | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        if (name.SequenceEqual("BitgetFutures"))
        {
            x = Exchange.Bitget | Exchange.Futures | Exchange.Perpetual | Exchange.UsdMargined;
            return true;
        }

        x = Exchange.None;
        return false;
    }

    // ===================== utilities =====================

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int IndexOfAny(ReadOnlySpan<char> span, char c1, char c2, char c3)
    {
        for (var i = 0; i < span.Length; i++)
        {
            var c = span[i];
            if (c == c1 || c == c2 || c == c3)
                return i;
        }

        return -1;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsAllDigits(ReadOnlySpan<char> s)
    {
        for (var i = 0; i < s.Length; i++)
        {
            var c = s[i];
            if (c < '0' || c > '9')
                return false;
        }

        return true;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int CompareAssets(Asset a, Asset b)
    {
        var sa = a.AsciiBytes;
        var sb = b.AsciiBytes;
        var n = Math.Min(sa.Length, sb.Length);
        for (var i = 0; i < n; i++)
        {
            var d = sa[i] - sb[i];
            if (d != 0)
                return d;
        }

        return sa.Length - sb.Length;
    }
}
