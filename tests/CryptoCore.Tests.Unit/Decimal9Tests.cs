using CryptoCore.Storage.Models.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// Tests for Decimal9 fixed-point primitive.
/// </summary>
public class Decimal9Tests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(-1)]
    [InlineData(123456.123456789)]
    [InlineData(0.000000001)]
    [InlineData(-0.000000001)]
    public void Decimal9_Roundtrip_Decimal(decimal value)
    {
        var d9 = new Decimal9(value);
        var back = d9.ToDecimal();

        back.ShouldBe(value);
        d9.RawValue.ShouldBe(Decimal9.ToRaw(value));
    }

    [Fact]
    public void Decimal9_Implicit_Conversion_Works()
    {
        decimal value = 42.000000001m;
        Decimal9 d9 = value; // implicit
        decimal back = d9; // implicit

        back.ShouldBe(value);
    }

    [Fact]
    public void Decimal9_Comparison_Operators_Work()
    {
        var a = new Decimal9(1.0m);
        var b = new Decimal9(2.0m);

        (a < b).ShouldBeTrue();
        (a <= b).ShouldBeTrue();
        (b > a).ShouldBeTrue();
        (b >= a).ShouldBeTrue();
        (a == new Decimal9(1.0m)).ShouldBeTrue();
        (a != b).ShouldBeTrue();
    }
}
