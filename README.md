<p align="center">
  <img src="https://i.imgur.com/5j7BRNw.png" alt="Omnijure TDS" width="100%" />
</p>

<h1 align="center">Omnijure TDS</h1>

<p align="center">
  <strong>The open-source trading terminal that Wall Street doesn't want you to have.</strong>
</p>

<p align="center">
  <img src="https://img.shields.io/badge/.NET_9-C%23_13-512BD4?style=for-the-badge&logo=dotnet" alt=".NET 9" />
  <img src="https://img.shields.io/badge/GPU-OpenGL_SkiaSharp-orange?style=for-the-badge" alt="GPU Accelerated" />
  <img src="https://img.shields.io/badge/License-MIT-green?style=for-the-badge" alt="MIT" />
  <img src="https://img.shields.io/badge/Status-Archived-red?style=for-the-badge" alt="Archived" />
</p>

> [!WARNING]
> **ARCHIVED PROJECT / PROYECTO ARCHIVADO**
>
> **English**: This project is no longer maintained. Due to a strategic decision, Omnijure TDS has been archived. All active development has shifted to the new successor project: **omnitrade**.
>
> **Español**: Este proyecto ya no recibirá mantenimiento. Por una decisión estratégica, Omnijure TDS ha sido archivado. Todo el desarrollo activo se ha trasladado al nuevo proyecto sucesor: **omnitrade**.


<p align="center">
  <a href="#quick-start">Quick Start</a> •
  <a href="#why-omnijure">Why Omnijure</a> •
  <a href="#features">Features</a> •
  <a href="#contributing">Contributing</a> •
  <a href="#support-the-project">Support</a>
</p>

---

## The Problem

Retail traders are stuck choosing between:

- **TradingView** — Great charts, zero automation, monthly fees.
- **Custom scripts** — Powerful but no UI, no visual feedback, painful debugging.
- **Institutional terminals** — Bloomberg at $24,000/year. Enough said.

There's no **free, open-source, desktop-native** platform that gives you professional charting, AI analysis, bot management, and a real-time order book — all in one window.

## The Solution

**Omnijure TDS** is a Trading Development Studio built from the ground up in C# with a custom GPU-accelerated rendering engine. No Electron. No web views. No monthly subscription. Just raw performance.

One executable. Real-time data. AI-powered analysis. Bot fleet management. A workspace you can arrange exactly how you want — like Visual Studio, but for trading.

---

## Why Omnijure

| | TradingView | MetaTrader | Bloomberg | **Omnijure TDS** |
|:---|:---:|:---:|:---:|:---:|
| Open source | ✗ | ✗ | ✗ | **✓** |
| Free | Freemium | ✓ | ✗ ($24k/yr) | **✓** |
| AI assistant | ✗ | ✗ | ✗ | **✓** |
| Custom bots | ✗ | Limited | ✗ | **✓** |
| GPU-accelerated | ✗ | ✗ | ✗ | **✓** |
| Dockable workspace | ✗ | ✗ | ✓ | **✓** |
| Desktop native | ✗ | ✓ | ✓ | **✓** |
| Extensible | ✗ | Limited | ✗ | **✓** |

---

## Features

### Real-Time Charting
Candlestick, line, and area charts rendered at 144Hz+ with SkiaSharp on OpenGL. Color-coded wicks, SMA indicators, crosshair, drawing tools, and smooth zoom/scroll. Every pixel is custom-rendered — no charting libraries, no compromises.

### AI-Powered Analysis
An integrated AI assistant that understands market context. Ask about patterns, run divergence scans across pairs, get entry/target/stop signals — all inside the platform. Pluggable LLM backend (GPT-4o, local models, or your own).

### Live Order Book & Trades
Dual-column order book with depth bars, real-time spread indicator, and a live trade feed — all streamed via WebSocket from Binance. See the market as it happens.

### Portfolio & Bot Management
Monitor multiple accounts, track active bots (Grid, DCA, Scalper), view holdings with real-time PnL, and manage datasets for backtesting. Everything in one panel.

### Positions Tracking
Open positions table with leverage badges, entry vs mark price, unrealized PnL, ROE percentage, and one-click close actions.

### Professional Workspace
Visual Studio-style docking system with diamond compass guides, tabbed panels, tab tear-off, panel resize, and floating windows. Arrange your workspace exactly how you work.

---

## Quick Start

```bash
git clone https://github.com/jackby03/omnijure-trading-platform.git
cd omnijure-trading-platform
dotnet run -c Release --project src/Omnijure.Visual
```

> **Requirements:** .NET 9.0 SDK • OpenGL 3.3+ GPU • Windows 10/11

The app connects to Binance public APIs on launch — no API key required for market data.

---

## Architecture

```
Omnijure.Core       Data layer — RingBuffers, SIMD math, WebSocket streaming
Omnijure.Visual     GPU rendering — Charts, Panels, Docking, Drawing tools
Omnijure.Mind       AI/ML — Strategy scripting, LLM integration
Omnijure.Oracle     Prediction — Market analysis models
```

Built with **C# 13** on **.NET 9**. Custom rendering with **SkiaSharp + Silk.NET**. Zero-allocation data structures. SIMD-accelerated technical analysis. No UI frameworks — every pixel is ours.

---

## Contributing

Omnijure is in active development and we're looking for contributors who want to build the future of open-source trading tools.

### Areas Where You Can Help

| Area | Skills | Impact |
|:---|:---|:---|
| **Charting** | C#, SkiaSharp, math | More indicators, chart types, annotations |
| **Exchange integration** | WebSocket, REST APIs | Bybit, OKX, Coinbase, Kraken support |
| **Bot engine** | Algorithms, backtesting | Strategy execution and simulation |
| **AI/ML** | LLM integration, NLP | Smarter market analysis and signals |
| **UI/UX** | Design, SkiaSharp rendering | Better panels, themes, accessibility |
| **Testing** | .NET testing, CI/CD | Reliability and automated builds |
| **Documentation** | Technical writing | Guides, API docs, tutorials |

### How to Start

1. Fork the repo and clone locally
2. Pick an issue labeled `good first issue` or `help wanted`
3. Open a PR with your changes
4. Join the discussion in Issues

We value clean code, meaningful commits, and respectful collaboration.

---

## Support the Project

Omnijure is free and open source. If you believe in democratizing access to professional trading tools, here's how you can help:

⭐ **Star this repo** — It helps others discover the project.

🍴 **Fork & contribute** — Code, docs, design, testing — everything counts.

📣 **Share it** — Tell traders, developers, and communities who need better tools.

💬 **Open issues** — Bug reports, feature requests, and ideas are all welcome.

🤝 **Partner with us** — If you're an exchange, data provider, or fintech company interested in integration, reach out.

### Fund Development

If you want to directly support ongoing development:

<p align="center">
  <a href="https://ko-fi.com/jackby03"><img src="https://img.shields.io/badge/Ko--fi-Support_on_Ko--fi-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white" alt="Ko-fi" /></a>
  <a href="https://publishers.basicattentiontoken.org/en/c/jackby03"><img src="https://img.shields.io/badge/BAT-Brave_Rewards-FB542B?style=for-the-badge&logo=brave&logoColor=white" alt="BAT" /></a>
</p>

Every contribution — code, feedback, or a coffee — helps keep this project alive and independent.

---

## License

MIT License — free to use, modify, and distribute. See [LICENSE](LICENSE).

---

<p align="center">
  <strong>Stop renting your trading tools. Own them.</strong>
</p>

<p align="center">
  Built by <a href="https://github.com/jackby03"><strong>@jackby03</strong></a>
</p>
