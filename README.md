# Omnijure 🚀

> A high-performance, AI-powered cryptocurrency trading platform built with C# and Clojure

[![License: MIT](https://img.shields.io/badge/License-MIT-blue.svg)](https://opensource.org/licenses/MIT)
[![.NET](https://img.shields.io/badge/.NET-9.0-purple.svg)](https://dotnet.microsoft.com/)
[![Clojure](https://img.shields.io/badge/Clojure-1.11-green.svg)](https://clojure.org/)

Omnijure is a professional-grade cryptocurrency trading platform that combines real-time market data visualization, advanced technical analysis, and AI-powered decision-making capabilities. Built with a unique three-layer architecture (Metal/Mind/Visual), it delivers institutional-quality trading tools with a modern, responsive interface.

![Omnijure Platform](https://imgur.com/a/pwWSUTV)

## ✨ Features

### 📊 Advanced Charting
- **Multiple Chart Types**: Candlestick, Line, and Area charts
- **Real-time Updates**: WebSocket-based live data from Binance
- **Interactive Controls**: Zoom, pan, and scroll through historical data
- **Professional UI**: TradingView-inspired interface with Binance-style aesthetics

### 🔍 Smart Asset Search
- **TradingView-Style Modal**: Centered search with keyboard navigation
- **Real-time Filtering**: Instant results as you type
- **Rich Information**: Current prices, 24h changes, and exchange data
- **Keyboard Shortcuts**: `Ctrl+K` to open, arrows to navigate, Enter to select

### 📈 Market Data
- **Order Book**: Real-time bid/ask depth visualization
- **Market Trades**: Live trade feed with price and volume
- **Watchlist**: Track your favorite trading pairs
- **Multiple Timeframes**: 1m, 5m, 15m, 1h, 4h, 1d intervals

### 🤖 AI Integration (Omnijure.Mind)
- **Clojure-based Engine**: Embedded ClojureCLR for strategy execution
- **BERT Integration**: Sentiment analysis from news and social media
- **Custom Strategies**: Write trading logic in Clojure

### ⚡ Performance
- **SIMD Optimization**: Hardware-accelerated calculations
- **Ring Buffers**: Efficient data structures for streaming data
- **GPU Rendering**: SkiaSharp with OpenGL backend
- **Async Architecture**: Non-blocking WebSocket connections

## 🏗️ Architecture

Omnijure follows a three-layer architecture:

```
┌─────────────────────────────────────────┐
│         Omnijure.Visual (C#)            │  ← UI Layer
│   Silk.NET + SkiaSharp + OpenGL         │
├─────────────────────────────────────────┤
│         Omnijure.Mind (Clojure)         │  ← AI/Strategy Layer
│      ClojureCLR + BERT Models           │
├─────────────────────────────────────────┤
│         Omnijure.Core (C#)              │  ← Data Layer
│   WebSockets + REST API + SIMD          │
└─────────────────────────────────────────┘
```

### Layer Responsibilities

- **Metal (Core)**: Data acquisition, WebSocket management, market data structures
- **Mind (AI)**: Strategy execution, sentiment analysis, decision-making
- **Visual (UI)**: Rendering, user interaction, real-time visualization

## 🚀 Getting Started

### Prerequisites

- [.NET 9.0 SDK](https://dotnet.microsoft.com/download/dotnet/9.0)
- Windows 10/11 (Linux/macOS support coming soon)
- GPU with OpenGL 3.3+ support

### Installation

1. **Clone the repository**
   ```bash
   git clone https://github.com/yourusername/omnijure.git
   cd omnijure
   ```

2. **Restore dependencies**
   ```bash
   dotnet restore
   ```

3. **Build the project**
   ```bash
   dotnet build
   ```

4. **Run the application**
   ```bash
   dotnet run --project src/Omnijure.Visual/Omnijure.Visual.csproj
   ```

## 🎮 Usage

### Keyboard Shortcuts

| Shortcut | Action |
|----------|--------|
| `Ctrl+K` | Open asset search modal |
| `↑` `↓` | Navigate search results |
| `Enter` | Select asset |
| `Esc` | Close modal |
| `Mouse Wheel` | Zoom in/out on chart |
| `Click + Drag` | Pan chart horizontally |

### Changing Assets

1. Click the search box in the toolbar (shows current asset, e.g., "BTCUSDT")
2. Type to filter available trading pairs
3. Use arrow keys or mouse to select
4. Press Enter or click to switch

### Changing Timeframes

Use the interval dropdown to switch between:
- **1m**: 1-minute candles
- **5m**: 5-minute candles
- **15m**: 15-minute candles
- **1h**: 1-hour candles
- **4h**: 4-hour candles
- **1d**: Daily candles

## 🛠️ Development

### Project Structure

```
omnijure/
├── src/
│   ├── Omnijure.Core/          # Data layer (C#)
│   │   ├── DataStructures/     # Market data types
│   │   ├── Network/            # WebSocket & REST clients
│   │   └── Math/               # SIMD operations
│   ├── Omnijure.Mind/          # AI layer (Clojure)
│   │   └── scripts/            # Clojure strategies
│   ├── Omnijure.Visual/        # UI layer (C#)
│   │   ├── Rendering/          # Chart renderers
│   │   └── Program.cs          # Main entry point
│   └── Omnijure.Oracle/        # ML models (Python interop)
└── docs/                       # Documentation
```

### Building from Source

```bash
# Debug build
dotnet build

# Release build
dotnet build -c Release

# Run tests
dotnet test

# Publish standalone executable
dotnet publish -c Release -r win-x64 --self-contained
```

### Adding Custom Strategies

Create Clojure scripts in `src/Omnijure.Mind/scripts/`:

```clojure
(ns my-strategy
  (:require [omnijure.core :as core]))

(defn analyze [candles]
  (let [sma-20 (core/sma candles 20)
        sma-50 (core/sma candles 50)]
    (if (> sma-20 sma-50)
      :buy
      :sell)))
```

## 🤝 Contributing

Contributions are welcome! Please feel free to submit a Pull Request. For major changes, please open an issue first to discuss what you would like to change.

### Development Guidelines

1. Follow C# coding conventions
2. Write unit tests for new features
3. Update documentation as needed
4. Ensure all tests pass before submitting PR

## 📝 License

This project is licensed under the MIT License - see the [LICENSE](LICENSE) file for details.

## 🙏 Acknowledgments

- **Binance** for providing free market data API
- **TradingView** for UI/UX inspiration
- **Silk.NET** for cross-platform windowing and input
- **SkiaSharp** for high-performance 2D graphics
- **ClojureCLR** for embedded Lisp capabilities

## 📧 Contact

- **Author**: Jackby03
- **Email**: jackby03@protonmail.com
- **Twitter**: [@ijackby03](https://twitter.com/ijackby03)

## ⚠️ Disclaimer

This software is for educational purposes only. Use at your own risk. Cryptocurrency trading carries significant financial risk. Always do your own research before making investment decisions.

---

**Made with ❤️ and C#**
