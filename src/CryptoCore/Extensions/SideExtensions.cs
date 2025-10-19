using System.Runtime.CompilerServices;
using CryptoCore.Primitives;

namespace CryptoCore.Extensions;

/// <summary>Helpers for <see cref="Side"/> arithmetics.</summary>
public static class SideExtensions
{
    /// <summary>Converts <see cref="Side"/> to its signed integer (+1, 0, -1).</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static int Sign(this Side side) => (int)side;

    /// <summary>Returns <c>side * value</c> as <see cref="double"/> for quick PnL/position math.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static double Mul(this Side side, double value) => side.Sign() * value;
}
