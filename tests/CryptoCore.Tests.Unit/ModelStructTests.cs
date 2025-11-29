using System.Runtime.InteropServices;
using CryptoCore.Primitives;
using CryptoCore.Storage.Models;
using CryptoCore.Storage.Models.Enums;
using CryptoCore.Storage.Models.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// Tests for hash and packed record structs.
/// </summary>
public class ModelStructTests
{
    [Fact]
    public void PackedMarketData24_HasCorrectSize()
    {
        var size = Marshal.SizeOf<PackedMarketData24>();
        size.ShouldBe(24);
    }

    [Fact]
    public void PackedMarketData24_StoresValues()
    {
        var price = new Decimal9(123.456789012m);
        var qty = new Decimal9(0.000000001m);
        const int timeMs = 123456;
        const int flags = 0x12;

        var p = new PackedMarketData24(timeMs, price, qty, flags);

        p.TimeMs.ShouldBe(timeMs);
        p.Price.ToDecimal().ShouldBe(price.ToDecimal());
        p.Quantity.ToDecimal().ShouldBe(qty.ToDecimal());
        p.Flags.ShouldBe(flags);
    }

    [Fact]
    public void MarketDataHash_FilePath_CreatesDirectories()
    {
        // Symbol из CryptoCore.Primitives; default достаточно, нам важен сам путь
        Symbol symbol = default;
        var date = new DateOnly(2025, 1, 2);
        var hash = new MarketDataHash(symbol, date, FeedType.Trades);

        var root = Path.Combine(Path.GetTempPath(), "CryptoCore.Storage.Tests.Hash", Guid.NewGuid().ToString("N"));
        var path = hash.FilePath(root);

        Directory.Exists(Path.GetDirectoryName(path)!).ShouldBeTrue();
        path.EndsWith(".dat", StringComparison.OrdinalIgnoreCase).ShouldBeTrue();
    }
}