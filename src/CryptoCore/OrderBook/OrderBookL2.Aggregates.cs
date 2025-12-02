using System.Runtime.CompilerServices;
using CryptoCore.Extensions;
using CryptoCore.Primitives;

namespace CryptoCore.OrderBook;

/// <summary>
/// Derived metrics and counters for <see cref="OrderBookL2"/>:
/// VWAP over top-N levels, top-level imbalance, cancellation counters.
/// Implemented in a separate partial to keep core structure minimal.
/// </summary>
public sealed partial class OrderBookL2
{
    private long _cancelsBid;
    private long _cancelsAsk;

    /// <summary>Total removals (Quantity→0) applied to bid/ask since last reset.</summary>
    public (long Bid, long Ask) CancellationCounters => (_cancelsBid, _cancelsAsk);

    /// <summary>Resets cancellation counters to zero.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void ResetCancellationCounters() => _cancelsBid = _cancelsAsk = 0;

    /// <summary>
    /// Computes VWAP over the top-N price levels on a side. N=0 means all levels.
    /// Returns (vwap, totalQty). If the book is empty on the side, returns (0,0).
    /// </summary>
    public (double Vwap, double Qty) ComputeVwap(Side side, int topLevels = 0)
    {
        double sumPxQty = 0, sumQty = 0;
        var i = 0;

        var seq = side == Side.Buy
            ? EnumerateBids(topLevels == 0 ? int.MaxValue : topLevels)
            : EnumerateAsks(topLevels == 0 ? int.MaxValue : topLevels);

        foreach (var (p, q) in seq)
        {
            if (q.IsLowerOrEquals(0))
                continue;
            sumPxQty += p * q;
            sumQty += q;
            if (topLevels != 0 && ++i >= topLevels)
                break;
        }

        if (sumQty.IsEquals(0))
            return (0, 0);
        return (sumPxQty / sumQty, sumQty);
    }

    /// <summary>
    /// Imbalance over top-N levels: (BidQty - AskQty) / (BidQty + AskQty). Returns 0 if both sums are 0.
    /// </summary>
    public double ComputeTopImbalance(int topLevels = 1)
    {
        double qb = 0, qa = 0;
        var i = 0;

        foreach (var (_, q) in EnumerateBids(topLevels == 0 ? int.MaxValue : topLevels))
        {
            qb += q;
            if (topLevels != 0 && ++i >= topLevels)
                break;
        }

        i = 0;
        foreach (var (_, q) in EnumerateAsks(topLevels == 0 ? int.MaxValue : topLevels))
        {
            qa += q;
            if (topLevels != 0 && ++i >= topLevels)
                break;
        }

        var sum = qb + qa;
        if (sum.IsEquals(0))
            return 0;
        return (qb - qa) / sum;
    }

    // отмены считаем — это экземплярный метод
    partial void OnLevelRemoved(Side side, double price, double prevQty)
    {
        if (side == Side.Buy)
            _cancelsBid++;
        else if (side == Side.Sell)
            _cancelsAsk++;
    }

    // пустой хук для будущих метрик; подавляем CA1822 (не трогает состояние)
    [System.Diagnostics.CodeAnalysis.SuppressMessage(
        "Performance",
        "CA1822:Mark members as static",
        Justification = "signature for partial hook across parts")]
    partial void OnLevelUpserted(Side side, double price, double qty)
    {
        // intentionally empty
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int CopyTopBids(double* pxPtr, double* qtyPtr, int levels)
    {
        ArgumentNullException.ThrowIfNull(pxPtr);
        ArgumentNullException.ThrowIfNull(qtyPtr);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);

        var i = 0;
        using var e = _bidsSorted.GetEnumerator();
        while (i < levels && e.MoveNext())
        {
            var kv = e.Current;
            pxPtr[i] = kv.Key;
            qtyPtr[i] = kv.Value;
            i++;
        }

        return i;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public unsafe int CopyTopAsks(double* pxPtr, double* qtyPtr, int levels)
    {
        ArgumentNullException.ThrowIfNull(pxPtr);
        ArgumentNullException.ThrowIfNull(qtyPtr);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(levels);

        var i = 0;
        using var e = _asksSorted.GetEnumerator();
        while (i < levels && e.MoveNext())
        {
            var kv = e.Current;
            pxPtr[i] = kv.Key;
            qtyPtr[i] = kv.Value;
            i++;
        }

        return i;
    }

    public unsafe int CopyTopBids(Span<double> prices, Span<double> qtys, int levels)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(prices.Length, qtys.Length);
        ArgumentOutOfRangeException.ThrowIfNotEqual(prices.Length, levels);

        fixed (double* pxPtr = prices)
        {
            fixed (double* qtyPtr = qtys)
            {
                return CopyTopBids(pxPtr, qtyPtr, levels);
            }
        }
    }

    public unsafe int CopyTopAsks(Span<double> prices, Span<double> qtys, int levels)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(prices.Length, qtys.Length);
        ArgumentOutOfRangeException.ThrowIfNotEqual(prices.Length, levels);

        fixed (double* pxPtr = prices)
        {
            fixed (double* qtyPtr = qtys)
            {
                return CopyTopAsks(pxPtr, qtyPtr, levels);
            }
        }
    }
}
