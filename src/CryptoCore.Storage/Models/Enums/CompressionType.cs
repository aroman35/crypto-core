namespace CryptoCore.Storage.Models.Enums;

/// <summary>
/// Compression algorithm used for the data section of a market data file.
/// The header is always stored uncompressed.
/// </summary>
public enum CompressionType
{
    /// <summary>No compression, raw binary payload.</summary>
    NoCompression = 0,

    /// <summary>GZip compression.</summary>
    GZip = 1,

    /// <summary>Brotli compression.</summary>
    Brotli = 2,

    /// <summary>LZ4 or compatible fast compression.</summary>
    Lz4 = 3,

    /// <summary>Deflate</summary>
    Deflate = 4
}
