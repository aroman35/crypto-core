# CryptoCore

High-performance, allocation-free primitives for crypto trading in .NET:

- **Asset** — fixed-size, ASCII-only value type (struct) with zero‑allocation parsing and fast equality.
- **Symbol** — compact value type that combines `BaseAsset`, `QuoteAsset`, and `Exchange`.  
  Parses **native exchange formats** (Binance, OKX, KuCoin, Bybit) and emits the **same native format** in `ToString()`.
- **Exchange** — flag-based enum with rich helper extensions (venue, market, contract attributes, margin type).

Companion package **CryptoCore.Json** adds **System.Text.Json** and **Newtonsoft.Json** converters for all types.

> Target framework: **.NET 9**

---

## Packages

| Package           | What’s inside                                              |
|-------------------|------------------------------------------------------------|
| `CryptoCore`      | Asset, Symbol, Exchange, helper extensions                 |
| `CryptoCore.Json` | JSON converters for System.Text.Json and Newtonsoft.Json   |

---

## Features

- ✅ **Zero allocations** on parsing and comparisons (uses `Span<T>`, `stackalloc`, caches strings on first `ToString()`).
- ✅ **Native exchange formats** for `Symbol`:
  - Binance/Bybit: `BTCUSDT`
  - OKX Spot: `BTC-USDT`
  - OKX Perp/Swap: `BTC-USDT-SWAP`
  - Delivery (OKX-style): `BTC-USD-20241227`
- ✅ **Flexible parsing**:
  - `BASE-QUOTE@Preset` (e.g., `ETH-USDT@OKXSpot`)
  - Delimiter-less tickers via **stable-coin suffix** (default: `USDT`, `USDC`, `BUSD`, `TUSD`, `USDP`, `DAI`, `FDUSD`, `USD`).
  - Extend at runtime: `Symbol.AddStablecoin("FDUSD")`.
- ✅ Strong **`Exchange` helpers** (e.g., `.IsSpot()`, `.IsPerpetual()`, `.IsUsdMargined()`, `.TryGetSingleVenue()`).
- ✅ Converters for `System.Text.Json` and **Newtonsoft.Json** (serialize as string; deserialize from all supported formats).
- ✅ Full **xUnit + Shouldly** test suite.

---

## Quick start

### Parse & format

```csharp
using CryptoCore.Root;

var binance = Symbol.Parse("BTCUSDT");       // delimiter-less; defaults to BinanceSpot
var okxSwap = Symbol.Parse("BTC-USDT-SWAP"); // OKX perpetual/swap
var explicitP = Symbol.Parse("ETH-USDT@OKXSpot");

binance.For(Exchange.OKXSwap).ToString();  // "BTC-USDT-SWAP"
explicitP.ToString();                      // "ETH-USDT" (native OKX spot format)
```

### JSON (System.Text.Json)

```csharp
using System.Text.Json;
using CryptoCore.Json.SystemTextJson;
using CryptoCore.Root;

var options = new JsonSerializerOptions().AddCryptoCoreConverters();

var s = Symbol.Parse("BTCUSDT").For(Exchange.BinanceFutures);
var payload = JsonSerializer.Serialize(s, options);     // "BTCUSDT"
var back    = JsonSerializer.Deserialize<Symbol>(payload, options);
```

### JSON (Newtonsoft.Json)

```csharp
using Newtonsoft.Json;
using CryptoCore.Json.Newtonsoft;
using CryptoCore.Root;

var settings = new JsonSerializerSettings().AddCryptoCoreConverters();

var s = Symbol.Parse("ETH-USDT@OKXSpot");
var payload = JsonConvert.SerializeObject(s, settings); // "ETH-USDT"
var back    = JsonConvert.DeserializeObject<Symbol>(payload, settings);
```
## License

[MIT](LICENSE)
