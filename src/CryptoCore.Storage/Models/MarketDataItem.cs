using System.Runtime.InteropServices;
using CryptoCore.Storage.Extensions;

namespace CryptoCore.Storage.Models;

/// <summary>
/// Lightweight wrapper around a single market data event with a unified time key.
/// The <see cref="Timestamp"/> is interpreted via the existing <c>Timestamp.AsDateTime()</c> extension
/// (for example, as Unix time in milliseconds or ticks, depending on your implementation).
/// </summary>
/// <typeparam name="T">
/// The underlying market data payload type (e.g. trade, level update, order book snapshot).
/// Must be an unmanaged type to allow efficient binary serialization.
/// </typeparam>
[StructLayout(LayoutKind.Sequential)]
public readonly struct MarketDataItem<T>
    where T : unmanaged
{
    /// <summary>
    /// Unified time key for this market data item.
    /// The exact time encoding is defined by your <c>Timestamp.AsDateTime()</c> extension method
    /// (typically Unix time in milliseconds or ticks).
    /// </summary>
    public readonly long Timestamp;

    /// <summary>
    /// The underlying market data payload (for example trade, level update, snapshot, etc.).
    /// </summary>
    public readonly T Item;

    /// <summary>
    /// Initializes a new instance of the <see cref="MarketDataItem{T}"/> struct
    /// with the specified payload and timestamp.
    /// </summary>
    /// <param name="item">The market data payload associated with this event.</param>
    /// <param name="timestamp">
    /// The unified time key for this event. It will be converted to <see cref="DateTime"/>
    /// via <c>Timestamp.AsDateTime()</c>.
    /// </param>
    public MarketDataItem(T item, long timestamp)
    {
        Item = item;
        Timestamp = timestamp;
    }

    /// <summary>
    /// Gets the <see cref="DateTime"/> representation of <see cref="Timestamp"/>,
    /// using the existing <c>Timestamp.AsDateTime()</c> extension method.
    /// The resulting time is assumed to be in the system's local time zone or UTC,
    /// depending on your <c>AsDateTime()</c> implementation.
    /// </summary>
    public DateTime DateTime => Timestamp.AsUtcDateTime();

    /// <summary>
    /// Gets the calendar date component of this market data item,
    /// derived from <see cref="DateTime"/>.
    /// </summary>
    public DateOnly Date => DateTime.ToUtcDateOnly();

    /// <summary>
    /// Gets the time-of-day component of this market data item,
    /// derived from <see cref="DateTime"/>.
    /// </summary>
    public TimeOnly Time => DateTime.ToUtcTimeOnly();

    /// <summary>
    /// Returns a string representation of this market data item,
    /// including the timestamp (in ISO-8601 format) and the payload.
    /// </summary>
    /// <returns>
    /// A string in the form <c>[yyyy-MM-ddTHH:mm:ss.fffffffK]: {Item}</c>.
    /// </returns>
    public override string ToString()
    {
        return $"[{DateTime:O}]: {Item}";
    }
}
