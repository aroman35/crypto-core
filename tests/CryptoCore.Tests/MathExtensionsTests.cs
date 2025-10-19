using CryptoCore.Extensions;
using Shouldly;

namespace CryptoCore.Tests;

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
}
