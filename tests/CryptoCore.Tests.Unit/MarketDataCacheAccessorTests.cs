using System.IO.Compression;
using CryptoCore.Primitives;
using CryptoCore.Storage;
using CryptoCore.Storage.Extensions;
using CryptoCore.Storage.Models;
using CryptoCore.Storage.Models.Enums;
using Shouldly;
using StorageVersion = CryptoCore.Storage.Models.Version;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// End-to-end tests for MarketDataCacheAccessor<PackedMarketData24> (write + read).
/// </summary>
public class MarketDataCacheAccessorTests
{
    private static MarketDataCacheAccessor<PackedMarketData24> CreateWriter(
        string directory,
        MarketDataHash hash,
        CompressionType compression,
        CompressionLevel level)
    {
        return new MarketDataCacheAccessor<PackedMarketData24>(directory, hash, compression, level);
    }

    private static MarketDataCacheAccessor<PackedMarketData24> CreateReader(
        string directory,
        MarketDataHash hash)
    {
        return new MarketDataCacheAccessor<PackedMarketData24>(directory, hash);
    }

    [Fact]
    public void WriteRead_GZip_Roundtrip()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "CryptoCore.Storage.Tests.Cache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpRoot);

        Symbol symbol = default;
        var date = new DateOnly(2025, 1, 1);
        var hash = new MarketDataHash(symbol, date, FeedType.Combined);

        // Prepare some domain events
        var tradeTs = new DateTimeOffset(2025, 1, 1, 12, 0, 0, TimeSpan.Zero);
        var l2Ts = new DateTimeOffset(2025, 1, 1, 12, 0, 1, TimeSpan.Zero);

        var trade = new Trade(tradeTs, Side.Buy, 1000.123456789m, 0.987654321m);
        var l2 = new LevelUpdate(l2Ts, Side.Sell, 999.999999999m, 1.000000001m, true);

        var packedTrade = trade.ToStorage();
        var packedL2 = l2.ToStorage();

        // Write
        using (var writer = CreateWriter(tmpRoot, hash, CompressionType.GZip, CompressionLevel.Fastest))
        {
            writer.Write(packedTrade);
            writer.Write(packedL2);
        }

        // Read
        using var reader = CreateReader(tmpRoot, hash);

        reader.IsEmpty.ShouldBeFalse();
        reader.ItemsCount.ShouldBe(2);
        reader.Meta.Hash.ShouldBe(hash);
        reader.Meta.ItemsCount.ShouldBe(2);
        reader.Meta.CompressionType.ShouldBe(CompressionType.GZip);
        reader.Meta.Version.ShouldBe(StorageVersion.Create(1, 0, 0));

        var all = reader.ReadAll().ToArray();
        all.Length.ShouldBe(2);

        var restoredTrade = all[0].ToTrade(date);
        var restoredL2 = all[1].ToLevelUpdate(date);

        restoredTrade.ShouldBe(trade);
        restoredL2.ShouldBe(l2);
    }

    [Fact]
    public void EmptyFile_ReadAll_ReturnsEmptySequence()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "CryptoCore.Storage.Tests.Cache.Empty", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpRoot);

        Symbol symbol = default;
        var date = new DateOnly(2025, 2, 1);
        var hash = new MarketDataHash(symbol, date, FeedType.Combined);

        // Сразу создаём reader, файла ещё нет => IsEmpty = true
        using var reader = CreateReader(tmpRoot, hash);

        reader.IsEmpty.ShouldBeTrue();
        reader.ItemsCount.ShouldBe(0);

        var all = reader.ReadAll().ToArray();
        all.Length.ShouldBe(0);
    }
}
