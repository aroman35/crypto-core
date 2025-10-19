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

---

## Project layout

```
/src
  /CryptoCore           # core value types and extensions
  /CryptoCore.Json      # System.Text.Json & Newtonsoft converters
/tests
  /CryptoCore.Tests     # xUnit + Shouldly tests
```

---

## Build, test, pack

> Requires **.NET 9 SDK**

```bash
# Restore & build
dotnet build -c Release

# Run tests
dotnet test -c Release

# Pack both libraries
dotnet pack ./src/CryptoCore/CryptoCore.csproj -c Release -o ./artifacts
dotnet pack ./src/CryptoCore.Json/CryptoCore.Json.csproj -c Release -o ./artifacts
```

### Versioning

You can set the version at pack time:

```bash
dotnet pack ./src/CryptoCore/CryptoCore.csproj -c Release -o ./artifacts -p:Version=0.2.0
dotnet pack ./src/CryptoCore.Json/CryptoCore.Json.csproj -c Release -o ./artifacts -p:Version=0.2.0
```

Alternatively, set `<Version>` inside the project file(s).

---

## NuGet metadata (csproj)

Add these to each `.csproj` to enrich the package (readme will be embedded):

```xml
<PropertyGroup>
  <PackageReadmeFile>README.md</PackageReadmeFile>
  <PackageProjectUrl>https://github.com/your-org/your-repo</PackageProjectUrl>
  <RepositoryUrl>https://github.com/your-org/your-repo</RepositoryUrl>
  <RepositoryType>git</RepositoryType>
  <PackageLicenseExpression>MIT</PackageLicenseExpression>
  <Authors>Your Name or Org</Authors>
  <PackageTags>crypto;trading;exchange;symbol;asset</PackageTags>
  <IncludeSymbols>true</IncludeSymbols>
  <SymbolPackageFormat>snupkg</SymbolPackageFormat>
</PropertyGroup>

<ItemGroup>
  <None Include="$(SolutionDir)README.md" Pack="true" PackagePath="\" />
</ItemGroup>
```

> If you place `README.md` at repo root, `$(SolutionDir)` resolves to that path when packing from inside the solution.

---

## Publish to Yandex Cloud Registry (NuGet)

1) **Add the Yandex Cloud NuGet source** (one-time on your machine/runner):

```bash
dotnet nuget add source \
  "https://registry.yandexcloud.net/nuget/v3/<REGISTRY_ID>/index.json" \
  -n yc-reg \
  -u "$REGISTRY_USERNAME" \
  -p "$REGISTRY_PASSWORD" \
  --store-password-in-clear-text
```

2) **Push packages** (after `dotnet pack`):

```bash
# Push .nupkg; credentials from the source entry will be used
dotnet nuget push ./artifacts/CryptoCore.*.nupkg -s yc-reg --skip-duplicate
dotnet nuget push ./artifacts/CryptoCore.Json.*.nupkg -s yc-reg --skip-duplicate
```

3) **Increment version and republish**

```bash
# bump version at pack time
dotnet pack ./src/CryptoCore/CryptoCore.csproj -c Release -o ./artifacts -p:Version=0.2.1
dotnet pack ./src/CryptoCore.Json/CryptoCore.Json.csproj -c Release -o ./artifacts -p:Version=0.2.1

# push the new version
dotnet nuget push ./artifacts/CryptoCore.0.2.1.nupkg -s yc-reg
dotnet nuget push ./artifacts/CryptoCore.Json.0.2.1.nupkg -s yc-reg
```

> The registry requires NuGet v3 API and basic credentials. Store your credentials in CI as secrets and inject via env vars.

---

## CI (example)

Minimal GitHub Actions workflow to pack & push to Yandex Cloud Registry when you push a version tag (`v*`):

```yaml
name: publish
on:
  push:
    tags:
      - 'v*'

jobs:
  pack-and-push:
    runs-on: ubuntu-latest
    steps:
      - uses: actions/checkout@v4
        with: { fetch-depth: 0 }

      - uses: actions/setup-dotnet@v4
        with:
          dotnet-version: '9.0.x'

      - name: Restore & build
        run: dotnet build -c Release

      - name: Pack
        run: |
          dotnet pack ./src/CryptoCore/CryptoCore.csproj -c Release -o ./artifacts -p:Version=${GITHUB_REF_NAME#v}
          dotnet pack ./src/CryptoCore.Json/CryptoCore.Json.csproj -c Release -o ./artifacts -p:Version=${GITHUB_REF_NAME#v}

      - name: Add YC NuGet source
        run: |
          dotnet nuget add source "https://registry.yandexcloud.net/nuget/v3/${{ secrets.YC_REGISTRY_ID }}/index.json" \
            -n yc-reg -u "${{ secrets.YC_REGISTRY_USER }}" -p "${{ secrets.YC_REGISTRY_PASSWORD }}" \
            --store-password-in-clear-text

      - name: Push to Yandex Cloud Registry
        run: |
          dotnet nuget push "./artifacts/*.nupkg" -s yc-reg --skip-duplicate
```

Set these repository **secrets**:
- `YC_REGISTRY_ID` — your Yandex Cloud Registry ID
- `YC_REGISTRY_USER`, `YC_REGISTRY_PASSWORD` — credentials for the registry

> If you use service accounts, provide a username/password pair configured for the registry. Refer to your YC account setup.

---

## License

[MIT](LICENSE)
