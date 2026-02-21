using System;
using SkiaSharp;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Widgets.Panels;

public class ConsoleRenderer : IPanelRenderer
{
    public string PanelId => PanelDefinitions.LOGS;
    private const float ConsoleFixedHeaderH = 30;
    private const float ConsoleLineH = 15;
    private float _contentHeight = 0;

    public void Render(SKCanvas canvas, SKRect rect, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontMono = new SKFont(SKTypeface.FromFamilyName("Cascadia Code", SKFontStyle.Normal), 10);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Cascadia Code", SKFontStyle.Normal), 9);
            using var fontHeader = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);

            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;
            float width = rect.Width;
            float height = rect.Height;
            float px = 6;
            float lineH = ConsoleLineH;

            // SCROLLABLE LOG LINES
            float logsTop = ConsoleFixedHeaderH;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, logsTop, width, height));
            canvas.Translate(0, -scrollY);

            float y = logsTop + 4;

            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:14:02", "INFO", "WebSocket connected to wss://stream.binance.com", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:14:02", "INFO", "Subscribing to btcusdt@trade, btcusdt@depth20", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:14:03", "DEBUG", "OrderBook snapshot received: 500 bids, 500 asks", new SKColor(120, 140, 255));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:14:05", "INFO", "Grid Bot #1 initialized: BTCUSDT 20 levels", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:14:05", "INFO", "DCA Bot #2 started: ETHUSDT interval=4h", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:14:08", "DEBUG", "Latency check: REST 23ms, WS 8ms", new SKColor(120, 140, 255));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:15:12", "INFO", "Grid Bot #1: BUY filled 0.001 BTC @ $68,842.00", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:15:14", "INFO", "Grid Bot #1: SELL order placed 0.001 BTC @ $68,862.00", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:16:30", "WARN", "Rate limit approaching: 1180/1200 weight used", new SKColor(255, 180, 50));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:16:45", "INFO", "Scalper #3: Entry signal SOLUSDT short @ $153.80", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:17:01", "DEBUG", "RSI(14) BTCUSDT=58.3, ETHUSDT=52.1, SOLUSDT=44.7", new SKColor(120, 140, 255));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:17:22", "ERR", "Scalper #3: SL hit SOLUSDT -0.24% ($-1.12)", new SKColor(239, 83, 80));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:18:05", "INFO", "DCA Bot #2: Accumulated 0.02 ETH @ avg $3,418.50", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:18:30", "WARN", "High volatility detected: BTC 1m ATR > 2x avg", new SKColor(255, 180, 50));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:19:10", "INFO", "Grid Bot #1: SELL filled 0.001 BTC @ $68,862.00 +$0.02", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:19:11", "INFO", "Grid Bot #1: BUY order placed 0.001 BTC @ $68,842.00", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:19:45", "DEBUG", "Memory: 248 MB | CPU: 12% | GPU: 34%", new SKColor(120, 140, 255));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:20:02", "INFO", "Alert triggered: SOLUSDT RSI > 70 (current: 71.2)", new SKColor(46, 204, 113));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:20:15", "ERR", "WS reconnect attempt 1/5: timeout after 5000ms", new SKColor(239, 83, 80));
            DrawConsoleLine(canvas, paint, fontMono, fontSmall, px, width, ref y, lineH,
                "09:20:16", "INFO", "WS reconnected successfully (latency: 12ms)", new SKColor(46, 204, 113));

            _contentHeight = y - logsTop;
            
            canvas.Restore();

            // FIXED FILTER BAR
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, 0, width, ConsoleFixedHeaderH, paint);

            float fy = 4;
            paint.Color = new SKColor(22, 26, 34);
            canvas.DrawRoundRect(new SKRect(px, fy, width - px, fy + 20), 4, 4, paint);

            paint.Color = new SKColor(85, 90, 100);
            canvas.DrawText("ALL", px + 8, fy + 14, fontHeader, paint);
            paint.Color = new SKColor(46, 204, 113);
            canvas.DrawText("INFO", px + 40, fy + 14, fontHeader, paint);
            paint.Color = new SKColor(255, 180, 50);
            canvas.DrawText("WARN", px + 76, fy + 14, fontHeader, paint);
            paint.Color = new SKColor(239, 83, 80);
            canvas.DrawText("ERR", px + 118, fy + 14, fontHeader, paint);
            paint.Color = new SKColor(120, 140, 255);
            canvas.DrawText("DEBUG", px + 150, fy + 14, fontHeader, paint);

            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(px, ConsoleFixedHeaderH - 1, width - px, ConsoleFixedHeaderH - 1, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public float GetContentHeight()
    {
        return _contentHeight > 0 ? _contentHeight : 300; // 20 * 15
    }

    private static void DrawConsoleLine(SKCanvas canvas, SKPaint paint, SKFont monoFont, SKFont smallFont,
        float px, float width, ref float y, float lineH,
        string time, string level, string message, SKColor levelColor)
    {
        paint.Color = new SKColor(70, 75, 85);
        canvas.DrawText(time, px, y + lineH - 3, smallFont, paint);

        float levelX = px + 62;
        float badgeW = smallFont.MeasureText(level) + 6;
        float badgeH = 12;
        float badgeY = y + 1;
        paint.Color = new SKColor(levelColor.Red, levelColor.Green, levelColor.Blue, 25);
        canvas.DrawRoundRect(new SKRect(levelX, badgeY, levelX + badgeW, badgeY + badgeH), 2, 2, paint);
        paint.Color = levelColor;
        canvas.DrawText(level, levelX + 3, y + lineH - 3, smallFont, paint);

        float msgX = levelX + badgeW + 6;
        float maxMsgW = width - msgX - px;
        paint.Color = new SKColor(180, 185, 195);
        string msg = message;
        if (monoFont.MeasureText(msg) > maxMsgW)
        {
            while (msg.Length > 3 && monoFont.MeasureText(msg + "...") > maxMsgW)
                msg = msg[..^1];
            msg += "...";
        }
        canvas.DrawText(msg, msgX, y + lineH - 3, monoFont, paint);

        y += lineH;
    }
}
