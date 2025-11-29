using CryptoCore.Primitives;
using CryptoCore.Storage.Extensions;

namespace CryptoCore.Storage.Models;

/// <summary>
/// Domain-level representation of a single Level 2 order book update.
/// Timestamp is assumed to be in UTC.
/// </summary>
public readonly record struct LevelUpdate(
    DateTimeOffset Timestamp,
    Side Side,
    decimal Price,
    decimal Quantity,
    bool IsSnapshot)
{
    /// <summary>
    /// Trading date (UTC calendar date) derived from <see cref="Timestamp"/>.
    /// </summary>
    public DateOnly TradeDate => Timestamp.ToUtcDateOnly();

    /// <summary>
    /// Time of day (UTC) derived from <see cref="Timestamp"/>.
    /// </summary>
    public TimeOnly Time => Timestamp.ToUtcTimeOnly();
}
