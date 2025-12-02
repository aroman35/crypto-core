using System.IO.Compression;
using K4os.Compression.LZ4;
using K4os.Compression.LZ4.Streams;

namespace CryptoCore.Storage.Extensions;

/// <summary>
/// Helper to create LZ4 encoder/decoder streams so they fit into our
/// CompressionType + CompressionLevel abstraction.
/// </summary>
internal static class Lz4StreamFactory
{
    /// <summary>
    /// Creates LZ4 encoder stream wrapping <paramref name="inner"/>.
    /// </summary>
    public static Stream CreateEncoder(Stream inner, CompressionLevel level, bool leaveOpen)
    {
        var lz4Level = level switch
        {
            CompressionLevel.NoCompression => LZ4Level.L00_FAST,
            CompressionLevel.Fastest => LZ4Level.L00_FAST,
            CompressionLevel.Optimal => LZ4Level.L09_HC,
            CompressionLevel.SmallestSize => LZ4Level.L12_MAX,
            _ => LZ4Level.L00_FAST,
        };

        const int extraMemory = 0;

        return LZ4Stream.Encode(
            inner,
            lz4Level,
            extraMemory: extraMemory,
            leaveOpen: leaveOpen);
    }

    /// <summary>
    /// Creates LZ4 decoder stream wrapping <paramref name="inner"/>.
    /// </summary>
    public static Stream CreateDecoder(Stream inner, bool leaveOpen)
    {
        const int extraMemory = 0;

        return LZ4Stream.Decode(
            inner,
            extraMemory: extraMemory,
            leaveOpen: leaveOpen);
    }
}
