
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
   - `WaitReadyAsync(Symbol sym, TimeSpan timeout, minUpdates, minTrades)` → wait for snapshot applied & buffers flushed & counters reached.
   - Access to `OrderBookL2` instances via `store.TryGet(sym)`.
   - Access to simple rolling metrics collected from `LagMonitor`.

**Disposal:** `DisposeAsync()` must cleanly close WS, dispose store (draining pooled messages), dispose transport, and cancel any background loops.

---

## 6. Test Scenarios (xUnit)

### 6.1 Book assembly & top-of-book invariants
**Category:** `E2E`  
**Given** the fixture is started with symbols S = {BTCUSDT, ETHUSDT, SOLUSDT}  
**When** we wait until each book is `Ready` (snapshot applied) and we have at least `E2E_MIN_L2_UPDATES` post-snapshot updates  
**Then** for each symbol:
- `LastUpdateId > 0`.
- `(bestBidPx, bestBidQty)` and `(bestAskPx, bestAskQty)` are set and positive.
- `bestBidPx < bestAskPx`.
- First 5 bid levels are monotonic non-increasing by price; first 5 ask levels are monotonic non-decreasing.
- Median ingest lag ≤ `E2E_MAX_LAG_MS` (computed from `LagMonitor` events).

### 6.2 Binance stitching rule confirmation
**When** the first incremental update after snapshot is applied  
**Then** it must satisfy `U <= lastFromSnapshot + 1 <= u` (if both U and u are present).

### 6.3 Trades sanity
**When** we collect at least `E2E_MIN_TRADES` per symbol within `E2E_DURATION_SEC`  
**Then** each trade satisfies:
- `price > 0`, `qty > 0`;
- `trade.Symbol` matches subscription;
- `EventTime` is not in the future and not older than 2 minutes;
- (Soft) A subset of buy-aggressor trades should not be strictly below `bestAsk`; sell-aggressor not strictly above `bestBid`. This check is **non-fatal** (warn only).

### 6.4 Combined stream smoke
**Given** `E2E_COMBINED=true`  
**When** depth and trades streams are active for 2 symbols  
**Then** both books and trades flow without errors; at least `2` L2 updates & `3` trades per symbol arrive in time.

### 6.5 Resource lifecycle
At test completion:
- No unhandled exceptions from background loops.
- `OrderBookStore.DisposeAsync` drains any buffered `L2UpdatePooled` (no leaks).
- WebSocket session is closed cleanly.

---

## 7. Non-Functional Requirements

- **No secrets** required.
- **Time-bounded** execution: default `E2E_DURATION_SEC=30` must be sufficient to pass on a typical network.
- **Flakiness minimization:** guarded waits, reasonable retries/backoff for snapshots/subscribe, skip (or fail with informative message) on persistent network failure.
- **Low allocations:** rely on pooled L2 path; only one depth subscriber in transport.
- **Logging:** structured logs with symbol tags and timings; emit book and trade counters at the end of a test.

---

## 8. Validation Rules (Details)

### 8.1 Book-level validations
- **Top-of-book set:** both sides present.
- **Top-of-book positive qty**.
- **Bid < Ask** (strict inequality).
- **Monotonicity of levels:**
  - For bids: `p1 >= p2 >= p3 >= ...`
  - For asks: `p1 <= p2 <= p3 <= ...`
- **Snapshot+Delta stitch:** When snapshot `lastId = L`, the first accepted incremental batch must satisfy `U <= L+1 <= u`. Thereafter, `PrevLastUpdateId == LastUpdateId(previous)` when fields are present.
- **Update application:** Any delta with zero qty removes the level; positive qty upserts/overwrites.

### 8.2 Trades validations
- `price > 0`, `qty > 0`.
- `EventTime` within `(now - 2min, now + 5s)`.
- Symbol name matches the subscription (case-insensitive compare only on asset pair, venue is implied by transport).

### 8.3 Latency metrics
- `LagMonitor` calculates per-symbol samples of ingest lag: `nowMs - update.EventTimeMs`.
- We compute **median** lag and assert it is ≤ `E2E_MAX_LAG_MS`.
- Also track the **max** buffer size observed before snapshot application; it must be less than `MaxBufferPerSymbol` (configured).

---

## 9. Manual Run Instructions

### 9.1 Local
```bash
# From repo root:
export E2E_SYMBOLS=BTCUSDT,ETHUSDT,SOLUSDT
export E2E_DURATION_SEC=30
export E2E_COMBINED=true
export E2E_MAX_LAG_MS=1500

dotnet test tests/CryptoCore.Tests.E2e -c Release --filter "Category=E2E" -v n
```

### 9.2 GitHub Actions (manual workflow)
Create `.github/workflows/e2e.yml` with `workflow_dispatch` (do not add any secrets). Steps:
- checkout
- setup .NET 9
- restore
- build
- run: `dotnet test tests/CryptoCore.Tests.E2e -c Release --filter "Category=E2E"`
- upload TRX & logs as artifacts

> The main CI must continue to run unit tests with `--filter "Category!=E2E"` and **must not** trigger E2E.

---

## 10. Deliverables

- `tests/CryptoCore.Tests.E2e/` project with:
  - Collection fixture that wires: transport, client (combined or not), snapshot provider, store.
  - Helper utilities to wait for readiness and to collect latency metrics.
  - Test classes covering scenarios 6.1–6.5 with `[Trait("Category","E2E")]`.
  - README.md in the project folder with a quick start and env var reference.
- Optional later: `tests/CryptoCore.Tests.E2e.Specs/` SpecFlow project (Gherkin), sharing the same infrastructure via hooks/world.

---

## 11. Risks and Mitigations

- **Network flakiness / firewall** — Guard with timeouts and retries; skip with clear output if endpoints unreachable.
- **Rate-limits** — Limit symbols to ≤ 5; prefer combined streams.
- **API shape changes** — Parsing is decoupled; E2E errs with diagnostic logs if schema drifts.
- **Clock skew** — Use generous future-time allowance (up to +5s).

---

## 12. Future Extensions

- Add SpecFlow project with LivingDoc HTML reports.
- Add OKX/Bybit connectors to the same E2E suite via tags.
- Add perf smoke: measure CPU/allocs for a 30s window locally.
- Persist sampled books/trades to CSV for offline triage when failures occur.

---

### Appendix A — Where to find components in the repo

- Core primitives & book: `src/CryptoCore/…`
- Serialization: `src/CryptoCore.Serialization/…`
- Streaming abstractions: `src/CryptoStreaming.Abstractions/…`
- Channel transport (reference impl): `src/CryptoStreaming.Channels/…`
- Binance connector, parsers, snapshots, store: `src/CryptoConnector.Binance/…`
