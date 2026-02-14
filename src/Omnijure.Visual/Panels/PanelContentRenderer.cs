using Omnijure.Core.DataStructures;
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Renders all panel content (AI Chat, Portfolio, Positions, Alerts, Console, etc.)
/// Extracted from LayoutManager to keep that class focused on layout/tab logic.
/// </summary>
public class PanelContentRenderer
{
    private readonly PanelSystem _panelSystem;
    private readonly SidebarRenderer _sidebar;

    // Panel scroll state: panelId -> scrollY offset
    private readonly Dictionary<string, float> _panelScrollOffsets = new();
    private float _aiChatContentHeight = 600;
    private bool _aiScrollInitialized;

    // Cached references for overlay rendering
    private OrderBook _lastOrderBook;
    private RingBuffer<MarketTrade> _lastTrades;

    // Constants
    private const float AlertsFixedHeaderH = 58;
    private const int AlertsRowCount = 12;
    private const float AlertsRowH = 24; // 22 + 2 gap
    private const float ConsoleFixedHeaderH = 30;
    private const int ConsoleLineCount = 20;
    private const float ConsoleLineH = 15;

    public PanelContentRenderer(PanelSystem panelSystem, SidebarRenderer sidebar)
    {
        _panelSystem = panelSystem;
        _sidebar = sidebar;
    }

    public void RenderPanelContent(SKCanvas canvas, OrderBook orderBook, RingBuffer<MarketTrade> trades, RingBuffer<Candle> buffer)
    {
        _lastOrderBook = orderBook;
        _lastTrades = trades;

        foreach (var panel in _panelSystem.Panels)
        {
            if (panel.IsClosed || panel.IsCollapsed) continue;
            if (panel.Config.Id == PanelDefinitions.CHART) continue;
            if (_panelSystem.IsPanelBeingDragged(panel)) continue;
            if (!_panelSystem.IsBottomTabActive(panel)) continue;

            RenderSinglePanelContent(canvas, panel);
        }
    }

    public void RenderSinglePanelContent(SKCanvas canvas, DockablePanel panel)
    {
        canvas.Save();
        canvas.ClipRect(panel.ContentBounds);
        canvas.Translate(panel.ContentBounds.Left, panel.ContentBounds.Top);

        var contentWidth = panel.ContentBounds.Width;
        var contentHeight = panel.ContentBounds.Height;
        float scrollY = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);

        switch (panel.Config.Id)
        {
            case PanelDefinitions.ORDERBOOK:
                _sidebar.RenderOrderBook(canvas, contentWidth, contentHeight, _lastOrderBook, panel.Position);
                break;
            case PanelDefinitions.TRADES:
                _sidebar.RenderTrades(canvas, contentWidth, contentHeight, _lastTrades, panel.Position);
                break;
            case PanelDefinitions.POSITIONS:
                RenderPositionsPanel(canvas, contentWidth, contentHeight, scrollY);
                break;
            case PanelDefinitions.AI_ASSISTANT:
                RenderAIAssistantPanel(canvas, contentWidth, contentHeight, scrollY);
                break;
            case PanelDefinitions.PORTFOLIO:
                canvas.Translate(0, -scrollY);
                RenderPortfolioPanel(canvas, contentWidth, contentHeight);
                break;
            case PanelDefinitions.SCRIPT_EDITOR:
                RenderPlaceholderPanel(canvas, contentWidth, contentHeight, "Script Editor", "Pine Script \u2022 C# \u2022 Python");
                break;
            case PanelDefinitions.ALERTS:
                RenderAlertsPanel(canvas, contentWidth, contentHeight, scrollY);
                break;
            case PanelDefinitions.LOGS:
                RenderConsolePanel(canvas, contentWidth, contentHeight, scrollY);
                break;
        }

        canvas.Restore();

        // Draw scrollbar for scrollable panels (after clip restore)
        if (panel.Config.Id is PanelDefinitions.PORTFOLIO or PanelDefinitions.AI_ASSISTANT
            or PanelDefinitions.ALERTS or PanelDefinitions.LOGS)
        {
            float totalH = GetPanelContentHeight(panel);
            float scroll = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);

