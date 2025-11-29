using Shouldly;
using Version = CryptoCore.Storage.Models.Version;

namespace CryptoCore.Tests.Unit;

/// <summary>
/// Tests for Version value type.
/// </summary>
public class VersionTests
{
    [Fact]
    public void Version_Parse_Valid_Values()
    {
        var v = Version.Parse("1.2.3");
        v.Major.ShouldBe(1);
        v.Minor.ShouldBe(2);
        v.Build.ShouldBe(3);

        v.ToString().ShouldBe("1.2.3");
    }

    [Fact]
    public void Version_Implicit_FromInt_Sets_Major()
    {
        Version v = 5;
        v.Major.ShouldBe(5);
        v.Minor.ShouldBe(0);
        v.Build.ShouldBe(0);
    }

    [Fact]
    public void Version_Comparison_And_Compatibility()
    {
        var v1 = Version.Create(1, 2, 3);
        var v2 = Version.Create(1, 2, 4);
        var v3 = Version.Create(2, 0, 0);

        (v2 > v1).ShouldBeTrue();
        (v1 < v2).ShouldBeTrue();
        (v1.IsCompatible(v2)).ShouldBeTrue(); // same major/minor
        (v1.IsCompatible(v3)).ShouldBeFalse(); // different major/minor
    }
}