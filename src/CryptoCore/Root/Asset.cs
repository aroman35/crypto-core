using System.Collections.Concurrent;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CryptoCore.Root;

/// <summary>
/// Compact, fixed-size ASCII asset identifier.
/// - Max length: 11 characters (ASCII only), normalized to upper-case.
/// - Layout: fixed 12-byte ASCII buffer + 1-byte length.
/// - Parsing and equality are allocation-free; ToString() is cached.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public unsafe struct Asset : IEquatable<Asset>, IEquatable<string>, IComparable<Asset>
{
    /// <summary>
    /// Maximum allowed length of an asset identifier, in characters.
    /// </summary>
    public const int MAX_LENGTH = 11;

    private fixed byte _bytes[12]; // ASCII storage (upper-case)
    private byte _len; // 0..11

    private static readonly ConcurrentDictionary<Asset, string> AssetToString = new();
    private static readonly ConcurrentDictionary<string, Asset> StringToAsset = new(StringComparer.Ordinal);

    /// <summary>Common predefined assets.</summary>
    public static readonly Asset USDT = Parse("USDT");

    /// <summary>USD fiat ticker.</summary>
    public static readonly Asset USD = Parse("USD");

    /// <summary>Bitcoin ticker.</summary>
    public static readonly Asset BTC = Parse("BTC");

    /// <summary>Ethereum ticker.</summary>
    public static readonly Asset ETH = Parse("ETH");

    /// <summary>BNB ticker.</summary>
    public static readonly Asset BNB = Parse("BNB");

    static Asset()
    {
        // Warm reverse-cache for most common symbols.
        StringToAsset["USDT"] = USDT;
        StringToAsset["USD"] = USD;
        StringToAsset["BTC"] = BTC;
        StringToAsset["ETH"] = ETH;
        StringToAsset["BNB"] = BNB;
    }

    /// <summary>
    /// Gets the length (characters/bytes) of this asset identifier.
    /// </summary>
    public readonly int Length => _len;

    /// <summary>
    /// Returns the ASCII bytes backing this asset (no trailing zeros).
    /// </summary>
    public readonly ReadOnlySpan<byte> AsciiBytes
    {
        get
        {
            ref var first = ref Unsafe.AsRef(in _bytes[0]);
            return MemoryMarshal.CreateReadOnlySpan(ref first, _len);
        }
    }

    /// <summary>
    /// Converts to string. First call allocates, subsequent calls are served from an internal cache.
    /// </summary>
    public override readonly string ToString()
    {
        if (AssetToString.TryGetValue(this, out var s))
            return s;

        var created = string.Create(_len, this, static (dst, a) =>
        {
            var src = a.AsciiBytes;
            for (var i = 0; i < src.Length; i++)
                dst[i] = (char)src[i]; // ASCII → UTF-16
        });

        return AssetToString.GetOrAdd(this, created);
    }

    /// <summary>
    /// Parses from string (ASCII only). Normalizes to upper-case. Throws on invalid input.
    /// </summary>
    public static Asset Parse(string s)
    {
        if (TryParse(s.AsSpan(), out var a))
            return a;
        throw new FormatException("Invalid ASCII asset or length exceeded.");
    }

    /// <summary>
    /// Tries to parse from a char span (ASCII only), normalizing to upper-case.
    /// </summary>
    public static bool TryParse(ReadOnlySpan<char> chars, out Asset asset)
    {
        if (chars.Length == 0 || chars.Length > MAX_LENGTH)
        {
            asset = default;
            return false;
        }

        Asset a = default;
        a._len = (byte)chars.Length;

        for (var i = 0; i < chars.Length; i++)
        {
            var ch = chars[i];
            if (ch > 0x7F)
            {
                asset = default;
                return false;
            } // non-ASCII

            var b = (byte)ch;
            if (b >= (byte)'a' && b <= (byte)'z')
                b = (byte)(b - 32);

            if (!IsAllowedAscii(b))
            {
                asset = default;
                return false;
            }

            a._bytes[i] = b;
        }

        asset = a;
        return true;
    }

    /// <summary>
    /// Parses from ASCII bytes. Normalizes letters to upper-case. Throws on invalid input.
    /// </summary>
    public static Asset FromAscii(ReadOnlySpan<byte> ascii)
    {
        if (TryFromAscii(ascii, out var a))
            return a;
        throw new FormatException("Invalid ASCII asset or length exceeded.");
    }

    /// <summary>
    /// Tries to parse from ASCII bytes. Normalizes letters to upper-case.
    /// </summary>
    public static bool TryFromAscii(ReadOnlySpan<byte> ascii, out Asset asset)
    {
        if (ascii.Length == 0 || ascii.Length > MAX_LENGTH)
        {
            asset = default;
            return false;
        }

        Asset a = default;
        a._len = (byte)ascii.Length;

        for (var i = 0; i < ascii.Length; i++)
        {
            var b = ascii[i];
            if (b >= 0x80)
            {
                asset = default;
                return false;
            } // non-ASCII

            if (b >= (byte)'a' && b <= (byte)'z')
                b = (byte)(b - 32);
            if (!IsAllowedAscii(b))
            {
                asset = default;
                return false;
            }

            a._bytes[i] = b;
        }

        asset = a;
        return true;
    }

    /// <summary>
    /// Compares assets lexicographically by their ASCII bytes.
    /// </summary>
    public readonly int CompareTo(Asset other)
    {
        var a = AsciiBytes;
        var b = other.AsciiBytes;
        var n = Math.Min(a.Length, b.Length);
        for (var i = 0; i < n; i++)
        {
            var d = a[i] - b[i];
            if (d != 0)
                return d;
        }

        return a.Length - b.Length;
    }

    /// <summary>
    /// Value equality by length and bytes.
    /// </summary>
    public readonly bool Equals(Asset other)
    {
        if (_len != other._len)
            return false;
        for (var i = 0; i < _len; i++)
            if (_bytes[i] != other._bytes[i])
                return false;
        return true;
    }

    /// <summary>
    /// Equality against a string (parsed and normalized).
    /// </summary>
    public readonly bool Equals(string? s)
        => !string.IsNullOrEmpty(s) && TryParse(s.AsSpan(), out var a) && Equals(a);

    /// <summary>Object equality.</summary>
    public override readonly bool Equals(object? obj) => obj is Asset a && Equals(a);

    /// <summary>Stable hash over up to 11 ASCII bytes (FNV-1a).</summary>
    public override readonly int GetHashCode()
    {
        const uint prime = 16777619;
        var h = 2166136261;
        for (var i = 0; i < _len; i++)
        {
            h ^= _bytes[i];
            h *= prime;
        }

        return unchecked((int)h);
    }

    /// <summary>Equality operator.</summary>
    public static bool operator ==(Asset left, Asset right) => left.Equals(right);

    /// <summary>Inequality operator.</summary>
    public static bool operator !=(Asset left, Asset right) => !left.Equals(right);

    /// <summary>Equality vs string.</summary>
    public static bool operator ==(Asset left, string right) => left.Equals(right);

    /// <summary>Inequality vs string.</summary>
    public static bool operator !=(Asset left, string right) => !left.Equals(right);

    /// <summary>
    /// Returns a by-value copy of this asset.
    /// </summary>
    public readonly Asset Clone()
    {
        Asset a = default;
        a._len = _len;
        for (var i = 0; i < _len; i++)
            a._bytes[i] = _bytes[i];
        return a;
    }

    /// <summary>
    /// Returns true if the byte is allowed in an asset identifier.
    /// Allowed: A-Z, 0-9, '_', '-', '.', '/'.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsAllowedAscii(byte b)
        => (b >= (byte)'A' && b <= (byte)'Z')
           || (b >= (byte)'0' && b <= (byte)'9')
           || b is (byte)'_' or (byte)'-' or (byte)'.' or (byte)'/';
}
