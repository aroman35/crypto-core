# CryptoCore

[![Version](https://img.shields.io/github/v/tag/aroman35/crypto-core?label=version)](https://github.com/aroman35/crypto-core/tags)
[![Build Status](https://github.com/aroman35/crypto-core/actions/workflows/publish.yml/badge.svg)](https://github.com/aroman35/crypto-core/actions/workflows/publish.yml)
[![Tests](https://github.com/aroman35/crypto-core/actions/workflows/publish.yml/badge.svg?label=tests)](https://github.com/aroman35/crypto-core/actions/workflows/publish.yml)

> **Current version:** from repo tags

High‑performance, allocation‑aware primitives and building blocks for crypto market data in .NET 9:
- **Asset** — fixed‑size, ASCII‑only value type with zero‑allocation parsing and fast equality.
- **Symbol** — compact value type combining `BaseAsset`, `QuoteAsset`, and `Exchange`, with exchange‑native string forms.
- **Exchange** — flag‑based enum with helper extensions and **short preset slugs** (`binance`, `binance-futures`, `okx`, `okx-futures`, `okx-swap`, `kucoin`, `kucoin-futures`).
- **Side** — `+1`/`-1` enum for arithmetic-friendly direction.
- **Market data types** — `PublicTrade`, `L2Update`/`L2UpdatePooled`.
- **OrderBookL2** — fast L2 book with snapshot+delta assembly and aggregation helpers.
- **Streaming** — abstractions for market‑data transport + a Channel‑based reference transport.
- **Binance connector** — public WS/combined stream + depth/trades parsers + snapshot provider + order book store.

---

## Table of Contents
- [Packages](#packages)
- [Quick Start](#quick-start)
  - [Add YC NuGet source](#add-yc-nuget-source)
  - [Install packages](#install-packages)
- [Primitives](#primitives)
  - [Asset](#asset)
  - [Symbol](#symbol)
  - [Exchange](#exchange)
  - [Side](#side)
- [Market Data Models](#market-data-models)
  - [PublicTrade](#publictrade)
  - [L2Update and L2UpdatePooled](#l2update-and-l2updatepooled)
- [Order Book](#order-book)
  - [OrderBookL2](#orderbookl2)
  - [OrderBookStore](#orderbookstore)
- [Streaming Abstractions](#streaming-abstractions)
- [Binance Connector](#binance-connector)
- [JSON Serialization](#json-serialization)
- [Versioning](#versioning)
- [License](#license)
- [Repository Map](#repository-map)

---

## Packages

| Package | What’s inside |
|---|---|
| `CryptoCore` | `Asset`, `Symbol`, `Exchange`, `Side`, math/utility extensions |
| `CryptoCore.Serialization` | JSON converters for `System.Text.Json` and `Newtonsoft.Json` |
| `CryptoStreaming.Abstractions` | Transport abstraction (`IMarketDataTransport`, `IMarketDataSubscription<T>`) |
| `CryptoStreaming.Channels` | Channel‑based transport implementation |
| `CryptoConnector.Binance` | Public WS client + parsers + snapshot provider + book store |

> Target framework: **.NET 9**

---

## Quick Start

### Add YC NuGet source

```bash
# macOS/Linux
SOURCE="https://registry.yandexcloud.net/nuget/v3/<YOUR_REGISTRY_ID>/index.json"
TOKEN="<YOUR_IAM_TOKEN>"

dotnet nuget add source "$SOURCE"   --name "yc-reg"   --username "iam"   --password "$TOKEN"   --store-password-in-clear-text   --protocol-version 3
```

### Install packages

```bash
dotnet add package CryptoCore --source yc-reg
dotnet add package CryptoCore.Serialization --source yc-reg
dotnet add package CryptoStreaming.Abstractions --source yc-reg
dotnet add package CryptoStreaming.Channels --source yc-reg
dotnet add package CryptoConnector.Binance --source yc-reg
```

---

## Primitives

### Asset

Fixed‑size ASCII identifier (max length 11). Stores upper‑case bytes; zero‑allocation parse; cached `ToString()`.

| Member | Type | Description |
|---|---|---|
| `MAX_LENGTH` | `int` | Maximum ASCII length (11). |
| `AsciiBytes` | `ReadOnlySpan<byte>` | Raw upper‑case ASCII bytes of the asset. |
| `TryParse(ReadOnlySpan<char>, out Asset)` | `bool` | Parses ASCII, normalizes to upper case; rejects invalid chars. |
| `TryFromAscii(ReadOnlySpan<byte>, out Asset)` | `bool` | Parses ASCII bytes directly. |
| `ToString()` | `string` | Cached string (first call allocates, then memoized). |
| Comparison | operators | Zero‑allocation equality + `<,<=,>,>=`. |

**Examples**
```csharp
using CryptoCore.Primitives;

var usdt = Asset.Parse("usdt");               // "USDT"
var btc  = Asset.Parse("BTC");
var eq   = btc == "btc";                      // true
var s    = btc.ToString();                    // "BTC" (cached)
```

---

### Symbol

Compact value type: `BaseAsset + QuoteAsset + Exchange`. Parses and emits **native exchange forms**. Delimiter‑less parsing via **stable‑coin suffix**. Exchange preset can be rebound with `For(...)`.

| Member | Type | Description |
|---|---|---|
| `BaseAsset` | `Asset` | Left part. |
| `QuoteAsset` | `Asset` | Right part. |
| `Exchange` | `Exchange` | Flags describing venue/market/attrs. |
| `Create(Asset, Asset, Exchange)` | `Symbol` | Construct directly. |
| `TryParse(ReadOnlySpan<char>, out Symbol)` | `bool` | Accepts: `BASE-QUOTE@Preset`, `BASE/QUOTE`, `BASE_QUOTE`, **delimiter-less** (`BTCUSDT`) and OKX‑like (`BTC-USDT[-SWAP]`). |
| `AddStablecoin(string/Asset)` | `void` | Extend delimiter‑less suffix registry. |
| `For(Exchange)` | `Symbol` | Rebind exchange (preserves base/quote). |
| `ToString()` | `string` | Exchange‑native form: Binance `BTC‑USDT` (spot prints as `BTCUSDT` when venue is Binance), OKX Spot `BTC‑USDT`, OKX Swap `BTC‑USDT‑SWAP`. |
| Compare | `IComparable<Symbol>` | Lexicographic by base, quote, then preset name. |

**Examples**
```csharp
using CryptoCore.Primitives;

var s1 = Symbol.Parse("BTCUSDT");                // Binance Spot by default for delimiter-less
var s2 = s1.For(Exchange.BinanceSpot);           // explicit preset
var s3 = Symbol.Parse("ETH-USDT@OKXSpot");       // explicit preset
var s4 = Symbol.Parse("BTC-USDT-SWAP");          // OKX perpetual/swap
```

**String forms by venue**

| Venue | Spot | Perp/Swap | Delivery |
|---|---|---|---|
| **Binance / Bybit** | `BTCUSDT` | `BTCUSDT` | n/a |
| **OKX** | `BTC-USDT` | `BTC-USDT-SWAP` | `BTC-USD-YYYYMMDD` |
| **KuCoin** | `BTC-USDT` | `BTC-USDT` | n/a |

> Delimiter‑less split uses the longest stable‑coin suffix (`USDT`, `USDC`, `BUSD`, `TUSD`, `USDP`, `DAI`, `FDUSD`, `USD`). Extendable at runtime.

---

### Exchange

Flag enum capturing venue/market/contract/margin with helper extensions.  
**Short preset slugs** (used everywhere for JSON and text IO):

- `binance`, `binance-futures`
- `okx`, `okx-futures`, `okx-swap`
- `kucoin`, `kucoin-futures`

> Input text is either the **enum name** (case‑insensitive) like `OKXSwap` or the **short slug** above.

**Common presets**

| Preset name | Flags |
|---|---|
| `BinanceSpot` | `Binance | Spot` |
| `BinanceFutures` | `Binance | Futures | Perpetual | UsdMargined` |
| `OKXSpot` | `OKX | Spot` |
| `OKXFutures` | `OKX | Futures | Perpetual | UsdMargined` |
| `OKXSwap` | `OKX | Swap | Perpetual | UsdMargined` |
| `KuCoinSpot` | `KuCoin | Spot` |
| `KuCoinFutures` | `KuCoin | Futures | Perpetual | UsdMargined` |

Helpers: `.IsSpot()`, `.IsFutures()`, `.IsSwap()`, `.IsPerpetual()`, `.IsDelivery()`, `.IsUsdMargined()`, `.IsCoinMargined()`, `.IsBinance()`, `.IsOKX()`, `.TryGetSingleVenue()`.

---

### Side

Arithmetic‑friendly direction enum:

| Name | Value | Meaning |
|---|---|---|
| `Buy` | `+1` | Bid / maker buy / taker buy |
| `Sell` | `-1` | Ask / maker sell / taker sell |

---

## Market Data Models

### PublicTrade

| Field | Type | Notes |
|---|---|---|
| `Symbol` | `Symbol` | |
| `Price` | `double` | Positive. |
| `Quantity` | `double` | Positive. |
| `Side` | `Side` | Aggressor direction. |
| `EventTimeMs` | `long` | Exchange event time (ms). |
| `TradeId` | `long` | Exchange trade id. |
| `Attributes` | `TradeAttributes` | Flags (best match, maker, etc.). |

---

### L2Update and L2UpdatePooled

Efficient depth deltas with **binary serialization** and **pooling** for zero GC.

| Field | Type | Notes |
|---|---|---|
| `Symbol` | `Symbol` | |
| `LastUpdateId` | `long` | Binance `u` (or stitched id). |
| `PrevLastUpdateId` | `long` | Binance `pu` / previous `u`. |
| `EventTimeMs` | `long` | Timestamp for lag metrics. |
| `Deltas` | `L2Delta[]` / pooled | Price‑level updates. |
| Flags | `IsSnapshot` bit | Snapshot batches marked via flag. |

`L2Delta`:
| Field | Type | Notes |
|---|---|---|
| `Side` | `Side` | `Buy`=bid, `Sell`=ask. |
| `Price` | `double` | Level price. |
| `Quantity` | `double` | `0` ⇒ delete the level. |

**Binary IO**
- `TryWrite(Span<byte> dst, out int written)`
- `TryRead(ReadOnlySpan<byte> src, out L2Update)`
- `L2UpdatePooled.Dispose()` returns rented array.

---

## Order Book

### OrderBookL2

Fast reference implementation of an L2 order book for **snapshot + incremental** updates.

| API | Signature | Notes |
|---|---|---|
| Snapshot/Delta | `Apply(in L2Update update)` | One entry point; `IsSnapshot` inside the update switches mode. |
| Top | `BestBid()` / `BestAsk()` | Return `(price, qty)`. |
| Enumerate | `EnumerateBids(int n)` / `EnumerateAsks(int n)` | Allocation‑free enumerators. |
| Events | `OnTopUpdated(Action<OrderBookL2>)` / `OnBookUpdated(Action<OrderBookL2>)` | Lightweight callbacks for UI/metrics. |

Aggregates (VWAP, imbalance, cancellation rate) live in a partial file.

---

### OrderBookStore

Owns per‑symbol books; **fetches snapshot**, buffers deltas until ready; stitches according to Binance rules.

| Option | Type | Default | Description |
|---|---|---|---|
| `MaxBufferPerSymbol` | `int` | `8192` | Max queued deltas while waiting for snapshot. |
| `SnapshotLimit` | `int` | `1000` | Snapshot depth per side. |
| `MaxRetryAttempts` | `int` | `5` | Snapshot/subscribe retries. |
| `InitialBackoff` | `TimeSpan` | `500ms` | Backoff start. |
| `MaxBackoff` | `TimeSpan` | `10s` | Backoff cap. |

---

## Streaming Abstractions

`IMarketDataTransport` exposes **subscription factories** returning `IMarketDataSubscription<T>`.  
Each subscription yields an `IAsyncEnumerable<T>`. Reference impl: `ChannelMarketDataTransport`.

- Depth: single subscriber (book store).
- Trades: multi‑subscriber.

---

## Binance Connector

- `BinancePublicClient` — WS (combined stream supported) + dynamic (un)subscribe.
- `BinanceDepthParser` / `BinanceTradeParser` — low‑allocation parsers.
- `BinanceSnapshotProvider` — REST snapshot for initial book state.
- `OrderBookStore` — enforces Binance stitching (`U <= L+1 <= u`).

**Usage**
```csharp
using var transport = new ChannelMarketDataTransport();
using var client = new BinancePublicClient(new SimpleSymbolProvider());
using var http = new HttpClient();
var snapshots = new BinanceSnapshotProvider(http);

var store = new OrderBookStore(client, transport, new OrderBookStoreOptions { SnapshotLimit = 1000 });
await store.StartAsync(CancellationToken.None);

var sym = Symbol.Parse("BTCUSDT").For(Exchange.BinanceSpot);
await store.GetOrCreateAsync(sym, CancellationToken.None);

var (bidPx, bidQty) = store.TryGet(sym)!.BestBid();
var (askPx, askQty) = store.TryGet(sym)!.BestAsk();
```

---

## JSON Serialization

**Short slug policy** (only presets): `binance`, `binance-futures`, `okx`, `okx-futures`, `okx-swap`, `kucoin`, `kucoin-futures`.  
Input accepts **either** short slugs **or** enum names (`OKXSwap`, `BinanceFutures`, …), case‑insensitive.

System.Text.Json:
```csharp
using System.Text.Json;
using CryptoCore.Serialization.SystemTextJson;
using CryptoCore.Primitives;

var options = new JsonSerializerOptions().AddCryptoCoreConverters();
var x = Exchange.OKXSwap;
var json = JsonSerializer.Serialize(x, options);       // "okx-swap"
var back = JsonSerializer.Deserialize<Exchange>(json, options); // OKXSwap

var dict = new Dictionary<Exchange,int> { [Exchange.BinanceSpot] = 1 };
var dictJson = JsonSerializer.Serialize(dict, options);  // {"binance":1}
```

Newtonsoft.Json:
```csharp
using Newtonsoft.Json;
using CryptoCore.Serialization.Newtonsoft;
using CryptoCore.Primitives;

var settings = new JsonSerializerSettings().AddCryptoCoreConverters();
var json = JsonConvert.SerializeObject(Exchange.BinanceFutures, settings); // "binance-futures"
var back = JsonConvert.DeserializeObject<Exchange>(json, settings);        // BinanceFutures
```

---

## Versioning

Tags follow `vX.Y.Z`. See all tags on the **Tags** page.

---

## License

[MIT](LICENSE)

---

## Repository Map

```
src/
  CryptoCore/                    # Primitives, order book
  CryptoCore.Serialization/      # System.Text.Json + Newtonsoft converters
  CryptoStreaming.Abstractions/  # Transport contracts
  CryptoStreaming.Channels/      # Channel-based transport
  CryptoConnector.Binance/       # Public client + parsers + snapshots + store
tests/
  CryptoCore.Tests/              # Unit tests
```
