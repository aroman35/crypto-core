using System;
using System.IO.Compression;
using System.IO.MemoryMappedFiles;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CryptoCore.Storage.Extensions;
using CryptoCore.Storage.Models;
using CryptoCore.Storage.Models.Enums;
using Version = CryptoCore.Storage.Models.Version;

namespace CryptoCore.Storage;

/// <summary>
/// Streaming reader/writer for market data cache files in the unified format:
/// [MarketDataCacheMeta (uncompressed)] + [sequence of T (optionally compressed)].
/// </summary>
public sealed unsafe class MarketDataCacheAccessor<T> : IDisposable
    where T : unmanaged
{
    private static readonly TypeMetadata _metadata = ResolveMetadata();

    static MarketDataCacheAccessor()
    {
        ValidateType();
    }

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
    /// Number of <see cref="T"/> records stored in the file.
    /// </summary>
    public long ItemsCount => _itemsCount;

    private static int MetaSize => sizeof(MarketDataCacheMeta);
    private static int ItemSize => sizeof(T);

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

        var expectedFeed = _metadata.FeedType;
        if (hash.Feed is not FeedType.Unknown && hash.Feed != expectedFeed)
            throw new ArgumentException(
                $"Hash feed mismatch. Expected {expectedFeed}, got {hash.Feed}.");

        hash = new MarketDataHash(hash.Symbol, hash.Date, expectedFeed);

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
            version: _metadata.Version);
    }

    /// <summary>
    /// Internal constructor for read operations.
    /// Opens an existing file, reads the header and prepares the decompression stream.
    /// </summary>
    public MarketDataCacheAccessor(string? directory, MarketDataHash hash)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);

        var expectedFeed = _metadata.FeedType;
        if (hash.Feed is not FeedType.Unknown && hash.Feed != expectedFeed)
            throw new ArgumentException(
                $"Hash feed mismatch. Expected {expectedFeed}, got {hash.Feed}.");

        hash = new MarketDataHash(hash.Symbol, hash.Date, expectedFeed);

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

        if (Meta.Hash.Feed != expectedFeed)
            throw new ArgumentException(
                $"Invalid market data file. Expected feed {expectedFeed}, found {Meta.Hash.Feed}.");

        if (!Meta.Version.IsCompatible(_metadata.Version))
            throw new ArgumentException(
                $"Incompatible market data version. Expected {_metadata.Version}, found {Meta.Version}.");

        _itemsCount = Meta.ItemsCount;
        _isEmpty = _itemsCount == 0;

        ResetReader();
    }

    #endregion

    #region Write API

    /// <summary>
    /// Writes a single market data record to the underlying stream.
    /// </summary>
    /// <param name="item">Packed record to write.</param>
    public void Write(T item)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor<T>));

        if (_isReader)
            throw new InvalidOperationException("Cannot write using a reader instance.");

        ObjectDisposedException.ThrowIf(_compressionStream is null, typeof(Stream));

        WriteInternal(_compressionStream, item);
        checked
        {
            _itemsCount++;
        }
    }

    /// <summary>
    /// Writes a batch of market data records to the underlying stream.
    /// </summary>
    /// <param name="items">Records to write.</param>
    public void Write(ReadOnlySpan<T> items)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor<T>));

        if (_isReader)
            throw new InvalidOperationException("Cannot write using a reader instance.");

        if (items.IsEmpty)
            return;

        ObjectDisposedException.ThrowIf(_compressionStream is null, typeof(Stream));

        var bytes = MemoryMarshal.AsBytes(items);
        _compressionStream.Write(bytes);

        checked
        {
            _itemsCount += items.Length;
        }
    }

    #endregion

    #region Read API

    /// <summary>
    /// Reads all records sequentially from the beginning (or current position)
    /// until the end of the file.
    /// </summary>
    /// <returns>Sequence of <see cref="T"/> records.</returns>
    public IEnumerable<T> ReadAll()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor<T>));

        if (_isEmpty)
            yield break;

        while (TryReadNext(out var item))
            yield return item;
    }

    /// <summary>
    /// Reads up to <paramref name="destination"/> length items into the provided buffer.
    /// </summary>
    /// <param name="destination">Destination buffer.</param>
    /// <returns>Number of items read.</returns>
    public int Read(Span<T> destination)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor<T>));

        if (!_isReader)
            throw new InvalidOperationException("Cannot read using a writer instance.");

        if (_isEmpty || destination.IsEmpty)
            return 0;

        var remaining = _itemsCount - _readerPosition;
        if (remaining <= 0)
            return 0;

        var toRead = (int)Math.Min(remaining, destination.Length);
        if (toRead <= 0)
            return 0;

        var slice = destination[..toRead];

        if (_isReader &&
            Meta.CompressionType == CompressionType.NoCompression &&
            _mmfAccessor is not null &&
            _mmfPointerAcquired)
        {
            return ReadFromMmf(slice);
        }

        return ReadFromStream(slice);
    }

    /// <summary>
    /// Reads a single item from the current position.
    /// </summary>
    /// <param name="item">Read item.</param>
    /// <returns>True if item was read; false if at end.</returns>
    public bool TryReadNext(out T item)
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor<T>));

        if (!_isReader)
            throw new InvalidOperationException("Cannot read using a writer instance.");

        item = default;

        if (_isEmpty || _readerPosition >= _itemsCount)
            return false;

        if (_isReader &&
            Meta.CompressionType == CompressionType.NoCompression &&
            _mmfAccessor is not null &&
            _mmfPointerAcquired)
        {
            return TryReadNextMmf(out item);
        }

        return TryReadNextStream(out item);
    }

    /// <summary>
    /// Resets reader state and reinitializes the decompression stream
    /// to start reading from the beginning of the data section.
    /// </summary>
    public void ResetReader()
    {
        ObjectDisposedException.ThrowIf(_disposed, nameof(MarketDataCacheAccessor<T>));

        if (!_isReader)
            throw new InvalidOperationException("ResetReader is only valid for reader instances.");

        if (_sourceFileStream?.CanSeek != true)
            throw new InvalidOperationException("Source data file is not seekable.");

        // Для NoCompression используем MMF, для остальных – обычный Stream + декомпрессор.
        if (Meta.CompressionType == CompressionType.NoCompression)
        {
            if (ResetMmfReader())
            {
                _readerPosition = 0;
                return;
            }

            _sourceFileStream.Seek(MetaSize, SeekOrigin.Begin);
            _compressionStream = _sourceFileStream;
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
    /// Инициализирует MemoryMappedFile для чтения массива T при NoCompression.
    /// </summary>
    private bool ResetMmfReader()
    {
        if (_sourceFileStream is null)
            throw new InvalidOperationException("Source stream is null for MMF reader.");

        // Освобождаем предыдущий MMF, если был
        ReleaseMmf();

        try
        {
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

            return true;
        }
        catch
        {
            ReleaseMmf();
            return false;
        }
    }

    #endregion

    #region Low-level IO helpers

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void WriteInternal<TItem>(Stream stream, TItem data)
        where TItem : unmanaged
    {
        var size = sizeof(TItem);
        Span<byte> buffer = stackalloc byte[size];
        MemoryMarshal.Write(buffer, in data);
        stream.Write(buffer);
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool ReadSingleItem<TItem>(Stream stream, out TItem data)
        where TItem : unmanaged
    {
        var size = sizeof(TItem);
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

        data = MemoryMarshal.Read<TItem>(buffer);
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
            if (!_isReader && _sourceFileStream is not null)
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
    private bool TryReadNextMmf(out T item)
    {
        item = default;

        if (!_mmfPointerAcquired || _mmfAccessor is null)
            return false;

        if (_readerPosition >= _itemsCount)
            return false;

        var offsetBytes = _readerPosition * ItemSize;
        var itemPtr = (T*)(_mmfBasePtr + offsetBytes);

        item = *itemPtr;
        _readerPosition++;
        return true;
    }

    private bool TryReadNextStream(out T item)
    {
        item = default;

        ObjectDisposedException.ThrowIf(_compressionStream is null, typeof(Stream));

        if (!ReadSingleItem<T>(_compressionStream, out item))
            return false;

        _readerPosition++;
        return true;
    }

    private int ReadFromStream(Span<T> destination)
    {
        ObjectDisposedException.ThrowIf(_compressionStream is null, typeof(Stream));

        var bytes = MemoryMarshal.AsBytes(destination);
        var totalRead = 0;

        while (totalRead < bytes.Length)
        {
            var read = _compressionStream.Read(bytes[totalRead..]);
            if (read == 0)
                break;
            totalRead += read;
        }

        if (totalRead == 0)
            return 0;

        if (totalRead % ItemSize != 0)
            throw new InvalidOperationException("Unexpected end of stream while reading market data.");

        var itemsRead = totalRead / ItemSize;
        _readerPosition += itemsRead;
        return itemsRead;
    }

    private int ReadFromMmf(Span<T> destination)
    {
        var remaining = _itemsCount - _readerPosition;
        if (remaining <= 0)
            return 0;

        var toRead = (int)Math.Min(remaining, destination.Length);
        if (toRead <= 0)
            return 0;

        var offsetBytes = _readerPosition * ItemSize;
        var bytesToCopy = (long)toRead * ItemSize;

        fixed (T* destPtr = destination)
        {
            Buffer.MemoryCopy(
                _mmfBasePtr + offsetBytes,
                destPtr,
                (long)destination.Length * ItemSize,
                bytesToCopy);
        }

        _readerPosition += toRead;
        return toRead;
    }

    private static TypeMetadata ResolveMetadata()
    {
        var attr = typeof(T).GetCustomAttribute<FeedTypeAttribute>();
        if (attr is null)
            throw new InvalidOperationException(
                $"Missing {nameof(FeedTypeAttribute)} on type '{typeof(T).Name}'.");

        if (attr.FeedType is FeedType.Unknown)
            throw new InvalidOperationException(
                $"{nameof(FeedTypeAttribute)} on '{typeof(T).Name}' cannot be {FeedType.Unknown}.");

        return new TypeMetadata(attr.FeedType, attr.Version);
    }

    private static void ValidateType()
    {
        if (RuntimeHelpers.IsReferenceOrContainsReferences<T>())
            throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' must not contain reference fields.");

        var layout = typeof(T).StructLayoutAttribute;
        if (layout is null ||
            (layout.Value != LayoutKind.Sequential && layout.Value != LayoutKind.Explicit))
        {
            throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' must use {nameof(StructLayoutAttribute)} with sequential or explicit layout.");
        }

        if (layout.Value == LayoutKind.Sequential && layout.Pack != 1)
        {
            throw new InvalidOperationException(
                $"Type '{typeof(T).Name}' must specify [StructLayout(LayoutKind.Sequential, Pack = 1)].");
        }
    }

    private readonly struct TypeMetadata
    {
        public TypeMetadata(FeedType feedType, Version version)
        {
            FeedType = feedType;
            Version = version;
        }

        public FeedType FeedType { get; }

        public Version Version { get; }
    }
}
