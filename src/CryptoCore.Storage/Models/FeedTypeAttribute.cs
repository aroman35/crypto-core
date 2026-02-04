using System;
using CryptoCore.Storage.Models.Enums;

namespace CryptoCore.Storage.Models;

[AttributeUsage(AttributeTargets.Struct, Inherited = false, AllowMultiple = false)]
public sealed class FeedTypeAttribute : Attribute
{
    public FeedTypeAttribute(FeedType feedType, int major, int minor, int build)
    {
        FeedType = feedType;
        Version = Version.Create(major, minor, build);
    }

    public FeedType FeedType { get; }

    public Version Version { get; }
}