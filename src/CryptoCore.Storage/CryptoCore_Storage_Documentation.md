# CryptoCore.Storage Documentation

## Overview
CryptoCore.Storage provides a binary, high-throughput format for archived market data. The storage layer is optimized for:

- Compact, deterministic records
- Fast sequential reads/writes
- Efficient replay for analytics and backtesting

The system stores a small, fixed-width struct per record (typically `PackedMarketData24`) followed by optional compression.

## File Format
Each file is a single stream:

```
[MarketDataCacheMeta (uncompressed)] + [sequence of T (optionally compressed)]
```

- `MarketDataCacheMeta` is written at the start of the file in raw binary form.
- The data section is a contiguous sequence of `T` records.
- Compression is applied only to the data section.

## Core Concepts

### MarketDataHash
`MarketDataHash` defines file identity (Symbol, Date, Feed). It also controls the on-disk file path.

### FeedTypeAttribute
All storage record types must be annotated with `FeedTypeAttribute`. The attribute defines:

- `FeedType` (used for file naming and validation)
- `Version` (used for compatibility checks)

Example:

```csharp
[FeedType(FeedType.Combined, 1, 0, 0)]
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct PackedMarketData24 { ... }
```

### Versioning
`MarketDataCacheAccessor<T>` validates format compatibility using `CryptoCore.Storage.Models.Version`:

- The file header stores `Meta.Version` (from the `FeedTypeAttribute`).
- On read, the accessor checks `Meta.Version.IsCompatible(attributeVersion)`.
- Compatibility is defined as matching Major and Minor.

## MarketDataCacheAccessor<T>
A generic streaming reader/writer for binary cache files.

### Type Requirements
`T` must satisfy:

- `unmanaged`
- No reference fields (`RuntimeHelpers.IsReferenceOrContainsReferences<T>() == false`)
- `[StructLayout(LayoutKind.Sequential, Pack = 1)]` or explicit layout
- `[FeedType(...)]` attribute with non-`Unknown` feed and a version

### Constructors
- Writer:
  ```csharp
  var writer = new MarketDataCacheAccessor<T>(directory, hash, compressionType, compressionLevel);
  ```

- Reader:
  ```csharp
  var reader = new MarketDataCacheAccessor<T>(directory, hash);
  ```

If `hash.Feed` is `Unknown`, it is set to the feed from `FeedTypeAttribute`.
If `hash.Feed` is specified and does not match the attribute, an exception is thrown.

### Write API
- `Write(T item)`
- `Write(ReadOnlySpan<T> items)`

### Read API
- `IEnumerable<T> ReadAll()`
- `bool TryReadNext(out T item)`
- `int Read(Span<T> destination)`
- `ResetReader()`

### NoCompression + Memory Mapping
When `CompressionType.NoCompression` is used, the reader attempts to use a memory-mapped view for fast access. If MMF setup fails, it transparently falls back to streaming reads.

## Usage Examples

### PackedMarketData24 (combined feed)
```csharp
var hash = new MarketDataHash(symbol, date, FeedType.Combined);

using (var writer = new MarketDataCacheAccessor<PackedMarketData24>(root, hash, CompressionType.GZip, CompressionLevel.Fastest))
{
    writer.Write(packedRecord);
}

using var reader = new MarketDataCacheAccessor<PackedMarketData24>(root, hash);
foreach (var packed in reader.ReadAll())
{
    if (packed.IsTrade())
        ProcessTrade(packed.ToTrade(hash.Date));
    else
        ProcessUpdate(packed.ToLevelUpdate(hash.Date));
}
```

### Trade feed
```csharp
var hash = new MarketDataHash(symbol, date, FeedType.Trades);

using (var writer = new MarketDataCacheAccessor<Trade>(root, hash, CompressionType.Lz4, CompressionLevel.Fastest))
{
    writer.Write(tradesSpan);
}

using var reader = new MarketDataCacheAccessor<Trade>(root, hash);
var buffer = new Trade[1024];
while (true)
{
    var read = reader.Read(buffer);
    if (read == 0)
        break;

    for (var i = 0; i < read; i++)
        ProcessTrade(buffer[i]);
}
```

### LevelUpdate feed
```csharp
var hash = new MarketDataHash(symbol, date, FeedType.LevelUpdates);

using var reader = new MarketDataCacheAccessor<LevelUpdate>(root, hash);
while (reader.TryReadNext(out var update))
    ProcessUpdate(update);
```

## MarketDataCacheReplayer
`MarketDataCacheReplayer` replays packed data into an `OrderBookL2` and forwards events to `IMarketDataListener`.

```csharp
var replayer = new MarketDataCacheReplayer(root, hash, listener, rateMs: 100);
replayer.Run();
```

## Operational Notes
- File paths are derived from `MarketDataHash` and `FeedType`.
- Version incompatibility or feed mismatch results in a clear exception on read.
- For best performance, use `Write(ReadOnlySpan<T>)` and `Read(Span<T>)` in batch mode.

## Performance (Indicative)
Performance depends on hardware, compression, and record size. The format is designed for fast sequential I/O, and memory-mapped reads are used for uncompressed files when available.