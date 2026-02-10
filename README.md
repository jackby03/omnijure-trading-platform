# Omnijure

**Institutional-Grade AI Strategy Platform**

Omnijure is a minimalist, high-performance desktop environment designed for AI agents to architect, configure, and monitor institutional-grade trading strategies. It provides a unified interface for real-time market data visualization and seamless bot deployment.

---

## 🏛️ Core Pillars

### **Strategy Forge**
Powered by an embedded ClojureCLR engine, Omnijure allows for the rapid design of complex, non-deterministic trading logic. AI agents can iterate on strategies with immediate execution feedback.

### **Unified Execution**
Eliminate fragmentation. Configure, deploy, and manage entire bot fleets from a single, high-performance workstation.

### **High-Performance Visuals**
Built with a custom SkiaSharp rendering engine on OpenGL. Real-time sub-millisecond charting, order book depth, and live trade flows optimized for 144Hz+ monitoring.

---

## ⚡ Tech Stack

- **Metal (Core)**: C# 13, SIMD-optimized math, zero-allocation RingBuffers.
- **Mind (AI)**: Embedded Clojure 1.12, BERT-based sentiment orchestration.
- **Visual (UI)**: SkiaSharp, Silk.NET, Raw OpenGL.

---

## 🚀 Quick Start

### Prerequisites
- .NET 9.0 Runtime
- OpenGL 3.3+ capable GPU

### Installation
```bash
# Clone the repository
git clone https://github.com/jackby03/omnijure.git

# Build & Run
dotnet run -c Release --project src/Omnijure.Visual
```

---

## ⌨️ Orchestration

| Action | Command |
| :--- | :--- |
| **Search Asset** | `Ctrl + K` |
| **Switch Strategy** | `Ctrl + S` |
| **Toggle HUD** | `Ctrl + H` |
| **Reset View** | `Esc` |

---

## 📝 License
MIT License.

---
**Jackby03** • Institutional AI Trading Ecosystem
