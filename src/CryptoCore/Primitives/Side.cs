namespace CryptoCore.Primitives;

/// <summary>
/// Trade direction with signed numeric values for convenient arithmetic,
/// e.g. <c>position += side * qty</c>.
/// </summary>
public enum Side : sbyte
{
    /// <summary>No side / unknown.</summary>
    None = 0,

    /// <summary>Buy side (positive), numeric value = +1.</summary>
    Buy = 1,

    /// <summary>Sell side (negative), numeric value = -1.</summary>
    Sell = -1,
}
