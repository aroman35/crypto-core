# CryptoCore — End‑to‑End (E2E) Tests Technical Specification

**Repository:** https://github.com/aroman35/crypto-core  
**Goal:** Validate the full integration path on a live exchange (Binance): _WS → Parsing → Transport → OrderBookStore → OrderBookL2_, plus trades, under real network conditions.

This document is the single source of truth for the E2E test project design, scope, run instructions, and acceptance criteria.

---

## 1. Scope and Objectives

### 1.1 In scope
- Real-time connectivity to **Binance** public WebSocket and REST endpoints (no credentials).
- Subscription to **3–5 symbols** for **L2 (depth)** and **trades**.
- Assembly of books via **snapshot + incremental L2** (single logic for both live and Tardis-like streams using the `IsSnapshot` bit).
- Verification of:
  - Correct book stitching (Binance rules: `U <= last+1 <= u` on the first accepted update after snapshot).
  - Top-of-book invariants and monotonicity of levels.
  - Reasonable ingest latency and buffer behavior in `OrderBookStore`.
  - Trades sanity (positive price/qty, event time range, symbol match).
  - Combined stream mode (optional).

### 1.2 Out of scope
- Private APIs; order management.
- Performance benchmarking beyond simple latency/queue metrics.
- Massive symbol sets (> 10 instruments).

---

## 2. Project Layout and Namespaces

A **new test project** will be added:
```
tests/
  CryptoCore.Tests.E2e/               ← xUnit/Shouldly-based E2E tests (no SpecFlow)
  CryptoCore.Tests.E2e.Specs/         ← SpecFlow-based E2E (Gherkin) — optional/next
```

> For this phase, we start with `CryptoCore.Tests.E2e` (xUnit). A follow-up phase can add SpecFlow (`CryptoCore.Tests.E2e.Specs`) using the same infrastructure & hooks.

**Dependencies (ProjectReferences):**
- `src/CryptoCore`
- `src/CryptoCore.Serialization`
- `src/CryptoStreaming.Abstractions`
- `src/CryptoStreaming.Channels`
- `src/CryptoConnector.Binance`

**Important:** E2E tests **must not** be part of the main CI pipeline. They are **manual-run only**.

---

## 3. Execution Modes & Exclusion from Main CI

### 3.1 Main CI (build & unit tests)
- Keep the existing CI unchanged.
- Ensure unit tests are executed with filter excluding E2E:  
  `--filter "Category!=E2E"`

### 3.2 Manual E2E Workflow (GitHub Actions)
- A **separate workflow** `e2e.yml` with `workflow_dispatch` trigger.
- Steps: restore, build, run E2E tests only, publish TRX and logs as artifacts.
- No secrets required (public endpoints only).
- Environment variables are used to tune symbols, durations, etc.

---

## 4. Environments, Configuration, and Defaults

E2E tests should be configurable via **environment variables** with sensible defaults:

| Variable               | Default                               | Description |
|------------------------|----------------------------------------|-------------|
| `E2E_BINANCE_WS`       | `wss://stream.binance.com:9443`        | Base WS endpoint. |
| `E2E_COMBINED`         | `true`                                  | Use combined stream mode. |
| `E2E_SYMBOLS`          | `BTCUSDT,ETHUSDT,SOLUSDT`              | CSV of symbols (UPPER ASCII). |
| `E2E_SNAPSHOT_LIMIT`   | `1000`                                  | Snapshot depth limit per side. |
| `E2E_DURATION_SEC`     | `30`                                    | Overall run budget. |
| `E2E_MIN_L2_UPDATES`   | `3`                                     | Min L2 updates per symbol before assertions. |
| `E2E_MIN_TRADES`       | `5`                                     | Min trades per symbol before assertions. |
| `E2E_MAX_LAG_MS`       | `1500`                                  | Max acceptable median ingest lag. |
| `E2E_LOG_LEVEL`        | `Information`                           | Optional logging verbosity. |

> Symbols are expected in the **Binance native** format (e.g., `BTCUSDT`). The E2E will convert them to `Symbol` with `Exchange.BinanceSpot` when creating books and subscriptions.

**Short slug policy for Exchange JSON:** all JSON I/O must use **short preset slugs** (`binance`, `binance-futures`, `okx`, `okx-futures`, `okx-swap`, `kucoin`, `kucoin-futures`). Inputs may also use enum names (e.g., `OKXSwap`), case‑insensitive.

---

## 5. Test Infrastructure (shared fixture)

We will implement a **collection fixture** (xUnit) that constructs and shares the following across tests:

- `ChannelMarketDataTransport` (single depth subscriber; multi-subscriber for trades).
- `BinancePublicClient` (supports **combined stream** and dynamic (un)subscribe).
- `BinanceSnapshotProvider` (REST snapshot → L2 snapshot update).
- `OrderBookStore` with `OrderBookStoreOptions`:
  - `MaxBufferPerSymbol` (e.g., 8192)
  - `SnapshotLimit` (from env)
  - `MaxRetryAttempts`, `InitialBackoff`, `MaxBackoff`
  - `LagMonitor` callback for per-symbol lag metrics

**Fixture responsibilities:**
1. Parse env vars.
2. Start client in chosen mode (combined/non-combined).
3. Start store (single depth subscription).
4. Add subscriptions for requested symbols (`@depth@100ms` and `@trade`).
5. Fetch and apply snapshots (via store) automatically.
6. Provide utility methods:
   - `WaitReadyAsync(Symbol sym, TimeSpan timeout, minUpdates, minTrades)`
   - Access to `OrderBookL2` via `store.TryGet(sym)`.
   - Rolling metrics from `LagMonitor`.

**Disposal:** `DisposeAsync()` must cleanly close WS, dispose store (draining pooled messages), dispose transport, and cancel any background loops.

---

## 6. Test Scenarios (xUnit)

### 6.1 Book assembly & top-of-book invariants
- Ready per symbol; assert top present; bid < ask; monotonic first 5; median lag ≤ threshold.

### 6.2 Binance stitching rule confirmation
- First incremental after snapshot satisfies `U <= L+1 <= u` (when both U and u present).

### 6.3 Trades sanity
- price/qty positive; event time sane; symbol match; soft checks vs top-of-book.

### 6.4 Combined stream smoke
- With `E2E_COMBINED=true`, verify both books + trades flow for 2 symbols.

### 6.5 Resource lifecycle
- No unhandled exceptions; no pooled leaks; websocket closed cleanly.

---

## 7. Non-Functional Requirements

- No secrets; bounded execution; retries/backoff; low allocations; structured logs.

---

## 8. Validation Rules — Details

Covered above; see order-book assembly doc for constructor rules.

---

## 9. Manual Run Instructions

```bash
export E2E_SYMBOLS=BTCUSDT,ETHUSDT,SOLUSDT
export E2E_DURATION_SEC=30
export E2E_COMBINED=true
export E2E_MAX_LAG_MS=1500

dotnet test tests/CryptoCore.Tests.E2e -c Release --filter "Category=E2E" -v n
```

---

## 10. Deliverables

- `tests/CryptoCore.Tests.E2e/` project with fixture, helpers and tests (marked `[Trait("Category","E2E")]`).
