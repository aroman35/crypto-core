using System;
using System.Collections.Generic;
using System.IO.Compression;
using System.Linq;
using System.Runtime.InteropServices;
using CryptoCore.Primitives;
using CryptoCore.Storage;
using CryptoCore.Storage.Models;
using CryptoCore.Storage.Models.Enums;
using Shouldly;
using StorageVersion = CryptoCore.Storage.Models.Version;

namespace CryptoCore.Tests.Unit;

public sealed class MarketDataCacheAccessorVersionTests
{
    [Fact]
    public void WriteRead_UsesAttributeVersionInMeta()
    {
        var tmpRoot = CreateTempDirectory();
        var hash = new MarketDataHash(default(Symbol), new DateOnly(2025, 1, 2), FeedType.Unknown);

        using (var writer = new MarketDataCacheAccessor<TestPackedV1>(tmpRoot, hash, CompressionType.GZip, CompressionLevel.Fastest))
        {
            writer.Write(new TestPackedV1(1, 2));
        }

        using var reader = new MarketDataCacheAccessor<TestPackedV1>(tmpRoot, hash);

        reader.Meta.Version.ShouldBe(StorageVersion.Create(1, 0, 0));
        reader.Meta.Hash.Feed.ShouldBe(FeedType.Combined);
    }

    [Fact]
    public void Read_ThrowsOnIncompatibleVersion()
    {
        var tmpRoot = CreateTempDirectory();
        var hash = new MarketDataHash(default(Symbol), new DateOnly(2025, 1, 3), FeedType.Unknown);

        using (var writer = new MarketDataCacheAccessor<TestPackedV1>(tmpRoot, hash, CompressionType.GZip, CompressionLevel.Fastest))
        {
            writer.Write(new TestPackedV1(10, 20));
        }

        var ex = Should.Throw<ArgumentException>(() => new MarketDataCacheAccessor<TestPackedV2>(tmpRoot, hash));
        ex.Message.ShouldContain("Incompatible market data version");
    }

    [Fact]
    public void Read_BulkApi_RoundTripPreservesOrder()
    {
        var tmpRoot = CreateTempDirectory();
        var hash = new MarketDataHash(default(Symbol), new DateOnly(2025, 1, 4), FeedType.Unknown);
        var items = CreateItems(15);

        using (var writer = new MarketDataCacheAccessor<TestPackedV1>(tmpRoot, hash, CompressionType.GZip, CompressionLevel.Fastest))
        {
            writer.Write(items);
        }

        using var reader = new MarketDataCacheAccessor<TestPackedV1>(tmpRoot, hash);
        var buffer = new TestPackedV1[4];
        var readItems = new List<TestPackedV1>(items.Length);

        while (true)
        {
            var read = reader.Read(buffer);
            if (read == 0)
                break;

            for (var i = 0; i < read; i++)
                readItems.Add(buffer[i]);
        }

        readItems.Count.ShouldBe(items.Length);
        readItems.ShouldBe(items);
    }

    private static string CreateTempDirectory()
    {
        var tmpRoot = Path.Combine(Path.GetTempPath(), "CryptoCore.Storage.Tests.Cache", Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tmpRoot);
        return tmpRoot;
    }

    private static TestPackedV1[] CreateItems(int count)
    {
        var result = new TestPackedV1[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = new TestPackedV1(i, i * 10L);
        }

        return result;
    }

    [FeedType(FeedType.Combined, 1, 0, 0)]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct TestPackedV1
    {
        public TestPackedV1(int a, long b)
        {
            A = a;
            B = b;
        }

        public int A { get; }

        public long B { get; }
    }

    [FeedType(FeedType.Combined, 2, 0, 0)]
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly struct TestPackedV2
    {
        public TestPackedV2(int a, long b)
        {
            A = a;
            B = b;
        }

        public int A { get; }

        public long B { get; }
    }
}