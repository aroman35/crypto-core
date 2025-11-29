using CryptoCore.Storage.Models.Enums;

namespace CryptoCore.Storage.Models.Attributes;

[AttributeUsage(AttributeTargets.Struct | AttributeTargets.Class)]
public class FeedAttribute(FeedType feed) : Attribute
{
    public FeedType Feed { get; } = feed;
}
