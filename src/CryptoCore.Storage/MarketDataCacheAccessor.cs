using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CryptoCore.Storage.Extensions;
using CryptoCore.Storage.Models;
using CryptoCore.Storage.Models.Enums;
using Version = CryptoCore.Storage.Models.Version;

namespace CryptoCore.Storage;

/// <summary>
/// Streaming reader/writer for market data cache files in the unified format:
/// [MarketDataCacheMeta (uncompressed)] + [sequence of PackedMarketData24 (optionally compressed)].
/// </summary>
public sealed unsafe class MarketDataCacheAccessor : IDisposable
{
    private readonly FileStream? _sourceFileStream;
    private readonly bool _isReader;
    private readonly bool _isEmpty;

    private Stream? _compressionStream;
    private long _itemsCount;
    private long _readerPosition;
    private bool _disposed;

    // MMF-only fields (NoCompression read-path)
    private MemoryMappedFile? _mmf;
    private MemoryMappedViewAccessor? _mmfAccessor;
    private byte* _mmfBasePtr;
    private bool _mmfPointerAcquired;

    /// <summary>
    /// Logical identity of this stream (symbol, date, feed).
    /// </summary>
    public MarketDataHash Hash { get; }

    /// <summary>
    /// File-level metadata stored in the header.
    /// </summary>
    public MarketDataCacheMeta Meta { get; private set; }

    /// <summary>
    /// True if the file does not contain any data records.
    /// </summary>
    public bool IsEmpty => _isEmpty;

    /// <summary>
    /// Number of <see cref="PackedMarketData24"/> records stored in the file.
    /// </summary>
    public long ItemsCount => _itemsCount;

    private static int MetaSize => sizeof(MarketDataCacheMeta);
    private static int ItemSize => sizeof(PackedMarketData24);

    #region Constructors

    /// <summary>
    /// Internal constructor for write operations.
    /// Creates (or overwrites) a file for the specified hash and prepares the
    /// compression stream starting after the header region.
    /// </summary>
    public MarketDataCacheAccessor(
        string? directory,
        MarketDataHash hash,
        CompressionType compressionType,
        CompressionLevel compressionLevel)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _isReader = false;
        Hash = hash;

        var filePath = hash.FilePath(directory);
        if (File.Exists(filePath))
            File.Delete(filePath);

        var options = new FileStreamOptions
        {
            Access = FileAccess.Write,
            BufferSize = 1024 * ItemSize,
            Mode = FileMode.CreateNew,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan
        };

        _sourceFileStream = new FileStream(filePath, options);

        // reserve space for header (will be written in Dispose)
        _sourceFileStream.Seek(MetaSize, SeekOrigin.Begin);

        _compressionStream = compressionType switch
        {
            CompressionType.NoCompression => _sourceFileStream,
            CompressionType.Brotli => new BrotliStream(_sourceFileStream, compressionLevel, leaveOpen: true),
            CompressionType.GZip => new GZipStream(_sourceFileStream, compressionLevel, leaveOpen: true),
            CompressionType.Deflate => new DeflateStream(_sourceFileStream, compressionLevel, leaveOpen: true),
            CompressionType.Lz4 => Lz4StreamFactory.CreateEncoder(_sourceFileStream, compressionLevel, leaveOpen: true),
            _ => throw new NotSupportedException($"Compression type '{compressionType}' is not supported.")
        };

