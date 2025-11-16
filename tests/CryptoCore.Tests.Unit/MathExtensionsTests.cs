using CryptoCore.Extensions;
using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Unit;

public class MathExtensionsTests
{
    [Fact(DisplayName = "IsEquals: numbers within epsilon are equal")]
    public void Equals_WithEpsilon()
    {
        var a = 1.0;
        var b = 1.0 + MathExtensions.PRECISION / 2.0;
        a.IsEquals(b).ShouldBeTrue();
    }

    [Fact(DisplayName = "IsGreater: strictly greater by more than epsilon")]
    public void Greater_Strict()
    {
        var a = 100.0;
        var b = 99.999999;
        a.IsGreater(b).ShouldBeTrue();
        b.IsGreater(a).ShouldBeFalse();
    }

    [Fact(DisplayName = "IsLowerOrEquals and IsGreaterOrEquals behave consistently")]
    public void Ordering_WithEpsilon()
    {
        var a = 10.0;
        var b = 10.0 + MathExtensions.PRECISION * 0.75;

        a.IsLowerOrEquals(b).ShouldBeTrue();
        b.IsGreaterOrEquals(a).ShouldBeTrue();

        // barely below epsilon should still be equal-ish
        var c = 10.0 + MathExtensions.PRECISION * 0.25;
        a.IsEquals(c).ShouldBeTrue();
    }

    [Fact(DisplayName = "MathExtensions: Greater/Lower/Equals")]
    public void MathExtensions_Basic()
    {
        1.0.IsGreater(0.999999).ShouldBeTrue();
        1.0.IsLower(1.000001).ShouldBeTrue();
        1.0.IsEquals(1.0 + 1e-10).ShouldBeTrue();
        1.0.IsGreaterOrEquals(1.0).ShouldBeTrue();
        1.0.IsLowerOrEquals(1.0).ShouldBeTrue();
    }

    [Fact(DisplayName = "Side arithmetic")]
    public void Side_Arithmetic()
    {
        ((int)Side.Buy).ShouldBe(1);
        ((int)Side.Sell).ShouldBe(-1);

        var qty = 5.0;
        (qty * (int)Side.Buy).ShouldBe(5.0);
        (qty * (int)Side.Sell).ShouldBe(-5.0);
    }
}
