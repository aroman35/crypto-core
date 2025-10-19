using System.ComponentModel;

namespace CryptoCore.Root;

/// <summary>
/// A bit flags enumeration that encodes market type, contract attributes,
/// and venue (exchange) in a single 64-bit value.
/// Bit layout:
/// - Bits 0..7   : Market type flags (Spot/Futures/Options/Swap/Margin).
/// - Bits 8..15  : Contract attributes (Perpetual/Delivery, margin type).
/// - Bits 16..47 : Venue flags (Binance/OKX/KuCoin/...).
/// - Bits 48..63 : Reserved for future use.
/// The grouped masks (MarketMask/ContractMask/VenueMask) allow fast filtering.
/// </summary>
[Flags]
public enum Exchange : ulong
{
    /// <summary>
    /// No flags set (unknown/none).
    /// </summary>
    None = 0,

    /// <summary>
    /// Mask for market-type bits (0..7).
    /// </summary>
    MarketMask = 0x0000_0000_0000_00FF,

    /// <summary>
    /// Mask for contract-attribute bits (8..15).
    /// </summary>
    ContractMask = 0x0000_0000_0000_FF00,

    /// <summary>
    /// Mask for venue (exchange) bits (16..47).
    /// </summary>
    VenueMask = 0x0000_0000_FFFF_0000,

    /// <summary>
    /// Mask for reserved bits (48..63).
    /// </summary>
    ReservedMask = 0xFFFF_0000_0000_0000,

    // ===== Market Type (0..7) =====

    /// <summary>
    /// Spot market.
    /// </summary>
    Spot = 1UL << 0,

    /// <summary>
    /// Futures market.
    /// </summary>
    Futures = 1UL << 1,

    /// <summary>
    /// Options market.
    /// </summary>
    Options = 1UL << 2,

    /// <summary>
    /// Swap market (venue-specific linear/inverse swaps outside standard futures).
    /// </summary>
    Swap = 1UL << 3,

    /// <summary>
    /// Margin trading available on spot (treated as a market-level capability flag).
    /// </summary>
    Margin = 1UL << 4,

    // ===== Contract Attributes (8..15) =====

    /// <summary>
    /// Perpetual contract (no expiry).
    /// </summary>
    Perpetual = 1UL << 8,

    /// <summary>
    /// Delivery or dated futures (e.g., quarterly).
    /// </summary>
    Delivery = 1UL << 9,

    /// <summary>
    /// Coin-margined contracts (settled/margined in the base coin, e.g., BTC-M).
    /// </summary>
    CoinMargined = 1UL << 10,

    /// <summary>
    /// USD-margined contracts (settled/margined in USD/USDT/USDC, a.k.a. USDⓈ-M).
    /// </summary>
    UsdMargined = 1UL << 11,

    // ===== Venues (16..) =====

    /// <summary>
    /// Binance exchange.
    /// </summary>
    Binance = 1UL << 16,

    /// <summary>
    /// OKX exchange (formerly OKEx).
    /// </summary>
    OKX = 1UL << 17,

    /// <summary>
    /// KuCoin exchange.
    /// </summary>
    KuCoin = 1UL << 18,

    /// <summary>
    /// Bybit exchange.
    /// </summary>
    Bybit = 1UL << 19,

    /// <summary>
    /// Deribit exchange (notable for options).
    /// </summary>
    Deribit = 1UL << 20,

    /// <summary>
    /// Bitget exchange.
    /// </summary>
    Bitget = 1UL << 21,

    // ===== Presets (composite flags with human-readable slugs) =====

    /// <summary>
    /// Binance Spot market.
    /// </summary>
    [Description("binance")]
    BinanceSpot = Binance | Spot,

    /// <summary>
    /// Binance USD-margined Perpetual Futures.
    /// </summary>
    [Description("binance-futures")]
    BinanceFutures = Binance | Futures | Perpetual | UsdMargined,

    /// <summary>
    /// OKX Spot market.
    /// </summary>
    [Description("okx")]
    OKXSpot = OKX | Spot,

    /// <summary>
    /// OKX USD-margined Perpetual Futures.
    /// </summary>
    [Description("okx-futures")]
    OKXFutures = OKX | Futures | Perpetual | UsdMargined,

    /// <summary>
    /// OKX USD-margined Perpetual Swaps.
    /// </summary>
    [Description("okx-swap")]
    OKXSwap = OKX | Swap | Perpetual | UsdMargined,

    /// <summary>
    /// KuCoin Spot market.
    /// </summary>
    [Description("kucoin")]
    KuCoinSpot = KuCoin | Spot,

    /// <summary>
    /// KuCoin USD-margined Perpetual Futures.
    /// </summary>
    [Description("kucoin-futures")]
    KuCoinFutures = KuCoin | Futures | Perpetual | UsdMargined,
}