        Meta = new MarketDataCacheMeta(
            hash,
            compressionType,
            compressionLevel,
            itemsCount: 0,
            buildTimeUtc: DateTime.UtcNow,
            version: Version.Create(1, 0, 0));
    }

    /// <summary>
    /// Internal constructor for read operations.
    /// Opens an existing file, reads the header and prepares the decompression stream.
    /// </summary>
    public MarketDataCacheAccessor(string? directory, MarketDataHash hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        _isReader = true;
        Hash = hash;

        var filePath = hash.FilePath(directory);
        if (!File.Exists(filePath))
        {
            _isEmpty = true;
            Meta = default;
            return;
        }

        var options = new FileStreamOptions
        {
            Access = FileAccess.Read,
            BufferSize = 1024 * ItemSize,
            Mode = FileMode.Open,
            Share = FileShare.Read,
            Options = FileOptions.SequentialScan
        };

        _sourceFileStream = File.Open(filePath, options);

        Meta = ExtractMeta(_sourceFileStream);
        if (!Meta.Hash.Equals(hash))
            throw new ArgumentException(
                $"Invalid market data file. Expected {hash}, found {Meta.Hash}.");

        _itemsCount = Meta.ItemsCount;
        _isEmpty = _itemsCount == 0;

        ResetReader();
    }

    #endregion

    #region Write API

    /// <summary>
    /// Writes a single packed market data record to the underlying stream.
    /// </summary>
    /// <param name="item">Packed record to write.</param>
    public void Write(PackedMarketData24 item)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor));

        if (_isReader)
            throw new InvalidOperationException("Cannot write using a reader instance.");

        ObjectDisposedException.ThrowIf(_compressionStream is null, typeof(Stream));

        WriteInternal(_compressionStream, item);
        _itemsCount++;
    }

    #endregion

    #region Read API

    /// <summary>
    /// Reads all records sequentially from the beginning (or current position)
    /// until the end of the file.
    /// </summary>
    /// <returns>Sequence of <see cref="PackedMarketData24"/> records.</returns>
    public IEnumerable<PackedMarketData24> ReadAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor));

        if (_isEmpty)
            yield break;

        // MMF-оптимизированный путь только для NoCompression
        if (_isReader &&
            Meta.CompressionType == CompressionType.NoCompression &&
            _mmfAccessor is not null &&
            _mmfPointerAcquired)
        {
            while (TryReadNextMmf(out var item))
            {
                yield return item;
            }

            yield break;
        }

        // Стриминговый путь (gzip/brotli/deflate/lz4)
        while (_readerPosition < _itemsCount)
        {
            if (!ReadSingleItem<PackedMarketData24>(_compressionStream!, out var item))
                yield break;

            _readerPosition++;
            yield return item;
        }
    }

    /// <summary>
    /// Resets reader state and reinitializes the decompression stream
    /// to start reading from the beginning of the data section.
    /// </summary>
    public void ResetReader()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor));

        if (!_isReader)
            throw new InvalidOperationException("ResetReader is only valid for reader instances.");

        if (_sourceFileStream?.CanSeek != true)
            throw new InvalidOperationException("Source data file is not seekable.");

        // Для NoCompression используем MMF, для остальных – обычный Stream + декомпрессор.
        if (Meta.CompressionType == CompressionType.NoCompression)
        {
            ResetMmfReader();
            _readerPosition = 0;
            return;
        }

        // reinitialize compressed stream after header
        if (Meta.CompressionType is not CompressionType.NoCompression)
            _compressionStream?.Dispose();

        _sourceFileStream.Seek(MetaSize, SeekOrigin.Begin);

        _compressionStream = Meta.CompressionType switch
        {
            CompressionType.NoCompression => _sourceFileStream,
            CompressionType.Brotli => new BrotliStream(_sourceFileStream, CompressionMode.Decompress, leaveOpen: true),
            CompressionType.GZip => new GZipStream(_sourceFileStream, CompressionMode.Decompress, leaveOpen: true),
            CompressionType.Deflate => new DeflateStream(_sourceFileStream, CompressionMode.Decompress, leaveOpen: true),
            CompressionType.Lz4 => Lz4StreamFactory.CreateDecoder(_sourceFileStream, leaveOpen: true),
            _ => throw new NotSupportedException($"Compression type '{Meta.CompressionType}' is not supported.")
        };

        _readerPosition = 0;
    }

    /// <summary>
    /// Инициализирует MemoryMappedFile для чтения массива PackedMarketData24 при NoCompression.
    /// </summary>
    private void ResetMmfReader()
    {
        if (_sourceFileStream is null)
            throw new InvalidOperationException("Source stream is null for MMF reader.");

        // Освобождаем предыдущий MMF, если был
        ReleaseMmf();

        // Создаём MMF поверх существующего файлового потока
        _mmf = MemoryMappedFile.CreateFromFile(
            _sourceFileStream,
            mapName: null,
            capacity: 0,
            access: MemoryMappedFileAccess.Read,
            inheritability: HandleInheritability.None,
            leaveOpen: true);

        var dataOffset = (long)MetaSize;
        var dataLength = _itemsCount <= 0
            ? 0
            : checked((long)_itemsCount * ItemSize);

        _mmfAccessor = _mmf.CreateViewAccessor(
            offset: dataOffset,
            size: dataLength,
            access: MemoryMappedFileAccess.Read);

        // Получаем базовый указатель на окно
        var handle = _mmfAccessor.SafeMemoryMappedViewHandle;

        byte* ptr = null;
        handle.AcquirePointer(ref ptr);
        _mmfPointerAcquired = true;

        // PointerOffset учитывает смещение, с которым открыт view
        _mmfBasePtr = ptr + _mmfAccessor.PointerOffset;

        // Поток декомпрессии нам не нужен в режиме MMF
        _compressionStream?.Dispose();
        _compressionStream = null;
    }

    #endregion

    #region Low-level IO helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInternal<T>(Stream stream, T data)
        where T : unmanaged
    {
        var size = sizeof(T);
        Span<byte> buffer = stackalloc byte[size];
        MemoryMarshal.Write(buffer, in data);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ReadSingleItem<T>(Stream stream, out T data)
        where T : unmanaged
    {
        var size = sizeof(T);
        Span<byte> buffer = stackalloc byte[size];
        data = default;

        var read = stream.Read(buffer);
        if (read == 0)
            return false;

        while (read < size)
        {
            var extra = stream.Read(buffer[read..]);
            if (extra == 0)
                throw new InvalidOperationException("Unexpected end of stream while reading market data.");
            read += extra;
        }

        data = MemoryMarshal.Read<T>(buffer);
        return true;
    }

    private static MarketDataCacheMeta ExtractMeta(Stream stream)
    {
        stream.Seek(0, SeekOrigin.Begin);

        if (!ReadSingleItem<MarketDataCacheMeta>(stream, out var meta))
            throw new InvalidOperationException(
                $"Invalid market data file. Expected {nameof(MarketDataCacheMeta)} header.");

        return meta;
    }

    private void WriteMeta()
    {
        if (_sourceFileStream is null)
            return;

        var meta = new MarketDataCacheMeta(
            hash: Hash,
            compressionType: Meta.CompressionType,
            compressionLevel: Meta.CompressionLevel,
            itemsCount: _itemsCount,
            buildTimeUtc: DateTime.UtcNow,
            version: Meta.Version);

        Meta = meta;

        _sourceFileStream.Seek(0, SeekOrigin.Begin);
        WriteInternal(_sourceFileStream, meta);
    }

    #endregion

    #region MMF cleanup

    private void ReleaseMmf()
    {
        if (_mmfAccessor is not null)
        {
            if (_mmfPointerAcquired)
            {
                _mmfAccessor.SafeMemoryMappedViewHandle.ReleasePointer();
                _mmfPointerAcquired = false;
            }

            _mmfAccessor.Dispose();
            _mmfAccessor = null;
        }

        _mmf?.Dispose();
        _mmf = null;
        _mmfBasePtr = null;
    }

    #endregion

    #region Dispose

    public void Dispose()
    {
        if (_disposed)
            return;

        try
        {
            // MMF-путь: сначала отпускаем mmap-ресурсы
            ReleaseMmf();

            // Flush compressed data first (если есть отдельный поток)
            if (_compressionStream is not null && _compressionStream != _sourceFileStream)
                _compressionStream.Dispose();

            // On writer: finalize header with actual ItemsCount
            if (!_isReader && !_isEmpty && _sourceFileStream is not null)
            {
                WriteMeta();
            }

            _sourceFileStream?.Dispose();
        }
        finally
        {
            _disposed = true;
        }
    }

    #endregion

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool TryReadNextMmf(out PackedMarketData24 item)
    {
        item = default;

        if (!_mmfPointerAcquired || _mmfAccessor is null)
            return false;

        if (_readerPosition >= _itemsCount)
            return false;

        var offsetBytes = _readerPosition * ItemSize;
        var itemPtr = (PackedMarketData24*)(_mmfBasePtr + offsetBytes);

        item = *itemPtr;
        _readerPosition++;
        return true;
    }
}
