using CryptoCore.Storage.Extensions;
using Shouldly;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// Tests for Unix timestamp & UTC helpers.
/// </summary>
public class TimestampExtensionsTests
{
    [Fact]
    public void UnixMilliseconds_Roundtrip_DateTime()
    {
        var dt = new DateTime(2025, 1, 1, 12, 34, 56, 789, DateTimeKind.Utc);

        long ts = dt.ToUnixMillisecondsTimestamp();
        var back = ts.AsUtcDateTime();

        back.ShouldBe(dt);
        back.Kind.ShouldBe(DateTimeKind.Utc);
    }

    [Fact]
    public void UnixMilliseconds_Roundtrip_DateTimeOffset()
    {
        var dto = new DateTimeOffset(2025, 1, 1, 0, 0, 0, TimeSpan.Zero);

        long ts = dto.ToUnixMillisecondsTimestamp();
        var back = ts.AsUtcDateTimeOffset();

        back.ShouldBe(dto);
    }

    [Fact]
    public void DateOnly_TimeOnly_From_DateTimeOffset_Utc()
    {
        var dto = new DateTimeOffset(2025, 3, 4, 23, 59, 59, TimeSpan.Zero);

        var d = dto.ToUtcDateOnly();
        var t = dto.ToUtcTimeOnly();

        d.ShouldBe(new DateOnly(2025, 3, 4));
        t.ShouldBe(new TimeOnly(23, 59, 59));
    }

    [Fact]
    public void DateOnly_TimeOnly_From_DateTime_Local()
    {
        var local = new DateTime(2025, 3, 4, 10, 0, 0, DateTimeKind.Local);

        var d = local.ToUtcDateOnly();
        var t = local.ToUtcTimeOnly();

        d.ShouldBeOfType<DateOnly>();
        t.ShouldBeOfType<TimeOnly>();
    }
}