            if (panel.Config.Id == PanelDefinitions.AI_ASSISTANT)
            {
                float headerH = 48;
                float scrollZoneH = GetAiScrollZoneHeight(panel);
                if (totalH > scrollZoneH)
                {
                    DrawScrollbar(canvas,
                        panel.ContentBounds.Right - 6,
                        panel.ContentBounds.Top + headerH,
                        scrollZoneH, totalH, scroll);
                }
            }
            else if (panel.Config.Id == PanelDefinitions.ALERTS)
            {
                float scrollViewH = panel.ContentBounds.Height - AlertsFixedHeaderH;
                if (totalH > scrollViewH)
                {
                    DrawScrollbar(canvas,
                        panel.ContentBounds.Right - 6,
                        panel.ContentBounds.Top + AlertsFixedHeaderH,
                        scrollViewH, totalH, scroll);
                }
            }
            else if (panel.Config.Id == PanelDefinitions.LOGS)
            {
                float scrollViewH = panel.ContentBounds.Height - ConsoleFixedHeaderH;
                if (totalH > scrollViewH)
                {
                    DrawScrollbar(canvas,
                        panel.ContentBounds.Right - 6,
                        panel.ContentBounds.Top + ConsoleFixedHeaderH,
                        scrollViewH, totalH, scroll);
                }
            }
            else
            {
                float viewH = panel.ContentBounds.Height;
                if (totalH > viewH)
                {
                    DrawScrollbar(canvas,
                        panel.ContentBounds.Right - 6,
                        panel.ContentBounds.Top,
                        viewH, totalH, scroll);
                }
            }
        }
    }

    private static void DrawScrollbar(SKCanvas canvas, float x, float y, float viewH, float contentH, float scrollY)
    {
        float thumbRatio = viewH / contentH;
        float thumbH = Math.Max(viewH * thumbRatio, 16);
        float scrollRange = contentH - viewH;
        float thumbY = y + (scrollRange > 0 ? (scrollY / scrollRange) * (viewH - thumbH) : 0);

        using var trackPaint = new SKPaint { Color = new SKColor(25, 29, 38, 120), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(x, y, x + 4, y + viewH), 2, 2, trackPaint);

        using var thumbPaint = new SKPaint { Color = new SKColor(80, 85, 100), Style = SKPaintStyle.Fill, IsAntialias = true };
        canvas.DrawRoundRect(new SKRect(x, thumbY, x + 4, thumbY + thumbH), 2, 2, thumbPaint);
    }

    /// <summary>
    /// Re-renders dragging panel content on top of the overlay chrome.
    /// </summary>
    public void RenderDraggingPanelContent(SKCanvas canvas, DockablePanel panel)
    {
        if (panel.Config.Id == PanelDefinitions.CHART) return;
        RenderSinglePanelContent(canvas, panel);
    }

    // ═══════════════════════════════════════════════════════════════
    // AI Assistant Panel
    // ═══════════════════════════════════════════════════════════════

    private void RenderAIAssistantPanel(SKCanvas canvas, float width, float height, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 13);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
            using var fontCode = new SKFont(SKTypeface.FromFamilyName("Cascadia Code", SKFontStyle.Normal), 10);

            paint.IsAntialias = true;
            float pad = 10;
            float headerH = 48;
            float inputH = 52;

            float scrollZoneTop = headerH;
            float scrollZoneBottom = height - inputH;
            float scrollZoneH = scrollZoneBottom - scrollZoneTop;

            // ========================================
            // STEP 1: Chat bubbles (clipped to scroll zone, translated by -scrollY)
            // ========================================
            canvas.Save();
            canvas.ClipRect(new SKRect(0, scrollZoneTop, width, scrollZoneBottom));
            canvas.Translate(0, -scrollY);

            paint.Style = SKPaintStyle.Fill;
            float y = headerH + 8;

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "What pattern is forming on BTCUSDT 1h?", pad, "2h ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "I see a bullish flag forming on BTC/USDT 1h chart. The price consolidated between $68,800-$69,100 after a strong impulse move.\n\n> Entry: Break above $69,100\n> Target: $69,800 (+1.0%)\n> Stop: $68,600 (-0.7%)\n> R:R ratio: 1.43",
                pad, "2h ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Run RSI divergence scan on top 10 pairs", pad, "1h 45m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Scanning 10 pairs for RSI divergence...\n\n[+] ETHUSDT - Bullish divergence (RSI 34)\n[+] SOLUSDT - Bullish divergence (RSI 29)\n[!] BNBUSDT - Neutral (RSI 48)\n[-] XRPUSDT - Bearish divergence (RSI 72)",
                pad, "1h 44m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Set up a grid bot for ETH between $3,200-$3,600", pad, "1h 20m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Grid bot configured:\n\n> Pair: ETHUSDT\n> Range: $3,200 - $3,600\n> Grid levels: 20\n> Investment: $500 USDT\n> Grid spacing: $20 (0.56%)\n\nEstimated profit per grid: $1.12\nAnnualized return: ~18-24% in range\n\nReady to deploy. Type 'confirm' to start.",
                pad, "1h 19m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Show me the top gainers in the last 4h", pad, "48m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Top gainers (4h):\n\n[+] PEPEUSDT  +8.42%  Vol: $124M\n[+] SUIUSDT   +5.17%  Vol: $89M\n[+] FETUSDT   +4.83%  Vol: $67M\n[+] INJUSDT   +3.91%  Vol: $52M\n[+] ARBUSDT   +3.24%  Vol: $41M\n\nPEPE shows strong momentum with volume breakout above 20-day average.",
                pad, "47m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "What's the current market sentiment?", pad, "12m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Market Sentiment Analysis:\n\nFear & Greed Index: 72 (Greed)\nBTC Dominance: 54.2% (+0.3%)\nTotal Market Cap: $2.48T\n\n[+] Funding rates positive but moderate\n[+] Open interest rising steadily\n[!] Whale accumulation detected on BTC\n[-] Some altcoins showing exhaustion\n\nOverall: Cautiously bullish. Consider trailing stops on long positions.",
                pad, "11m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Backtest a simple EMA crossover on SOL", pad, "3m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Backtest: EMA 9/21 Crossover on SOLUSDT\nPeriod: Last 30 days (1h candles)\n\nResults:\n> Total trades: 14\n> Win rate: 64.3%\n> Profit factor: 1.82\n> Max drawdown: -3.4%\n> Net return: +8.7%\n> Sharpe ratio: 1.45\n\nThe strategy performs well in trending markets but suffers during consolidation. Consider adding a volatility filter.",
                pad, "2m ago");

            // Total height of all bubbles content (from headerH to final y)
            float bubblesContentH = y + 8 - headerH;
            _aiChatContentHeight = bubblesContentH;

            canvas.Restore(); // restore scroll clip

            // ---- Auto-scroll to bottom on first render ----
            if (!_aiScrollInitialized && bubblesContentH > scrollZoneH)
            {
                _aiScrollInitialized = true;
                _panelScrollOffsets[PanelDefinitions.AI_ASSISTANT] = bubblesContentH - scrollZoneH;
            }

            // ========================================
            // STEP 2: Fixed header (drawn OVER bubbles, no scroll)
            // ========================================
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, 0, width, headerH, paint);

            paint.Color = new SKColor(70, 140, 255);
            canvas.DrawText("Omnijure AI", 12, 18, fontBold, paint);

            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Star,
                fontBold.MeasureText("Omnijure AI") + 16, 6, 12, new SKColor(70, 140, 255));

            paint.Color = new SKColor(70, 75, 85);
            canvas.DrawText("GPT-4o  |  Connected", 12, 34, fontSmall, paint);

            paint.Color = new SKColor(46, 204, 113);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(width - 16, 26, 4, paint);

            // Separator
            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(8, headerH - 6, width - 8, headerH - 6, paint);

            // ========================================
            // STEP 3: Fixed input box (drawn OVER bubbles, no scroll)
            // ========================================
            float inputBoxH = 34;
            float inputPad = 8;
            float inputAreaTop = height - inputH;

            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, inputAreaTop, width, inputH, paint);

            paint.Color = new SKColor(28, 32, 40);
            var inputRect = new SKRect(inputPad, inputAreaTop + 8, width - inputPad, inputAreaTop + 8 + inputBoxH);
            canvas.DrawRoundRect(inputRect, 8, 8, paint);

            paint.Color = new SKColor(40, 45, 55);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawRoundRect(inputRect, 8, 8, paint);

            paint.Style = SKPaintStyle.Fill;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("Ask about patterns, strategies, or scan markets...", 16, inputAreaTop + 8 + inputBoxH / 2 + 4, fontSmall, paint);

            // Send button
            float btnH = inputBoxH - 8;
            float btnW = 28;
            float btnX = width - inputPad - btnW - 4;
            float btnY = inputAreaTop + 8 + 4;
            paint.Color = new SKColor(56, 139, 253);
            canvas.DrawRoundRect(new SKRect(btnX, btnY, btnX + btnW, btnY + btnH), 4, 4, paint);
            paint.Color = SKColors.White;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            paint.StrokeCap = SKStrokeCap.Round;
            float sendCy = btnY + btnH / 2;
            canvas.DrawLine(btnX + 7, sendCy, btnX + btnW - 7, sendCy, paint);
            canvas.DrawLine(btnX + btnW - 12, sendCy - 5, btnX + btnW - 7, sendCy, paint);
            canvas.DrawLine(btnX + btnW - 12, sendCy + 5, btnX + btnW - 7, sendCy, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private float DrawChatBubble(SKCanvas canvas, SKPaint paint, SKFont font, SKFont timeFont,
        float panelW, float y, bool isUser, string text, float pad, string timestamp = "just now")
    {
        float bubbleMaxW = panelW - pad * 2 - 20;
        float bubbleX = isUser ? pad + 20 : pad;
        float lineH = 15;

        // Split text into lines (handle \n and word wrap)
        var lines = new List<string>();
        foreach (var segment in text.Split('\n'))
        {
            if (string.IsNullOrEmpty(segment)) { lines.Add(""); continue; }
            string remaining = segment;
            while (remaining.Length > 0)
            {
                float w = font.MeasureText(remaining);
                if (w <= bubbleMaxW - 20) { lines.Add(remaining); break; }
                int cut = (int)(remaining.Length * (bubbleMaxW - 20) / w);
                if (cut >= remaining.Length) cut = remaining.Length - 1;
                int space = remaining.LastIndexOf(' ', cut);
                if (space > 0) cut = space;
                lines.Add(remaining[..cut]);
                remaining = remaining[cut..].TrimStart();
            }
        }

        float bubbleH = lines.Count * lineH + 16;
        float bubbleW = bubbleMaxW;
        if (isUser)
        {
            float maxLineW = 0;
            foreach (var l in lines) { float lw = font.MeasureText(l); if (lw > maxLineW) maxLineW = lw; }
            bubbleW = Math.Min(maxLineW + 24, bubbleMaxW);
            bubbleX = panelW - pad - bubbleW;
        }

        var bubbleRect = new SKRect(bubbleX, y, bubbleX + bubbleW, y + bubbleH);

        // Bubble background
        paint.Style = SKPaintStyle.Fill;
        paint.Color = isUser ? new SKColor(45, 75, 140) : new SKColor(28, 32, 40);
        canvas.DrawRoundRect(bubbleRect, 8, 8, paint);

        // Text
        paint.Color = isUser ? new SKColor(220, 230, 245) : new SKColor(195, 200, 210);
        float textY = y + 13;
        foreach (var line in lines)
        {
            if (line.Length > 0)
            {
                // Color-coded line markers
                if (!isUser && (line.StartsWith("[+]") || line.StartsWith(">")))
                    paint.Color = new SKColor(170, 210, 180);
                else if (!isUser && line.StartsWith("[!]"))
                    paint.Color = new SKColor(220, 200, 130);
                else if (!isUser && line.StartsWith("[-]"))
                    paint.Color = new SKColor(220, 150, 150);
                else
                    paint.Color = isUser ? new SKColor(220, 230, 245) : new SKColor(195, 200, 210);

                canvas.DrawText(line, bubbleX + 10, textY, font, paint);
            }
            textY += lineH;
        }

        // Timestamp
        paint.Color = new SKColor(70, 75, 85);
        string time = isUser ? $"You \u2022 {timestamp}" : $"AI \u2022 {timestamp}";
        float tw = timeFont.MeasureText(time);
        float timeX = isUser ? bubbleX + bubbleW - tw - 4 : bubbleX + 4;
        canvas.DrawText(time, timeX, y + bubbleH + 12, timeFont, paint);

        return y + bubbleH + 24;
    }

    // ═══════════════════════════════════════════════════════════════
    // Portfolio Panel
    // ═══════════════════════════════════════════════════════════════

    private void RenderPortfolioPanel(SKCanvas canvas, float width, float height)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontSection = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 9);
            using var fontBalance = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 20);

            paint.IsAntialias = true;
            float y = 4;
            float px = 8;
            float rx = width - px;

            // Total Balance
            paint.Color = new SKColor(85, 90, 100);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawText("TOTAL BALANCE", px, y + 10, fontSmall, paint);
            y += 18;

            paint.Color = new SKColor(235, 240, 250);
            canvas.DrawText("$12,847.53", px, y + 16, fontBalance, paint);
            y += 22;

            paint.Color = new SKColor(46, 204, 113);
            canvas.DrawText("+$342.18 (2.73%)", px, y + 10, fontSmall, paint);
            y += 18;

            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Accounts
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "ACCOUNTS");

            DrawAccountRow(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Binance Spot", "$8,240.12", true);
            DrawAccountRow(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Binance Futures", "$4,607.41", true);

            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Active Bots
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "ACTIVE BOTS");

            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Grid Bot #1", "BTCUSDT", "+3.42%", true);
            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "DCA Bot #2", "ETHUSDT", "+1.87%", true);
            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Scalper #3", "SOLUSDT", "-0.24%", false);

            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Holdings
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "HOLDINGS");

            paint.Color = new SKColor(70, 75, 85);
            canvas.DrawText("ASSET", px, y, fontSmall, paint);
            string hdrVal = "VALUE";
            float hdrValW = fontSmall.MeasureText(hdrVal);
            canvas.DrawText(hdrVal, rx - hdrValW, y, fontSmall, paint);
            y += 10;

            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "BTC", "0.0842 BTC", "$5,804.21", "+2.1%", true);
            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "ETH", "1.245 ETH", "$4,318.90", "+3.4%", true);
            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "SOL", "12.50 SOL", "$1,912.50", "-0.8%", false);
            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "USDT", "811.92 USDT", "$811.92", "0.0%", true);

            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Datasets
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "DATASETS");

            DrawDatasetRow(canvas, paint, fontNormal, fontSmall, px, ref y,
                "BTCUSDT 1m", "30 days \u2022 43,200 candles");
            DrawDatasetRow(canvas, paint, fontNormal, fontSmall, px, ref y,
                "ETHUSDT 5m", "90 days \u2022 25,920 candles");
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    // Portfolio helper renderers

    private static void DrawSectionDivider(SKCanvas canvas, SKPaint paint, float left, float right, ref float y)
    {
        paint.Color = new SKColor(35, 40, 50);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        canvas.DrawLine(left, y, right, y, paint);
        paint.Style = SKPaintStyle.Fill;
        y += 10;
    }

    private static void DrawSectionHeader(SKCanvas canvas, SKPaint paint, SKFont font, float x, ref float y, string label)
    {
        paint.Color = new SKColor(90, 96, 108);
        canvas.DrawText(label, x, y + 2, font, paint);
        y += 16;
    }

    private static void DrawAccountRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, ref float y, string name, string balance, bool connected)
    {
        paint.Color = connected ? new SKColor(46, 204, 113) : new SKColor(120, 125, 135);
        canvas.DrawCircle(left + 5, y + 1, 3.5f, paint);

        paint.Color = new SKColor(195, 200, 210);
        canvas.DrawText(name, left + 14, y + 5, font, paint);

        paint.Color = new SKColor(140, 145, 155);
        float bw = smallFont.MeasureText(balance);
        canvas.DrawText(balance, right - bw, y + 5, smallFont, paint);

        y += 20;
    }

    private static void DrawBotCard(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, ref float y, string name, string pair, string pnl, bool isPositive)
    {
        float cardH = 32;
        var cardRect = new SKRect(left, y, right, y + cardH);

        paint.Color = new SKColor(22, 26, 34);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(cardRect, 6, 6, paint);

        paint.Color = new SKColor(46, 204, 113);
        canvas.DrawCircle(left + 10, y + cardH / 2, 3, paint);

        paint.Color = new SKColor(200, 205, 215);
        canvas.DrawText(name, left + 20, y + 13, font, paint);

        paint.Color = new SKColor(85, 90, 100);
        canvas.DrawText(pair, left + 20, y + 26, smallFont, paint);

        paint.Color = isPositive ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
        float pw = font.MeasureText(pnl);
        canvas.DrawText(pnl, right - pw - 8, y + 20, font, paint);

        y += cardH + 4;
    }

    private static void DrawHoldingCard(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, ref float y, string coin, string amount, string value, string change, bool isPositive)
    {
        float rowH = 28;
        var rowRect = new SKRect(left, y, right, y + rowH);

        paint.Color = new SKColor(20, 24, 32);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(rowRect, 4, 4, paint);

        paint.Color = new SKColor(210, 215, 225);
        canvas.DrawText(coin, left + 8, y + 12, font, paint);

        paint.Color = new SKColor(80, 85, 95);
        canvas.DrawText(amount, left + 8, y + 24, smallFont, paint);

        paint.Color = new SKColor(190, 195, 205);
        float vw = smallFont.MeasureText(value);
        canvas.DrawText(value, right - vw - 6, y + 12, smallFont, paint);

        paint.Color = isPositive ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
        float cw = smallFont.MeasureText(change);
        canvas.DrawText(change, right - cw - 6, y + 24, smallFont, paint);

        y += rowH + 3;
    }

    private static void DrawDatasetRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, ref float y, string name, string details)
    {
        paint.Color = new SKColor(56, 139, 253);
        canvas.DrawCircle(left + 4, y + 2, 3, paint);

        paint.Color = new SKColor(185, 190, 200);
        canvas.DrawText(name, left + 14, y + 6, font, paint);
        y += 16;

        paint.Color = new SKColor(75, 80, 90);
        canvas.DrawText(details, left + 14, y, smallFont, paint);
        y += 16;
    }

    // ═══════════════════════════════════════════════════════════════
    // Positions Panel
    // ═══════════════════════════════════════════════════════════════

    private void RenderPositionsPanel(SKCanvas canvas, float width, float height, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontHeader = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 9);
            using var fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 10);
            using var fontValue = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 12);

            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;

            float px = 8;
            float rx = width - px;
            float rowH = 48;

            // FIXED HEADER (no scroll)
            float sumY = 4;
            float cardGap = 6;
            float cardW = (width - px * 2 - cardGap * 3) / 4;

            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px, sumY, cardW, "Margin", "$2,140.00", new SKColor(200, 205, 215));
            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px + cardW + cardGap, sumY, cardW, "Unrealized PnL", "+$187.42", new SKColor(46, 204, 113));
            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px + (cardW + cardGap) * 2, sumY, cardW, "ROE", "+8.76%", new SKColor(46, 204, 113));
            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px + (cardW + cardGap) * 3, sumY, cardW, "Positions", "4 open", new SKColor(200, 205, 215));

            float headerY = sumY + 48;
            float usableW = width - px * 2;
            float[] cols = [
                px + 4,
                px + usableW * 0.22f,
                px + usableW * 0.37f,
                px + usableW * 0.52f,
                px + usableW * 0.68f,
                rx - 56
            ];
            string[] headers = ["Symbol", "Entry", "Mark Price", "PnL (ROE)", "Liq. Price", ""];

            paint.Color = new SKColor(65, 70, 80);
            for (int i = 0; i < headers.Length && i < cols.Length; i++)
            {
                if (cols[i] < width && headers[i].Length > 0)
                    canvas.DrawText(headers[i], cols[i], headerY, fontHeader, paint);
            }

            headerY += 6;
            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(px, headerY, rx, headerY, paint);
            paint.Style = SKPaintStyle.Fill;

            // SCROLLABLE ROWS
            float rowsTop = headerY + 2;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, rowsTop, width, height));
            canvas.Translate(0, -scrollY);

            float y = rowsTop + 4;

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "BTCUSDT", "Long", "0.015 BTC",
                "$67,240.00", "$69,061.00",
                "+$27.32", "+2.71%", true, "10x", "$62,100");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "ETHUSDT", "Long", "0.85 ETH",
                "$3,420.00", "$3,512.40",
                "+$78.54", "+2.70%", true, "5x", "$2,980");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "SOLUSDT", "Short", "12.0 SOL",
                "$158.20", "$153.80",
                "+$52.80", "+2.78%", true, "10x", "$174.50");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "BNBUSDT", "Long", "1.5 BNB",
                "$612.40", "$608.50",
                "-$5.85", "-0.64%", false, "3x", "$420.80");

            canvas.Restore();

            // SCROLLBAR
            float rowsTotalH = rowH * 4 + 10;
            float rowsViewH = height - rowsTop;
            if (rowsTotalH > rowsViewH)
            {
                float scrollBarX = width - 6;
                float scrollTrackH = rowsViewH;
                float thumbRatio = rowsViewH / rowsTotalH;
                float thumbH = Math.Max(scrollTrackH * thumbRatio, 16);
                float scrollRange = rowsTotalH - rowsViewH;
                float thumbY = rowsTop + (scrollRange > 0 ? (scrollY / scrollRange) * (scrollTrackH - thumbH) : 0);

                paint.Color = new SKColor(25, 29, 38);
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRoundRect(new SKRect(scrollBarX, rowsTop, scrollBarX + 4, rowsTop + scrollTrackH), 2, 2, paint);

                paint.Color = new SKColor(70, 75, 90);
                canvas.DrawRoundRect(new SKRect(scrollBarX, thumbY, scrollBarX + 4, thumbY + thumbH), 2, 2, paint);
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private static void DrawSummaryCard(SKCanvas canvas, SKPaint paint, SKFont labelFont, SKFont valueFont,
        float x, float y, float w, string label, string value, SKColor valueColor)
    {
        float h = 38;
        paint.Color = new SKColor(20, 24, 32);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), 6, 6, paint);

        paint.Color = new SKColor(75, 80, 92);
        canvas.DrawText(label, x + 10, y + 14, labelFont, paint);

        paint.Color = valueColor;
        canvas.DrawText(value, x + 10, y + 30, valueFont, paint);
    }

    private static void DrawPositionRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont, SKFont boldFont,
        float[] cols, float width, ref float y, float rowH,
        string symbol, string side, string size,
        string entry, string mark,
        string pnl, string roe, bool isProfit, string leverage, string liqPrice)
    {
        float px = cols[0] - 4;

        paint.Color = new SKColor(18, 22, 30);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(px, y, width - 8, y + rowH - 2), 4, 4, paint);

        paint.Color = isProfit ? new SKColor(46, 204, 113, 180) : new SKColor(239, 83, 80, 180);
        canvas.DrawRoundRect(new SKRect(px, y + 4, px + 3, y + rowH - 6), 1.5f, 1.5f, paint);

        float topY = y + 16;
        float botY = y + 30;

        paint.Color = new SKColor(215, 220, 230);
        canvas.DrawText(symbol, cols[0] + 4, topY, boldFont, paint);

        float levX = cols[0] + 4 + boldFont.MeasureText(symbol) + 4;
        float levW = smallFont.MeasureText(leverage) + 8;
        paint.Color = new SKColor(50, 55, 70);
        canvas.DrawRoundRect(new SKRect(levX, topY - 9, levX + levW, topY + 2), 3, 3, paint);
        paint.Color = new SKColor(160, 165, 175);
        canvas.DrawText(leverage, levX + 4, topY - 1, smallFont, paint);

        bool isLong = side == "Long";
        paint.Color = isLong ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
        canvas.DrawText(side, cols[0] + 4, botY, smallFont, paint);
        paint.Color = new SKColor(100, 105, 115);
        canvas.DrawText(" " + size, cols[0] + 4 + smallFont.MeasureText(side), botY, smallFont, paint);

        if (cols[1] < width - 40)
        {
            paint.Color = new SKColor(160, 165, 175);
            canvas.DrawText(entry, cols[1], topY, font, paint);
        }

        if (cols[2] < width - 40)
        {
            paint.Color = new SKColor(220, 225, 235);
            canvas.DrawText(mark, cols[2], topY, boldFont, paint);
        }

        if (cols[3] < width - 40)
        {
            paint.Color = isProfit ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
            canvas.DrawText(pnl, cols[3], topY, boldFont, paint);
            canvas.DrawText(roe, cols[3], botY, smallFont, paint);
        }

        if (cols[4] < width - 60)
        {
            paint.Color = new SKColor(130, 135, 145);
            canvas.DrawText(liqPrice, cols[4], topY, font, paint);
        }

        if (cols.Length > 5 && cols[5] < width)
        {
            float btnX = cols[5];
            float btnY = y + rowH / 2 - 9;
            float btnW = 48;
            float btnH = 18;
            var btnRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

            paint.Color = new SKColor(60, 30, 30);
            canvas.DrawRoundRect(btnRect, 4, 4, paint);

            paint.Color = new SKColor(239, 83, 80);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawRoundRect(btnRect, 4, 4, paint);

            paint.Style = SKPaintStyle.Fill;
            paint.Color = new SKColor(239, 120, 110);
            string closeLabel = "Close";
            float clW = smallFont.MeasureText(closeLabel);
            canvas.DrawText(closeLabel, btnX + (btnW - clW) / 2, btnY + 13, smallFont, paint);
        }

        y += rowH;
    }

    // ═══════════════════════════════════════════════════════════════
    // Alerts Panel
    // ═══════════════════════════════════════════════════════════════

    private void RenderAlertsPanel(SKCanvas canvas, float width, float height, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontHeader = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 9);
            using var fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 10);

            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;
            float px = 8;
            float rx = width - px;

            // SCROLLABLE ROWS
            float rowsTop = AlertsFixedHeaderH;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, rowsTop, width, height));
            canvas.Translate(0, -scrollY);

            float y = rowsTop + 4;

            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "BTCUSDT", "Price above", "$69,500.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "ETHUSDT", "Price below", "$3,200.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "SOLUSDT", "RSI > 70", "$158.40", "Triggered", new SKColor(255, 180, 50));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "BTCUSDT", "EMA Cross 9/21", "$68,900.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "BNBUSDT", "Price above", "$620.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "XRPUSDT", "Vol spike >200%", "$0.6240", "Triggered", new SKColor(255, 180, 50));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "ARBUSDT", "Price below", "$1.12", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "DOGEUSDT", "MACD Cross", "$0.1580", "Expired", new SKColor(120, 125, 135));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "PEPEUSDT", "Price above", "$0.00001200", "Triggered", new SKColor(255, 180, 50));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "LINKUSDT", "Price below", "$14.80", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "AVAXUSDT", "RSI < 30", "$35.20", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "DOTUSDT", "BB squeeze", "$7.45", "Expired", new SKColor(120, 125, 135));

            canvas.Restore();

            // FIXED HEADER
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, 0, width, AlertsFixedHeaderH, paint);

            float hy = 4;
            float cardGap = 6;
            float cardW = (width - px * 2 - cardGap * 2) / 3;
            DrawAlertSummaryCard(canvas, paint, fontSmall, fontBold, px, hy, cardW, "Active", "7", new SKColor(46, 204, 113));
            DrawAlertSummaryCard(canvas, paint, fontSmall, fontBold, px + cardW + cardGap, hy, cardW, "Triggered", "3", new SKColor(255, 180, 50));
            DrawAlertSummaryCard(canvas, paint, fontSmall, fontBold, px + (cardW + cardGap) * 2, hy, cardW, "Expired", "2", new SKColor(120, 125, 135));
            hy += 40;

            paint.Color = new SKColor(65, 70, 80);
            canvas.DrawText("PAIR", px + 4, hy, fontHeader, paint);
            canvas.DrawText("CONDITION", px + width * 0.25f, hy, fontHeader, paint);
            canvas.DrawText("PRICE", px + width * 0.55f, hy, fontHeader, paint);
            string statusHdr = "STATUS";
            float sW = fontHeader.MeasureText(statusHdr);
            canvas.DrawText(statusHdr, rx - sW - 4, hy, fontHeader, paint);
            hy += 8;

            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(px, hy, rx, hy, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private static void DrawAlertSummaryCard(SKCanvas canvas, SKPaint paint, SKFont labelFont, SKFont valueFont,
        float x, float y, float w, string label, string value, SKColor valueColor)
    {
        float h = 32;
        paint.Color = new SKColor(20, 24, 32);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), 6, 6, paint);
        paint.Color = new SKColor(75, 80, 92);
        canvas.DrawText(label, x + 8, y + 12, labelFont, paint);
        paint.Color = valueColor;
        canvas.DrawText(value, x + 8, y + 26, valueFont, paint);
    }

    private static void DrawAlertRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, float width, ref float y,
        string pair, string condition, string price, string status, SKColor statusColor)
    {
        float rowH = 22;
        var rowRect = new SKRect(left, y, right, y + rowH);

        paint.Color = new SKColor(18, 22, 30);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(rowRect, 3, 3, paint);

        paint.Color = new SKColor(210, 215, 225);
        canvas.DrawText(pair, left + 4, y + 14, font, paint);

        paint.Color = new SKColor(150, 155, 165);
        canvas.DrawText(condition, left + width * 0.25f, y + 14, smallFont, paint);

        paint.Color = new SKColor(180, 185, 195);
        canvas.DrawText(price, left + width * 0.55f, y + 14, smallFont, paint);

        float badgeW = smallFont.MeasureText(status) + 10;
        float badgeX = right - badgeW - 4;
        float badgeY = y + 4;
        float badgeH = 14;
        paint.Color = new SKColor(statusColor.Red, statusColor.Green, statusColor.Blue, 30);
        canvas.DrawRoundRect(new SKRect(badgeX, badgeY, badgeX + badgeW, badgeY + badgeH), 3, 3, paint);
        paint.Color = statusColor;
        float statusTextW = smallFont.MeasureText(status);
        canvas.DrawText(status, badgeX + (badgeW - statusTextW) / 2, badgeY + 11, smallFont, paint);

        y += rowH + 2;
    }

    // ═══════════════════════════════════════════════════════════════
    // Console Panel
    // ═══════════════════════════════════════════════════════════════

    private void RenderConsolePanel(SKCanvas canvas, float width, float height, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontMono = new SKFont(SKTypeface.FromFamilyName("Cascadia Code", SKFontStyle.Normal), 10);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Cascadia Code", SKFontStyle.Normal), 9);
            using var fontHeader = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);

            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;
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

    // ═══════════════════════════════════════════════════════════════
    // Placeholder Panel
    // ═══════════════════════════════════════════════════════════════

    private void RenderPlaceholderPanel(SKCanvas canvas, float width, float height, string title, string subtitle)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontTitle = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 13);
            using var fontSub = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);

            paint.IsAntialias = true;
            paint.Color = new SKColor(110, 115, 125);
            float tw = TextMeasureCache.Instance.MeasureText(title, fontTitle);
            canvas.DrawText(title, (width - tw) / 2, height / 2 - 8, fontTitle, paint);

            paint.Color = new SKColor(70, 75, 85);
            float sw = TextMeasureCache.Instance.MeasureText(subtitle, fontSub);
            canvas.DrawText(subtitle, (width - sw) / 2, height / 2 + 14, fontSub, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    // ═══════════════════════════════════════════════════════════════
    // Scroll Handling
    // ═══════════════════════════════════════════════════════════════

    /// <summary>
    /// Handles mouse wheel scroll inside panels. Returns true if consumed.
    /// </summary>
    public bool HandlePanelScroll(float x, float y, float deltaY)
    {
        foreach (var panel in _panelSystem.Panels)
        {
            if (panel.IsClosed || panel.IsCollapsed) continue;
            if (panel.Config.Id == PanelDefinitions.CHART) continue;
            if (!_panelSystem.IsBottomTabActive(panel)) continue;
            if (!panel.ContentBounds.Contains(x, y)) continue;

            float contentHeight = GetPanelContentHeight(panel);
            float viewHeight = panel.ContentBounds.Height;

            if (panel.Config.Id == PanelDefinitions.POSITIONS)
            {
                float fixedHeaderH = 60;
                float rowsContentH = (48 * 4) + 10;
                float rowsViewH = viewHeight - fixedHeaderH;
                if (rowsContentH <= rowsViewH) return true;
                float posMaxScroll = rowsContentH - rowsViewH;
                float posCurrent = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);
                _panelScrollOffsets[panel.Config.Id] = Math.Clamp(posCurrent - deltaY * 20f, 0, posMaxScroll);
                return true;
            }

            if (panel.Config.Id == PanelDefinitions.AI_ASSISTANT)
            {
                float scrollZoneH = GetAiScrollZoneHeight(panel);
                if (contentHeight <= scrollZoneH) return true;
                float aiMaxScroll = contentHeight - scrollZoneH;
                float aiCurrent = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);
                _panelScrollOffsets[panel.Config.Id] = Math.Clamp(aiCurrent - deltaY * 20f, 0, aiMaxScroll);
                return true;
            }

            if (panel.Config.Id == PanelDefinitions.ALERTS)
            {
                float scrollViewH = viewHeight - AlertsFixedHeaderH;
                if (contentHeight <= scrollViewH) return true;
                float alertsMax = contentHeight - scrollViewH;
                float alertsCur = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);
                _panelScrollOffsets[panel.Config.Id] = Math.Clamp(alertsCur - deltaY * 20f, 0, alertsMax);
                return true;
            }

            if (panel.Config.Id == PanelDefinitions.LOGS)
            {
                float scrollViewH = viewHeight - ConsoleFixedHeaderH;
                if (contentHeight <= scrollViewH) return true;
                float logsMax = contentHeight - scrollViewH;
                float logsCur = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);
                _panelScrollOffsets[panel.Config.Id] = Math.Clamp(logsCur - deltaY * 20f, 0, logsMax);
                return true;
            }

            if (contentHeight <= viewHeight) return true;

            float maxScroll = contentHeight - viewHeight;
            float current = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);
            float newScroll = Math.Clamp(current - deltaY * 20f, 0, maxScroll);
            _panelScrollOffsets[panel.Config.Id] = newScroll;
            return true;
        }
        return false;
    }

    private float GetPanelContentHeight(DockablePanel panel)
    {
        float fixedHeaderH = 60;
        return panel.Config.Id switch
        {
            PanelDefinitions.POSITIONS => fixedHeaderH + (48 * 4) + 10,
            PanelDefinitions.PORTFOLIO => 520,
            PanelDefinitions.AI_ASSISTANT => _aiChatContentHeight,
            PanelDefinitions.ALERTS => AlertsRowCount * AlertsRowH + 8,
            PanelDefinitions.LOGS => ConsoleLineCount * ConsoleLineH + 8,
            _ => panel.ContentBounds.Height
        };
    }

    private float GetAiScrollZoneHeight(DockablePanel panel)
    {
        float headerH = 48;
        float inputH = 52;
        return panel.ContentBounds.Height - headerH - inputH;
    }
}
