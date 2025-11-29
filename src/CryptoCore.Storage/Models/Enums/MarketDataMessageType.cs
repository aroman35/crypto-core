namespace CryptoCore.Storage.Models.Enums;

/// <summary>
/// Message type stored in the packed 24-byte record.
/// </summary>
public enum MarketDataMessageType : byte
{
    /// <summary>Level 2 order book update.</summary>
    L2Update = 0,

    /// <summary>Trade print.</summary>
    Trade = 1

    // Values 2 and 3 are reserved for future message types.
}
