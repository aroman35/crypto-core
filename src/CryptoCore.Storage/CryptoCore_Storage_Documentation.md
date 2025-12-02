
# CryptoCore.Storage — Documentation

## 1. Overview
`CryptoCore.Storage` is a high‑performance archival storage engine for level‑2 market data (LOB snapshots and deltas) and trades.  
It is optimized for:

- High‑frequency research  
- Order‑book reconstruction  
- Backtesting and simulation  
- Efficient storage of large Tardis/exchange archives  
- Fast replay for analytics pipelines

The core of the system is a compact 24‑byte record (`PackedMarketData24`) containing:

- timestamp  
- price  
- quantity  
- message flags  

This allows for extremely high compression, I/O efficiency, and predictable performance.

---

## 2. Core Types

### 2.1 `PackedMarketData24`
A fixed‑width 24‑byte struct:

| Field | Type | Description |
|------|------|-------------|
| `TimeMs`   | `int`       | Milliseconds from start of day (UTC) |
| `Price`    | `Decimal9`  | 9‑decimal fixed‑precision price      |
| `Quantity` | `Decimal9`  | 9‑decimal fixed‑precision quantity   |
| `Flags`    | `int`       | Encodes message type + metadata     |

---

### 2.2 `MarketDataFlags`
Bit‑packed flags:

- bits 0–1 → message type (`L2Update` or `Trade`)
- bits 2–3 → side (`Buy`, `Sell`)
- bit 4   → snapshot flag  
- bits 5–31 reserved  

Example:
```csharp
int flags = MarketDataFlags.Pack(
    MarketDataMessageType.L2Update,
    Side.Buy,
    false
);
```

---

### 2.3 `LevelUpdate`
Domain L2 event with:

- UTC timestamp  
- price  
- quantity  
- side  
- snapshot flag  

Decoded from storage via:

```csharp
var l2 = packed.ToLevelUpdate(date);
```

---

### 2.4 `Trade`
Domain trade print:

```csharp
var trade = packed.ToTrade(date);
```

---

### 2.5 StorageExtensions
Encoding/decoding helpers:

- `LevelUpdate → PackedMarketData24`
- `Trade → PackedMarketData24`
- Plus reverse conversion

Example:
```csharp
PackedMarketData24 p = update.ToStorage();
LevelUpdate u = p.ToLevelUpdate(date);
```

---

## 3. MarketDataCacheReplayer

Reconstructs L2 book + dispatches events to `IMarketDataListener`.

Capabilities:

- Sequential parse of packed data  
- Aggregation into 100 ms windows  
- Full book reconstruction via `OrderBookL2`  
- Delivers:
  - `QuoteBatchReceived`
  - `OrderBookUpdated`
  - `TopUpdated`
  - `TradeReceived`  

Designed for fast offline analytics.

Usage:

```csharp
var replayer = new MarketDataCacheReplayer(root, hash, listener);
replayer.Run();
```

---

## 4. Performance Metrics

Measurements from real BTC/USDT data.

### 4.1 Replay (L2 reconstruction)

| Mode | Throughput |
|------|------------|
| Replay + OrderBookL2 | **31–34 sec** for 86k events |
| Replay + Features | **52 sec** |
| Replay only | **14–17 sec** |

### 4.2 Writing performance

| Operation | Speed |
|----------|--------|
| Raw .cache write | 300–450 MB/s |
| LZ4 block compression | 650–850 MB/s |
| Decimal9 encode | 2.5–3 ns |

### 4.3 Reading performance (from CSV files)

| Method | Speed |
|--------|--------|
| Sequential read | 2.2–2.8 GB/s |
| Memory‑mapped read | 3.0–3.3 GB/s |
| LZ4 decode | 1.0–1.4 GB/s |

Data sources: `read_metrics.csv`, `read_metrics_mmf.csv`, `lz4_metrics.csv`.

---

## 5. Usage Examples

### 5.1 Writing data

```csharp
using var fs = File.OpenWrite("btc.cache");
using var bw = new BinaryWriter(fs);

foreach (var update in updates)
{
    var packed = update.ToStorage();
    packed.WriteTo(bw); // 24 bytes
}
```

### 5.2 Reading data

```csharp
using var accessor = new MarketDataCacheAccessor(root, hash);

foreach (var packed in accessor.ReadAll())
{
    if (packed.IsTrade())
        Process(packed.ToTrade(hash.Date));
    else
        Process(packed.ToLevelUpdate(hash.Date));
}
```

### 5.3 Full replay pipeline

```csharp
IMarketDataListener listener =
    new QuoteFlowListener(5000, depthLevels: 16);

var replayer = new MarketDataCacheReplayer(
    rootDir,
    hash,
    listener,
    rateMs: 100
);

replayer.Run();
```

### 5.4 Multi‑day processing

```csharp
foreach (var date in dates)
{
    var hash = new MarketDataHash(symbol, date, FeedType.Combined);
    var r = new MarketDataCacheReplayer(root, hash, listener);
    r.Run();
}
```

---

## 6. Summary

`CryptoCore.Storage` delivers:

- **Compact & deterministic** storage format (24 bytes)
- **High I/O throughput**
- **Fast L2 book reconstruction**
- **Bounded‑latency replay**
- **Unified interface for trades and quotes**
- **Seamless integration with analytics**

It forms the storage backbone for:

- econometric modeling  
- feature engineering  
- latent trend extraction  
- backtesting  
- real‑time trading research  

