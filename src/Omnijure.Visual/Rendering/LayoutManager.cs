using SkiaSharp;
using Silk.NET.Maths;
using Omnijure.Core.DataStructures;
using System.Linq;

namespace Omnijure.Visual.Rendering;

public class LayoutManager
{
    // Layout Config
    public float HeaderHeight { get; private set; } = 28;
    
    // NEW: Panel System sin barras de t�tulo
    private readonly PanelSystem _panelSystem;

    // Bounds
    public SKRect HeaderRect { get; private set; }
    public SKRect LeftToolbarRect { get; private set; }
    public SKRect ChartRect { get; private set; }

    // Renderers
    private readonly SidebarRenderer _sidebar;
    private readonly LeftToolbarRenderer _leftToolbar;
    private readonly StatusBarRenderer _statusBar;
    
    // Panel scroll state: panelId -> scrollY offset
    private readonly Dictionary<string, float> _panelScrollOffsets = new();
    private float _aiChatContentHeight = 600;
    private bool _aiScrollInitialized;
    
    // Legacy properties for backward compatibility
    public bool IsResizingLeft => false;
    public bool IsResizingRight => false;
    
    public LayoutManager()
    {
        _sidebar = new SidebarRenderer();
        _leftToolbar = new LeftToolbarRenderer();
        _statusBar = new StatusBarRenderer();
        _panelSystem = new PanelSystem();
    }
    
    public void UpdateLayout(int width, int height)
    {
        // 0. Header
        HeaderRect = new SKRect(0, 0, width, HeaderHeight);

        // 1. Update panel system (calcula posiciones de paneles)
        _panelSystem.Update(width, height, HeaderHeight);

        // 2. Get chart area (�rea no ocupada por paneles dockeados)
        var chartArea = _panelSystem.GetChartArea(width, height, HeaderHeight);

        // 3. Chart completo (incluye toolbar interno)
        ChartRect = new SKRect(chartArea.Left, HeaderHeight, chartArea.Right, chartArea.Bottom);
        
        // 4. Left Toolbar DENTRO del chart (para referencia, pero se renderiza dentro del chart)
        // CRUCIAL: Usa chartArea.Bottom NO height para respetar panel Positions
        LeftToolbarRect = new SKRect(chartArea.Left, HeaderHeight, 
            chartArea.Left + LeftToolbarRenderer.ToolbarWidth, chartArea.Bottom);
    }
    
    public void HandleMouseDown(float x, float y)
    {
        _panelSystem.OnMouseDown(x, y);
    }
    
    public void TogglePanel(string panelId) => _panelSystem.TogglePanel(panelId);
    
    public bool IsPanelVisible(string panelId)
    {
        var panel = _panelSystem.GetPanel(panelId);
        return panel != null && !panel.IsClosed;
    }
    
    public void HandleMouseUp()
    {
        _panelSystem.OnMouseUp(0, 0, 0, 0);
    }
    
    public void HandleMouseMove(float x, float y, float deltaX, int screenWidth, int screenHeight)
    {
        _panelSystem.OnMouseMove(x, y, screenWidth, screenHeight, HeaderHeight);
    }

    public Omnijure.Visual.Drawing.DrawingTool? HandleToolbarClick(float x, float y)
    {
        // El toolbar est� dentro del ContentBounds del chart panel
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        if (chartPanel == null || chartPanel.IsClosed) return null;
        
        var contentArea = chartPanel.ContentBounds;
        var toolbarRect = new SKRect(contentArea.Left, contentArea.Top, 
            contentArea.Left + LeftToolbarRenderer.ToolbarWidth, contentArea.Bottom);
        
        if (toolbarRect.Contains(x, y))
        {
            float localY = y - contentArea.Top;
            float buttonY = 4;
            const float ButtonSize = 30;
            const float ButtonSpacing = 2;
            
            var tools = new[] 
            {
                Omnijure.Visual.Drawing.DrawingTool.None,
                Omnijure.Visual.Drawing.DrawingTool.TrendLine,
                Omnijure.Visual.Drawing.DrawingTool.HorizontalLine
            };
            
            for (int i = 0; i < tools.Length; i++)
            {
                if (localY >= buttonY && localY <= buttonY + ButtonSize)
                {
                    return tools[i];
                }
                buttonY += ButtonSize + ButtonSpacing;
            }
        }
        return null;
    }

