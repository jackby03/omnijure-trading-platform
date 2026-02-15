using Omnijure.Core.DataStructures;
using Omnijure.Core.Network;
using Omnijure.Core.Scripting;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual;

public class ChartTabState
{
    public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
    public string Symbol { get; set; } = "BTCUSDT";
    public string Timeframe { get; set; } = "1m";
    public ChartType ChartType { get; set; } = ChartType.Candles;

    // Data
    public RingBuffer<Candle> Buffer { get; set; }
    public OrderBook OrderBook { get; set; }
    public RingBuffer<MarketTrade> Trades { get; set; }
    public BinanceClient Connection { get; set; }

    // Viewport
    public float Zoom { get; set; } = 1.0f;
    public int ScrollOffset { get; set; }
    public bool AutoScaleY { get; set; } = true;
    public float ViewMinY { get; set; }
    public float ViewMaxY { get; set; }

    // Drawing tools
    public Omnijure.Visual.Drawing.DrawingToolState DrawingState { get; set; } = new();

    // Script engine
    public ScriptManager Scripts { get; set; } = new();

    // Default script source (replaces hardcoded SMA 20/50)
    public const string DefaultScript = """
        //@version=1
        indicator("Moving Averages", overlay=true)
        s20 = sma(close, 20)
        s50 = sma(close, 50)
        plot(s20, "SMA(20)", color=#FFD700)
        plot(s50, "SMA(50)", color=#00BCD4)
        """;

    public ChartTabState(string symbol, string timeframe)
    {
        Symbol = symbol;
        Timeframe = timeframe;
        Buffer = new RingBuffer<Candle>(4096);
        OrderBook = new OrderBook();
        Trades = new RingBuffer<MarketTrade>(1024);
        Connection = new BinanceClient(Buffer, OrderBook, Trades);

        // Add default indicator script
        Scripts.AddScript(DefaultScript, "Moving Averages");
    }
}
