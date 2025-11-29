using System.IO.Compression;
using System.Runtime.InteropServices;
using CryptoCore.Storage.Models.Enums;

namespace CryptoCore.Storage.Models;

/// <summary>
/// File-level metadata stored at the beginning of each market data file.
/// The header is written in raw (uncompressed) form, followed by a compressed
/// sequence of data records.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MarketDataCacheMeta(
    MarketDataHash hash,
    CompressionType compressionType,
    CompressionLevel compressionLevel,
    long itemsCount,
    DateTime buildTimeUtc,
    Version version)
{
    /// <summary>
    /// Logical stream identity: symbol, date and feed (trades, L2, etc.).
    /// </summary>
    public readonly MarketDataHash Hash = hash;

    /// <summary>
    /// Compression algorithm used for the data section.
    /// </summary>
    public readonly CompressionType CompressionType = compressionType;

    /// <summary>
    /// Compression level used for the data section
    /// (for algorithms that support multiple levels).
    /// </summary>
    public readonly CompressionLevel CompressionLevel = compressionLevel;

    /// <summary>
    /// Number of records (data items) stored in the compressed section.
    /// </summary>
    public readonly long ItemsCount = itemsCount;

    /// <summary>
    /// Timestamp when this file was built (UTC).
    /// </summary>
    public readonly DateTime BuildTimeUtc = buildTimeUtc;

    /// <summary>
    /// Binary format version of this file.
    /// Used to maintain backward compatibility when the layout evolves.
    /// </summary>
    public readonly Version Version = version;
}