    public void Render(SKCanvas canvas, ChartRenderer chartRenderer, RingBuffer<Candle> buffer, 
        string decision, int scrollOffset, float zoom, string symbol, string interval, 
        ChartType chartType, System.Collections.Generic.List<UiButton> buttons, 
        float minPrice, float maxPrice, Vector2D<float> mousePos, OrderBook orderBook, 
        RingBuffer<MarketTrade> trades, Omnijure.Visual.Drawing.DrawingToolState? drawingState,
        int screenWidth, int screenHeight)
    {
        // ???????????????????????????????????????????????????????????
        // ORDEN DE RENDERIZADO:
        //   CAPA 0: Panel backgrounds + chrome (sin overlay de drag)
        //   CAPA 1: Chart content DENTRO del panel
        //   CAPA 2: Panel content (sidebars)
        //   CAPA 3: Dock zone preview + dragging panel (SOBRE TODO)
        //   CAPA 4: Status bar
        //   CAPA 5: Window border
        // ???????????????????????????????????????????????????????????

        // CAPA 0: Workspace background
        var wsBgPaint = PaintPool.Instance.Rent();
        try
        {
            wsBgPaint.Color = new SKColor(10, 12, 16);
            wsBgPaint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, 0, screenWidth, screenHeight, wsBgPaint);
        }
        finally { PaintPool.Instance.Return(wsBgPaint); }

        // CAPA 0.5: Panel backgrounds + chrome
        _panelSystem.Render(canvas);

        // CAPA 1: Chart content
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        bool hasChart = chartPanel != null && !chartPanel.IsClosed;

        if (hasChart)
        {
            var contentArea = chartPanel.ContentBounds;
            
            canvas.Save();
            canvas.ClipRect(contentArea);
            canvas.Translate(contentArea.Left, contentArea.Top);
            
            var toolbarMousePos = new Vector2D<float>(
                mousePos.X - contentArea.Left, mousePos.Y - contentArea.Top);
            _leftToolbar.Render(canvas, contentArea.Height,
                drawingState?.ActiveTool ?? Omnijure.Visual.Drawing.DrawingTool.None, 
                toolbarMousePos.X, toolbarMousePos.Y);
            
            canvas.Save();
            canvas.Translate(LeftToolbarRenderer.ToolbarWidth, 0);
            canvas.ClipRect(new SKRect(0, 0, 
                contentArea.Width - LeftToolbarRenderer.ToolbarWidth, contentArea.Height));
            
            var chartMousePos = new Vector2D<float>(
                mousePos.X - contentArea.Left - LeftToolbarRenderer.ToolbarWidth, 
                mousePos.Y - contentArea.Top);
            
            chartRenderer.Render(canvas, 
                (int)(contentArea.Width - LeftToolbarRenderer.ToolbarWidth), 
                (int)contentArea.Height, 
                buffer, decision, scrollOffset, zoom, symbol, interval, chartType, buttons, 
                minPrice, maxPrice, chartMousePos, drawingState);
            
            canvas.Restore();
            canvas.Restore();
        }
        else
        {
            RenderEmptyState(canvas, ChartRect);
        }

        // CAPA 2: Sidebar panel content
        RenderPanelContent(canvas, orderBook, trades, buffer);

        // CAPA 3: Dock zone preview + dragging panel (SOBRE CHART Y PANELES)
        _panelSystem.RenderOverlay(canvas, RenderDraggingPanelContent);

        // CAPA 4: Status bar
        _statusBar.Render(canvas, screenWidth, screenHeight);
        
