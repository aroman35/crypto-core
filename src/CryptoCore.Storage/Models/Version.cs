using System.Globalization;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;

namespace CryptoCore.Storage.Models;

/// <summary>
/// Lightweight semantic version value type: Major.Minor.Build.
/// Хранится как три int'а подряд, без ссылочных типов.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public readonly partial struct Version : IEquatable<Version>, IComparable<Version>
{
    public readonly int Major;
    public readonly int Minor;
    public readonly int Build;

    #region Ctors / factories

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Version(int major, int minor = 0, int build = 0)
    {
        Major = major;
        Minor = minor;
        Build = build;
    }

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Version Create(int major)
        => new(major);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Version Create(int major, int minor)
        => new(major, minor);

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Version Create(int major, int minor, int build)
        => new(major, minor, build);

    #endregion

    #region Parse / TryParse

    /// <summary>
    /// Parses a string in the form "major.minor.build".
    /// Example: "1.0.0".
    /// </summary>
    /// <exception cref="FormatException">If format is invalid.</exception>
    public static Version Parse(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new FormatException("Version string is null or empty.");

        var match = VersionRegexPattern().Match(value);
        if (!match.Success)
            throw new FormatException($"Version format is invalid: '{value}'.");

        var major = int.Parse(match.Groups["major"].Value, CultureInfo.InvariantCulture);
        var minor = int.Parse(match.Groups["minor"].Value, CultureInfo.InvariantCulture);
        var build = int.Parse(match.Groups["build"].Value, CultureInfo.InvariantCulture);

        return new Version(major, minor, build);
    }

    /// <summary>
    /// Tries to parse a string in the form "major.minor.build".
    /// </summary>
    public static bool TryParse(string? value, out Version version)
    {
        version = default;
        if (string.IsNullOrWhiteSpace(value))
            return false;

        var match = VersionRegexPattern().Match(value);
        if (!match.Success)
            return false;

        if (!int.TryParse(match.Groups["major"].Value, out var major))
            return false;
        if (!int.TryParse(match.Groups["minor"].Value, out var minor))
            return false;
        if (!int.TryParse(match.Groups["build"].Value, out var build))
            return false;

        version = new Version(major, minor, build);
        return true;
    }

    /// <summary>
    /// Parses UTF-8 bytes representing "major.minor.build".
    /// Нормальный, простой вариант: сначала делаем строку.
    /// Для header’а это не hot-path.
    /// </summary>
    public static Version Parse(ReadOnlySpan<byte> utf8Bytes)
    {
        if (utf8Bytes.IsEmpty)
            throw new FormatException("Version span is empty.");

        var s = Encoding.UTF8.GetString(utf8Bytes);
        return Parse(s);
    }

    /// <summary>
    /// Tries to parse UTF-8 bytes representing "major.minor.build".
    /// </summary>
    public static bool TryParse(ReadOnlySpan<byte> utf8Bytes, out Version version)
    {
        if (utf8Bytes.IsEmpty)
        {
            version = default;
            return false;
        }

        var s = Encoding.UTF8.GetString(utf8Bytes);
        return TryParse(s, out version);
    }

    #endregion

    #region Compatibility / comparisons

    /// <summary>
    /// Простая проверка совместимости:
    /// совместимы, если совпадают Major и Minor, Build игнорируем.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool IsCompatible(Version other)
        => other.Major == Major && other.Minor == Minor;

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int CompareTo(Version other)
    {
        var cmp = Major.CompareTo(other.Major);
        if (cmp != 0)
            return cmp;

        cmp = Minor.CompareTo(other.Minor);
        if (cmp != 0)
            return cmp;

        return Build.CompareTo(other.Build);
    }

    #endregion

    #region Equality / hash / operators

    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(Version other)
        => Major == other.Major && Minor == other.Minor && Build == other.Build;

    public override bool Equals(object? obj)
        => obj is Version other && Equals(other);

    public override int GetHashCode()
        => HashCode.Combine(Major, Minor, Build);

    public static bool operator ==(Version left, Version right) => left.Equals(right);
    public static bool operator !=(Version left, Version right) => !left.Equals(right);

    public static bool operator <(Version left, Version right) => left.CompareTo(right) < 0;
    public static bool operator >(Version left, Version right) => left.CompareTo(right) > 0;
    public static bool operator <=(Version left, Version right) => left.CompareTo(right) <= 0;
    public static bool operator >=(Version left, Version right) => left.CompareTo(right) >= 0;

    /// <summary>
    /// Неявное преобразование из int: "1" → "1.0.0".
    /// Удобно для быстрых констант типа Version v = 1;
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static implicit operator Version(int value)
        => new(value);

    #endregion

    #region Formatting

    public override string ToString()
        => $"{Major}.{Minor}.{Build}";

    [GeneratedRegex(@"^(?<major>\d+)\.(?<minor>\d+)\.(?<build>\d+)$", RegexOptions.Compiled)]
    private static partial Regex VersionRegexPattern();

    #endregion
}
