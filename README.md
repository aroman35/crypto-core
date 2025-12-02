
# CryptoCore — High‑Performance Market Data Engine for Quantitative Research

## Overview
**CryptoCore** is a modular, high‑performance framework for working with cryptocurrency market‑data streams, historical archives, limit order books (LOB), and quantitative analytics.  
The system is designed for:

- High‑frequency research (HFT microstructure)
- Order book reconstruction
- Econometric feature engineering
- Backtesting (matcher integration planned)
- Mean‑reversion and ML modelling
- Large‑scale, multi‑day dataset generation

CryptoCore consists of several tightly optimized modules:

- **CryptoCore.Storage** — compact archival format + fast replay
- **CryptoCore.OrderBook** — deterministic L2 order book reconstruction
- **CryptoCore.Analytics** — feature engines, dataset builders
- **CryptoCore.Domain** — shared types (Symbol, Asset, Trade, LevelUpdate)

---

## Repository Structure

```
CryptoCore/
 ├── CryptoCore.Storage/         # packed format, IO engine, replay
 ├── CryptoCore.Analytics/       # feature listeners, dataset writers
 ├── CryptoCore.OrderBook/       # L2 order book engine
 ├── CryptoCore.Domain/          # domain primitives: Symbol, Asset, Exchange
 ├── CryptoCore.Tests/           # correctness + performance
 └── README.md
```

---

## 1. Core Domain Types

### 1.1 `Trade`
```csharp
public readonly record struct Trade(
    DateTimeOffset Timestamp,
    Side Side,
    decimal Price,
    decimal Quantity);
```

### 1.2 `LevelUpdate`
Represents a single L2 delta from exchange stream.
```csharp
public readonly record struct LevelUpdate(
    DateTimeOffset Timestamp,
    Side Side,
    decimal Price,
    decimal Quantity,
    bool IsSnapshot);
```

### 1.3 `Symbol`, `Asset`, `Exchange`
Strongly typed value‑objects with zero allocations, used throughout the system.

---

## 2. CryptoCore.Storage

### 2.1 Packed Format (`PackedMarketData24`)
A compact 24‑byte fixed‑layout structure:

| Field | Size | Description |
|-------|-------|-------------|
| `TimeMs`   | 4 bytes | Milliseconds from UTC start‑of‑day |
| `Price`    | 8 bytes | Fixed‑precision decimal (`Decimal9`) |
| `Quantity` | 8 bytes | Fixed‑precision decimal (`Decimal9`) |
| `Flags`    | 4 bytes | Message type, side, snapshot bit |

### 2.2 `MarketDataFlags`

Encodes:

- Message type: L2Update / Trade
- Side: Buy / Sell
- IsSnapshot: yes/no

Example:
```csharp
int flags = MarketDataFlags.Pack(
    MarketDataMessageType.L2Update,
    Side.Buy,
    isSnapshot: false);
```

### 2.3 Conversions (`StorageExtensions`)

#### Encode domain → storage
```csharp
PackedMarketData24 packed = update.ToStorage();
PackedMarketData24 packedTrade = trade.ToStorage();
```

#### Decode storage → domain
```csharp
LevelUpdate l2 = packed.ToLevelUpdate(date);
Trade trade = packed.ToTrade(date);
```

---

## 3. MarketDataCacheReplayer

A high‑throughput engine that:

- Reads packed records sequentially
- Reconstructs the L2 order book (via `OrderBookL2`)
- Groups L2 deltas into fixed windows (100ms by default)
- Sends events to `IMarketDataListener`

Usage:
```csharp
var replayer = new MarketDataCacheReplayer(rootDir, hash, listener);
replayer.Run();
```

Event flow:

- `QuoteBatchReceived`
- `OrderBookUpdated`
- `TopUpdated`
- `TradeReceived`

---

## 4. CryptoCore.OrderBook

### 4.1 `OrderBookL2`
Optimized limit order book:

- Sorted arrays for bids/asks
- O(logN) updates
- Zero allocations
- Ability to copy top‑N levels via pointers or Span

Example top‑N:
```csharp
Span<double> px = stackalloc double[16];
Span<double> qty = stackalloc double[16];
int count = book.CopyTopBids(px, qty, 16);
```

---

## 5. CryptoCore.Analytics

Analytics subsystem converts replayed events into features, labels, and datasets.

### 5.1 `IMarketDataListener`
Base interface for analytical modules:
```csharp
void QuoteBatchReceived(long tsMs, in L2UpdatePooled batch, OrderBookL2 book);
void OrderBookUpdated(long tsMs, OrderBookL2 book);
void TopUpdated(long tsMs, double bbPx, double bbQty, double baPx, double baQty);
void TradeReceived(in Trade trade);
```

### 5.2 Built‑in Listeners

#### QuoteFlowListener
Computes microstructure features:

- CancellationFrequency
- CancellationRate
- UpdateRatio
- OrderFlow
- VWAP (top‑N)
- Imbalance (top‑N)

Example usage:
```csharp
var flow = new QuoteFlowListener(5000, depthLevels: 16);
```

Other modules (TrendListener, VolumeListener, VolatilityListener, DatasetBuilderListener) follow same structure.

---

## 6. Performance Benchmarks

Measured on BTCUSDT (86k events):

### Replay throughput
| Mode | Time |
|------|------|
| Replay only | 14–17 s |
| Replay + OrderBookL2 | 31–34 s |
| Replay + L2 + Features | 52 s |

### IO throughput
| Operation | Speed |
|-----------|--------|
| Sequential read | 2.2–2.8 GB/s |
| Memory‑mapped read | 3.0–3.3 GB/s |
| LZ4 decode | 1.0–1.4 GB/s |

### Write throughput
| Format | Speed |
|--------|--------|
| Raw Packed | 300–450 MB/s |
| LZ4 blocks | 650–850 MB/s |

---

## 7. Usage Examples

### 7.1 Full Replay + Analytics
```csharp
var listener = new QuoteFlowListener(5000, 16);

var replayer = new MarketDataCacheReplayer(
    rootDir,
    hash,
    listener,
    rateMs: 100);

replayer.Run();
```

### 7.2 Multi‑Listener Configuration
```csharp
var listeners = new MultiListener(
    new QuoteFlowListener(5000, 16),
    new TrendListener(3000),
    new DatasetBuilderListener("dataset.csv")
);

new MarketDataCacheReplayer(root, hash, listeners).Run();
```

---

## 8. Dataset Output Example

```
timestamp,
realized_vol,
cancel_rate,
cancel_freq,
trade_intensity,
trade_imbalance,
update_ratio,
order_flow,
vwap,
imbalance,
slope,
reversal
```


---

## 9. License

MIT

---