        // CAPA 5: Window border
        var windowBorderPaint = PaintPool.Instance.Rent();
        try
        {
            windowBorderPaint.Color = new SKColor(50, 55, 65);
            windowBorderPaint.Style = SKPaintStyle.Stroke;
            windowBorderPaint.StrokeWidth = 1;
            windowBorderPaint.IsAntialias = true;
            canvas.DrawRoundRect(new SKRect(0.5f, 0.5f, screenWidth - 0.5f, screenHeight - 0.5f), 8, 8, windowBorderPaint);
        }
        finally
        {
            PaintPool.Instance.Return(windowBorderPaint);
        }
    }
    
    public void UpdateFps(int fps) => _statusBar.UpdateFps(fps);
    
    public void UpdateChartTitle(string symbol, string interval, float price)
    {
        _panelSystem.UpdateChartTitle(symbol, interval, price);
    }

    private void RenderEmptyState(SKCanvas canvas, SKRect area)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Background
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(area, paint);
            
            // Mensaje central
            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 18);
            paint.Color = new SKColor(100, 105, 115);
            paint.IsAntialias = true;
            
            string message = "No active panels";
            float textWidth = TextMeasureCache.Instance.MeasureText(message, font);
            canvas.DrawText(message, area.MidX - textWidth / 2, area.MidY, font, paint);
            
            // Subtitle
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 12);
            string hint = "Drag a panel here or reopen Chart";
            float hintWidth = TextMeasureCache.Instance.MeasureText(hint, fontSmall);
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText(hint, area.MidX - hintWidth / 2, area.MidY + 30, fontSmall, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    // Cached references for overlay rendering
    private OrderBook _lastOrderBook;
    private RingBuffer<MarketTrade> _lastTrades;

    private void RenderPanelContent(SKCanvas canvas, OrderBook orderBook, RingBuffer<MarketTrade> trades, RingBuffer<Candle> buffer)
    {
        _lastOrderBook = orderBook;
        _lastTrades = trades;
        
        foreach (var panel in _panelSystem.Panels)
        {
            if (panel.IsClosed || panel.IsCollapsed) continue;
            if (panel.Config.Id == PanelDefinitions.CHART) continue;
            if (_panelSystem.IsPanelBeingDragged(panel)) continue;
            if (!_panelSystem.IsBottomTabActive(panel)) continue; // Skip inactive bottom tabs

            RenderSinglePanelContent(canvas, panel);
        }
    }

    private void RenderSinglePanelContent(SKCanvas canvas, DockablePanel panel)
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
                _sidebar.RenderOrderBook(canvas, contentWidth, contentHeight, _lastOrderBook);
                break;
            case PanelDefinitions.TRADES:
                _sidebar.RenderTrades(canvas, contentWidth, contentHeight, _lastTrades);
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
                RenderPlaceholderPanel(canvas, contentWidth, contentHeight, "No active alerts", "Create alerts from the chart");
                break;
            case PanelDefinitions.LOGS:
                RenderPlaceholderPanel(canvas, contentWidth, contentHeight, "Console", "Bot execution logs will appear here");
                break;
        }

        canvas.Restore();
        
        // Draw scrollbar for scrollable panels (after clip restore)
        if (panel.Config.Id is PanelDefinitions.PORTFOLIO or PanelDefinitions.AI_ASSISTANT)
        {
            float totalH = GetPanelContentHeight(panel);
            float scroll = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);

            if (panel.Config.Id == PanelDefinitions.AI_ASSISTANT)
            {
                // Scrollbar in the scroll zone between header and input
                float headerH = 48;
                float inputH = 52;
                float scrollZoneH = GetAiScrollZoneHeight(panel);
                if (totalH > scrollZoneH)
                {
                    DrawScrollbar(canvas,
                        panel.ContentBounds.Right - 6,
                        panel.ContentBounds.Top + headerH,
                        scrollZoneH, totalH, scroll);
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
            float px = 8; // horizontal padding
            float rx = width - px; // right edge
            
            // ?? Total Balance ??
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
            
            // Divider
            DrawSectionDivider(canvas, paint, px, rx, ref y);
            
            // ?? Accounts ??
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "ACCOUNTS");
            
            DrawAccountRow(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Binance Spot", "$8,240.12", true);
            DrawAccountRow(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Binance Futures", "$4,607.41", true);
            
            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);
            
            // ?? Active Bots ??
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "ACTIVE BOTS");
            
            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Grid Bot #1", "BTCUSDT", "+3.42%", true);
            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "DCA Bot #2", "ETHUSDT", "+1.87%", true);
            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Scalper #3", "SOLUSDT", "-0.24%", false);
            
            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);
            
            // ?? Holdings ??
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "HOLDINGS");
            
            // Column headers
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
            
            // ?? Datasets ??
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

    // ?? Portfolio helper renderers ??

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
        // Dot
        paint.Color = connected ? new SKColor(46, 204, 113) : new SKColor(120, 125, 135);
        canvas.DrawCircle(left + 5, y + 1, 3.5f, paint);
        
        // Name
        paint.Color = new SKColor(195, 200, 210);
        canvas.DrawText(name, left + 14, y + 5, font, paint);
        
        // Balance right-aligned
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
        
        // Card bg
        paint.Color = new SKColor(22, 26, 34);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(cardRect, 6, 6, paint);
        
        // Left: status dot + name/pair stacked
        paint.Color = new SKColor(46, 204, 113);
        canvas.DrawCircle(left + 10, y + cardH / 2, 3, paint);
        
        paint.Color = new SKColor(200, 205, 215);
        canvas.DrawText(name, left + 20, y + 13, font, paint);
        
        paint.Color = new SKColor(85, 90, 100);
        canvas.DrawText(pair, left + 20, y + 26, smallFont, paint);
        
        // Right: PnL
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
        
        // Subtle row bg
        paint.Color = new SKColor(20, 24, 32);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(rowRect, 4, 4, paint);
        
        // Coin name (bold-ish)
        paint.Color = new SKColor(210, 215, 225);
        canvas.DrawText(coin, left + 8, y + 12, font, paint);
        
        // Amount below coin
        paint.Color = new SKColor(80, 85, 95);
        canvas.DrawText(amount, left + 8, y + 24, smallFont, paint);
        
        // Value + change right-aligned, stacked
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

            // ??? FIXED HEADER (no scroll) ???
            
            // Summary cards
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

            // Column headers
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
            
            // Divider
            headerY += 6;
            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(px, headerY, rx, headerY, paint);
            paint.Style = SKPaintStyle.Fill;
            
            // ??? SCROLLABLE ROWS (clipped below header) ???
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
            
            // ??? SCROLLBAR (rendered outside scroll clip) ???
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
                
                // Track
                paint.Color = new SKColor(25, 29, 38);
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRoundRect(new SKRect(scrollBarX, rowsTop, scrollBarX + 4, rowsTop + scrollTrackH), 2, 2, paint);
                
                // Thumb
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
        
        // Row background
        paint.Color = new SKColor(18, 22, 30);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(px, y, width - 8, y + rowH - 2), 4, 4, paint);
        
        // Left accent bar
        paint.Color = isProfit ? new SKColor(46, 204, 113, 180) : new SKColor(239, 83, 80, 180);
        canvas.DrawRoundRect(new SKRect(px, y + 4, px + 3, y + rowH - 6), 1.5f, 1.5f, paint);
        
        float topY = y + 16;
        float botY = y + 30;

        // Col 0: Symbol + leverage + side/size
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

        // Col 1: Entry
        if (cols[1] < width - 40)
        {
            paint.Color = new SKColor(160, 165, 175);
            canvas.DrawText(entry, cols[1], topY, font, paint);
        }

        // Col 2: Mark price
        if (cols[2] < width - 40)
        {
            paint.Color = new SKColor(220, 225, 235);
            canvas.DrawText(mark, cols[2], topY, boldFont, paint);
        }

        // Col 3: PnL + ROE
        if (cols[3] < width - 40)
        {
            paint.Color = isProfit ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
            canvas.DrawText(pnl, cols[3], topY, boldFont, paint);
            canvas.DrawText(roe, cols[3], botY, smallFont, paint);
        }

        // Col 4: Liq. Price
        if (cols[4] < width - 60)
        {
            paint.Color = new SKColor(130, 135, 145);
            canvas.DrawText(liqPrice, cols[4], topY, font, paint);
        }

        // Col 5: Close button
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

    public bool IsMouseOverPanel(float x, float y)
    {
        return _panelSystem.IsMouseOverPanel(x, y);
    }

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

            // For panels with fixed headers, scroll area is smaller
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

            // AI Assistant: scroll zone is between fixed header and fixed input
            if (panel.Config.Id == PanelDefinitions.AI_ASSISTANT)
            {
                float scrollZoneH = GetAiScrollZoneHeight(panel);
                if (contentHeight <= scrollZoneH) return true;
                float aiMaxScroll = contentHeight - scrollZoneH;
                float aiCurrent = _panelScrollOffsets.GetValueOrDefault(panel.Config.Id, 0);
                _panelScrollOffsets[panel.Config.Id] = Math.Clamp(aiCurrent - deltaY * 20f, 0, aiMaxScroll);
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
        // Returns total content height for scroll calculation
        // For POSITIONS: only the rows area scrolls (header is fixed ~60px)
        float fixedHeaderH = 60;
        return panel.Config.Id switch
        {
            PanelDefinitions.POSITIONS => fixedHeaderH + (48 * 4) + 10,
            PanelDefinitions.PORTFOLIO => 520,
            // _aiChatContentHeight = total bubble content height (measured from headerH)
            // The visible scroll zone = panel height - headerH(48) - inputH(52)
            // So the scrollable content is just _aiChatContentHeight
            PanelDefinitions.AI_ASSISTANT => _aiChatContentHeight,
            _ => panel.ContentBounds.Height
        };
    }

    /// <summary>
    /// Returns the visible scroll zone height for AI chat (between header and input).
    /// </summary>
    private float GetAiScrollZoneHeight(DockablePanel panel)
    {
        float headerH = 48;
        float inputH = 52;
        return panel.ContentBounds.Height - headerH - inputH;
    }

    public bool IsDraggingPanel => _panelSystem.IsDraggingPanel;
    public bool IsResizingPanel => _panelSystem.IsResizing;
}
