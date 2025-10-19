using System.Runtime.CompilerServices;

namespace CryptoCore.Extensions;

/// <summary>
/// Numeric helpers for robust comparison of <see cref="double"/> with a small epsilon.
/// Designed for market data (prices, sizes) where tiny floating errors are expected.
/// </summary>
public static class MathExtensions
{
    /// <summary>
    /// Default epsilon for equality/ordering checks. Tuned for trading use-cases.
    /// </summary>
    public const double PRECISION = 0.000000005d;

    /// <summary>
    /// Returns <c>true</c> if <paramref name="d1"/> is strictly greater than <paramref name="d2"/> by at least <paramref name="e"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGreater(this double d1, double d2, double e = PRECISION) => d1 - d2 > e;

    /// <summary>
    /// Returns <c>true</c> if <paramref name="d1"/> is strictly lower than <paramref name="d2"/> by at least <paramref name="e"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLower(this double d1, double d2, double e = PRECISION) => d2 - d1 > e;

    /// <summary>
    /// Returns <c>true</c> if <paramref name="d1"/> is greater than or approximately equal to <paramref name="d2"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsGreaterOrEquals(this double d1, double d2, double e = PRECISION) => d1 - d2 > e || d1.IsEquals(d2, e);

    /// <summary>
    /// Returns <c>true</c> if <paramref name="d1"/> is lower than or approximately equal to <paramref name="d2"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsLowerOrEquals(this double d1, double d2, double e = PRECISION) => d2 - d1 > e || d1.IsEquals(d2, e);

    /// <summary>
    /// Returns <c>true</c> if the absolute difference between <paramref name="d1"/> and <paramref name="d2"/> is less than <paramref name="e"/>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool IsEquals(this double d1, double d2, double e = PRECISION) => Math.Abs(d1 - d2) < e;
}
