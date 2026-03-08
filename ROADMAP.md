# Omnijure TDS — Roadmap

> **Philosophy:** Core before GUI. A trading platform with a beautiful UI but broken order execution is useless and dangerous. Every milestone prioritizes correctness, safety, and reliability of the trading engine over visual polish.

---

## Milestone v0.1 — Core Trading Engine (Alpha)

**Goal:** A working trading platform where a user can connect their Binance account, watch live market data, place orders (real or paper), and run a basic automated strategy — all without the app crashing.

**Target:** Q3 2025

### Architecture rules for v0.1
- No direct coupling between `Omnijure.Visual` and `Omnijure.Core` internals
- All data flows through `IEventBus`
- All services resolved through DI — no `new XxxService()` in `Program.cs`
- No feature ships without a unit test

---

### Issues

#### Exchange Layer
| # | Title | Priority |
|---|-------|----------|
| [#1](../../issues/1) | Define and implement order placement on `IExchangeClient` | P0 |
| [#2](../../issues/2) | WebSocket auto-reconnect with exponential backoff | P0 |
| [#3](../../issues/3) | Consolidate to a single combined WebSocket stream + `IExchangeClientFactory` | P1 |

#### Core — Technical Analysis
| # | Title | Priority |
|---|-------|----------|
| [#4](../../issues/4) | Expand TechnicalAnalysis: EMA, MACD, Bollinger Bands, VWAP, ATR, Stochastic + stateful RSI | P0 |

#### Core — Settings & Security
| # | Title | Priority |
|---|-------|----------|
| [#5](../../issues/5) | Wire `AppSettings` to app lifecycle — save and restore on startup/shutdown | P1 |
| [#6](../../issues/6) | Cross-platform credential storage + API key management UI | P1 |

#### Core — Paper Trading
| # | Title | Priority |
|---|-------|----------|
| [#7](../../issues/7) | Paper trading engine — simulated order fill and portfolio tracking | P0 |

#### Infrastructure
| # | Title | Priority |
|---|-------|----------|
| [#8](../../issues/8) | Introduce dependency injection — wire all services through `IServiceCollection` | P0 |
| [#9](../../issues/9) | Expand EventBus usage — decouple exchange, core and GUI through domain events | P0 |

#### Testing
| # | Title | Priority |
|---|-------|----------|
| [#10](../../issues/10) | Unit tests for all TechnicalAnalysis indicators | P1 |
| [#11](../../issues/11) | Unit tests for SharpScript — lexer, parser and interpreter | P1 |
| [#12](../../issues/12) | Unit tests for PaperTradingEngine — order fill, portfolio math, edge cases | P1 |
| [#13](../../issues/13) | Improve SettingsManager tests — round-trip, encryption assertion, error handling | P2 |

#### GUI (blocking only)
| # | Title | Priority |
|---|-------|----------|
| [#14](../../issues/14) | Order entry panel — buy/sell form with market and limit order support | P1 |
| [#15](../../issues/15) | Status bar — real connection state, Live/Paper mode indicator, reconnect feedback | P1 |

---

### Dependency graph

```
#8 (DI)
 └─► #7 (Paper Trading)
      └─► #12 (Paper Trading Tests)
 └─► #1 (Order Placement)
      └─► #14 (Order Entry GUI)

#9 (EventBus)
 └─► #2 (Reconnect)
      └─► #15 (Status Bar)
 └─► #3 (Combined Stream)

#4 (Indicators)
 └─► #10 (Indicator Tests)

#5 (Settings lifecycle)
 └─► #6 (Credential UI)
```

---

## Milestone v0.2 — Strategy Engine (planned)

- SharpScript: complete indicator built-ins, backtesting support
- Clojure strategy engine (`Omnijure.Mind`) stabilization and hot-reload
- Strategy marketplace (local)
- Multi-symbol chart tabs
- Alert system (price, indicator triggers)

## Milestone v0.3 — Multi-Exchange (planned)

- `IExchangeClient` implementations for Coinbase, Kraken
- Unified order book aggregation
- Cross-exchange arbitrage signal detection (`Omnijure.Oracle`)

## Milestone v1.0 — Production Ready (planned)

- Full automated trading with risk management rules
- Position sizing engine
- Drawdown circuit breaker
- Audit log for all order activity
- Packaging: single-file executable, auto-updater
