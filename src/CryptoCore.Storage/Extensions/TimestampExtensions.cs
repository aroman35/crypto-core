using System.Runtime.CompilerServices;

namespace CryptoCore.Storage.Extensions;

public static class TimestampExtensions
{
    /// <summary>
    /// Interprets the specified 64-bit value as Unix time in milliseconds (UTC)
    /// and converts it to a <see cref="DateTime"/> with <see cref="DateTimeKind.Utc"/>.
    /// </summary>
    /// <param name="unixMilliseconds">
    /// Unix timestamp in milliseconds since 1970-01-01T00:00:00Z.
    /// </param>
    /// <returns><see cref="DateTime"/> in UTC.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTime AsUtcDateTime(this long unixMilliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds).UtcDateTime;
    }

    /// <summary>
    /// Interprets the specified 64-bit value as Unix time in milliseconds (UTC)
    /// and converts it to a <see cref="DateTimeOffset"/> in UTC.
    /// </summary>
    /// <param name="unixMilliseconds">
    /// Unix timestamp in milliseconds since 1970-01-01T00:00:00Z.
    /// </param>
    /// <returns><see cref="DateTimeOffset"/> with UTC offset.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateTimeOffset AsUtcDateTimeOffset(this long unixMilliseconds)
    {
        return DateTimeOffset.FromUnixTimeMilliseconds(unixMilliseconds);
    }

    /// <summary>
    /// Converts the specified <see cref="DateTime"/> to a Unix timestamp
    /// in milliseconds since 1970-01-01T00:00:00Z (UTC).
    /// </summary>
    /// <remarks>
    /// If <paramref name="value"/> is <see cref="DateTimeKind.Local"/>,
    /// it is converted to UTC first. If it is <see cref="DateTimeKind.Unspecified"/>,
    /// it is treated as UTC without conversion.
    /// </remarks>
    /// <param name="value">The <see cref="DateTime"/> to convert.</param>
    /// <returns>Unix time in milliseconds (UTC).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToUnixMillisecondsTimestamp(this DateTime value)
    {
        DateTime utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return new DateTimeOffset(utc, TimeSpan.Zero).ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Converts the specified <see cref="DateTimeOffset"/> to a Unix timestamp
    /// in milliseconds since 1970-01-01T00:00:00Z (UTC).
    /// </summary>
    /// <param name="value">The <see cref="DateTimeOffset"/> to convert.</param>
    /// <returns>Unix time in milliseconds (UTC).</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToUnixMillisecondsTimestamp(this DateTimeOffset value)
    {
        return value.ToUnixTimeMilliseconds();
    }

    /// <summary>
    /// Converts the specified <see cref="DateTimeOffset"/> to UTC and returns
    /// the corresponding <see cref="DateOnly"/> (calendar date in UTC).
    /// </summary>
    /// <param name="value">The original timestamp.</param>
    /// <returns>
    /// The UTC calendar date extracted from <paramref name="value"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateOnly ToUtcDateOnly(this DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return DateOnly.FromDateTime(utc);
    }

    /// <summary>
    /// Converts the specified <see cref="DateTimeOffset"/> to UTC and returns
    /// the corresponding <see cref="TimeOnly"/> (time of day in UTC).
    /// </summary>
    /// <param name="value">The original timestamp.</param>
    /// <returns>
    /// The UTC time-of-day extracted from <paramref name="value"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeOnly ToUtcTimeOnly(this DateTimeOffset value)
    {
        var utc = value.UtcDateTime;
        return TimeOnly.FromDateTime(utc);
    }

    /// <summary>
    /// Treats the specified <see cref="DateTime"/> as UTC (if already in UTC)
    /// or converts it to UTC (if it has a different <see cref="DateTime.Kind"/>),
    /// and returns the corresponding <see cref="DateOnly"/> (calendar date in UTC).
    /// </summary>
    /// <param name="value">The original <see cref="DateTime"/> value.</param>
    /// <returns>
    /// The UTC calendar date extracted from <paramref name="value"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static DateOnly ToUtcDateOnly(this DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return DateOnly.FromDateTime(utc);
    }

    /// <summary>
    /// Treats the specified <see cref="DateTime"/> as UTC (if already in UTC)
    /// or converts it to UTC (if it has a different <see cref="DateTime.Kind"/>),
    /// and returns the corresponding <see cref="TimeOnly"/> (time of day in UTC).
    /// </summary>
    /// <param name="value">The original <see cref="DateTime"/> value.</param>
    /// <returns>
    /// The UTC time-of-day extracted from <paramref name="value"/>.
    /// </returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static TimeOnly ToUtcTimeOnly(this DateTime value)
    {
        var utc = value.Kind switch
        {
            DateTimeKind.Utc => value,
            DateTimeKind.Local => value.ToUniversalTime(),
            _ => DateTime.SpecifyKind(value, DateTimeKind.Utc)
        };

        return TimeOnly.FromDateTime(utc);
    }
}
