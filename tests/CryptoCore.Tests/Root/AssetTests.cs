using CryptoCore.Primitives;
using Shouldly;

namespace CryptoCore.Tests.Root
{
    public class AssetTests
    {
        [Fact(DisplayName = "Parse: valid upper ASCII within MaxLength succeeds")]
        public void Parse_Valid()
        {
            var a = Asset.Parse("USDT");
            a.Length.ShouldBe(4);
            a.ToString().ShouldBe("USDT");
        }

        [Fact(DisplayName = "Parse: lower-case input is normalized to upper-case")]
        public void Parse_NormalizesToUpper()
        {
            var a = Asset.Parse("eth");
            a.ToString().ShouldBe("ETH");
            (a == "ETH").ShouldBeTrue();
        }

        [Fact(DisplayName = "Parse: rejects empty or too long values")]
        public void Parse_Rejects_InvalidLengths()
        {
            Should.Throw<FormatException>(() => Asset.Parse(""));
            Should.Throw<FormatException>(() => Asset.Parse(new string('A', Asset.MaxLength + 1)));
        }

        [Fact(DisplayName = "TryParse: rejects non-ASCII characters")]
        public void TryParse_Rejects_NonAscii()
        {
            Asset.TryParse("РУБ".AsSpan(), out _).ShouldBeFalse();
        }

        [Fact(DisplayName = "TryParse: rejects disallowed ASCII (space, slash at end)")]
        public void TryParse_Rejects_Disallowed()
        {
            Asset.TryParse("US D".AsSpan(), out _).ShouldBeFalse();
            Asset.TryParse("USD\\".AsSpan(), out _).ShouldBeFalse();
        }

        [Fact(DisplayName = "FromAscii: accepts bytes and upper-cases letters")]
        public void FromAscii_Works()
        {
            ReadOnlySpan<byte> raw = new[] { (byte)'b', (byte)'t', (byte)'c' };
            var a = Asset.FromAscii(raw);
            a.ToString().ShouldBe("BTC");
        }

        [Fact(DisplayName = "Equality: value equality compares bytes and length")]
        public void Equality_Value()
        {
            var a = Asset.Parse("BNB");
            var b = Asset.Parse("BNB");
            var c = Asset.Parse("BNB1");

            (a == b).ShouldBeTrue();
            (a != c).ShouldBeTrue();
        }

        [Fact(DisplayName = "Equality: equals string without extra allocations after first cache fill")]
        public void Equality_String()
        {
            var a = Asset.Parse("USDT");
            (a == "USDT").ShouldBeTrue();
            (a != "USDC").ShouldBeTrue();
        }

        [Fact(DisplayName = "HashCode: equal assets have equal hashes; different assets usually differ")]
        public void HashCode_Stable()
        {
            var a = Asset.Parse("TQBR");
            var b = Asset.Parse("tqbr");
            var c = Asset.Parse("TQBR1");

            a.GetHashCode().ShouldBe(b.GetHashCode());
            a.GetHashCode().ShouldNotBe(c.GetHashCode());
        }

        [Fact(DisplayName = "ToString: caches resulting string instance")]
        public void ToString_Cache()
        {
            var a = Asset.Parse("BTC");
            var s1 = a.ToString();
            var s2 = a.ToString();
            ReferenceEquals(s1, s2).ShouldBeTrue();
        }

        [Fact(DisplayName = "MaxLength: accepts exactly 11 chars, rejects 12")]
        public void MaxLength_Edge()
        {
            var ok = new string('A', 11);
            var bad = new string('A', 12);

            var a = Asset.Parse(ok);
            a.Length.ShouldBe(11);

            Should.Throw<FormatException>(() => Asset.Parse(bad));
        }

        [Fact(DisplayName = "Charset: digits, underscore, hyphen and dot are allowed")]
        public void Charset_Allowed()
        {
            var a = Asset.Parse("USD_TST-1.2");
            a.ToString().ShouldBe("USD_TST-1.2");
        }

        [Fact(DisplayName = "CompareTo: lexicographic ordering by ASCII")]
        public void CompareTo_Lexicographic()
        {
            var a = Asset.Parse("AAA");
            var b = Asset.Parse("AAZ");
            var c = Asset.Parse("AAAA");

            a.CompareTo(b).ShouldBeLessThan(0);
            b.CompareTo(a).ShouldBeGreaterThan(0);
            a.CompareTo(c).ShouldBeLessThan(0); // "AAA" < "AAAA" (prefix equal, shorter is smaller)
        }

        [Fact(DisplayName = "Predefined constants exist and are upper ASCII")]
        public void Predefined_Constants()
        {
            Asset.USDT.ToString().ShouldBe("USDT");
            Asset.USD.ToString().ShouldBe("USD");
            Asset.BTC.ToString().ShouldBe("BTC");
            Asset.ETH.ToString().ShouldBe("ETH");
            Asset.BNB.ToString().ShouldBe("BNB");
        }
    }
}
