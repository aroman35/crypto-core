using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace CryptoCore.Storage.Models.Primitives;

/// <summary>
/// Fixed-point decimal value with 9 fractional digits of precision.
/// Internally stores a signed 64-bit integer where
/// <c>Value = RawValue / 1_000_000_000m</c>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly struct Decimal9 : IEquatable<Decimal9>, IComparable<Decimal9>
{
    /// <summary>
    /// Fixed scale factor used by <see cref="Decimal9"/>:
    /// <c>Scale = 1_000_000_000m</c> (9 decimal places).
    /// </summary>
    public const decimal SCALE = 1_000_000_000m;

    /// <summary>
    /// Raw fixed-point representation.
    /// The actual numeric value is <c>RawValue / Scale</c>.
    /// </summary>
    public readonly long RawValue;

    /// <summary>
    /// Initializes a new <see cref="Decimal9"/> from a pre-scaled raw value.
    /// The resulting numeric value will be <c>rawValue / Scale</c>.
    /// </summary>
    /// <param name="rawValue">Raw fixed-point value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Decimal9(long rawValue)
    {
        RawValue = rawValue;
    }

    /// <summary>
    /// Initializes a new <see cref="Decimal9"/> from a <see cref="decimal"/> value,
    /// using the fixed scale <see cref="SCALE"/>.
    /// </summary>
    /// <param name="value">The decimal value to encode.</param>
    /// <exception cref="OverflowException">
    /// Thrown if the scaled value does not fit into a 64-bit signed integer.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Decimal9(decimal value)
    {
        RawValue = ToRaw(value);
    }

    /// <summary>
    /// Creates a new <see cref="Decimal9"/> from a <see cref="double"/> value,
    /// using the fixed scale <see cref="SCALE"/>.
    /// </summary>
    /// <param name="value">The double value to encode.</param>
    /// <exception cref="OverflowException">
    /// Thrown if the scaled value does not fit into a 64-bit signed integer.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Decimal9 FromDouble(double value)
    {
        return new Decimal9((decimal)value);
    }

    /// <summary>
    /// Converts a <see cref="decimal"/> value to a raw 64-bit fixed-point representation
    /// using the fixed scale <see cref="SCALE"/>.
    /// </summary>
    /// <param name="value">The decimal value to convert.</param>
    /// <returns>The raw fixed-point value.</returns>
    /// <exception cref="OverflowException">
    /// Thrown if the scaled value does not fit into a 64-bit signed integer.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static long ToRaw(decimal value)
    {
        // Multiply by scale, round to nearest integer, then cast to Int64 with overflow check.
        return (long)decimal.Round(value * SCALE, 0, MidpointRounding.AwayFromZero);
    }

    /// <summary>
    /// Converts the raw fixed-point representation back to a <see cref="decimal"/> value.
    /// </summary>
    /// <returns>The decoded decimal value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public decimal ToDecimal()
    {
        return RawValue / SCALE;
    }

    /// <summary>
    /// Returns the string representation of the underlying decimal value
    /// using <see cref="decimal.ToString()"/>.
    /// </summary>
    public override string ToString()
    {
        return ToDecimal().ToString(CultureInfo.InvariantCulture);
    }

    /// <inheritdoc />
    public bool Equals(Decimal9 other) => RawValue == other.RawValue;

    /// <inheritdoc />
    public override bool Equals(object? obj) => obj is Decimal9 other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode() => RawValue.GetHashCode();

    /// <inheritdoc />
    public int CompareTo(Decimal9 other) => RawValue.CompareTo(other.RawValue);

    /// <summary>
    /// Implicit conversion from <see cref="decimal"/> to <see cref="Decimal9"/>.
    /// </summary>
    /// <param name="value">The decimal value to encode.</param>
    /// <exception cref="OverflowException">
    /// Thrown if the scaled value does not fit into a 64-bit signed integer.
    /// </exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Decimal9(decimal value) => new(value);

    /// <summary>
    /// Implicit conversion from <see cref="Decimal9"/> to <see cref="decimal"/>.
    /// </summary>
    /// <param name="value">The fixed-point value to decode.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator decimal(Decimal9 value) => value.ToDecimal();

    /// <summary>
    /// Explicit conversion from <see cref="Decimal9"/> to raw <see cref="long"/> representation.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator long(Decimal9 value) => value.RawValue;

    /// <summary>
    /// Explicit conversion from raw <see cref="long"/> to <see cref="Decimal9"/>.
    /// The numeric value will be <c>rawValue / Scale</c>.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static explicit operator Decimal9(long rawValue) => new(rawValue);

    public static bool operator ==(Decimal9 left, Decimal9 right)
    {
        return left.Equals(right);
    }

    public static bool operator !=(Decimal9 left, Decimal9 right)
    {
        return !(left == right);
    }

    public static bool operator <(Decimal9 left, Decimal9 right)
    {
        return left.CompareTo(right) < 0;
    }

    public static bool operator <=(Decimal9 left, Decimal9 right)
    {
        return left.CompareTo(right) <= 0;
    }

    public static bool operator >(Decimal9 left, Decimal9 right)
    {
        return left.CompareTo(right) > 0;
    }

    public static bool operator >=(Decimal9 left, Decimal9 right)
    {
        return left.CompareTo(right) >= 0;
    }
}
