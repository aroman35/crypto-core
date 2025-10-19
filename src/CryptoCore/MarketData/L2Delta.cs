using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using CryptoCore.Extensions;
using CryptoCore.Primitives;

namespace CryptoCore.MarketData;

/// <summary>
/// Atomic level-2 change: set <see cref="Quantity"/> for a price level on a given <see cref="Side"/>.
/// Quantity = 0 means “remove level”. Double is used on purpose for speed; compare via <see cref="MathExtensions"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential, Pack = 1)]
public readonly struct L2Delta
{
    /// <summary>Side (Buy=+1, Sell=-1).</summary>
    public readonly Side Side;

    /// <summary>Price level.</summary>
    public readonly double Price;

    /// <summary>New absolute quantity on this level (0 → remove).</summary>
    public readonly double Quantity;

    /// <summary>Create a delta.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public L2Delta(Side side, double price, double quantity)
    {
        Side = side;
        Price = price;
        Quantity = quantity;
    }

    /// <summary>True if this delta removes the level (Quantity == 0).</summary>
    public bool IsRemove => Quantity.IsEquals(0.0);
}
