using SkiaSharp;
using Silk.NET.Maths;
using Omnijure.Core.DataStructures;
using System.Linq;

namespace Omnijure.Visual.Rendering;

public class LayoutManager
{
    // Layout Config
    public float HeaderHeight { get; private set; } = 28;
    
    // NEW: Panel System sin barras de título
    private readonly PanelSystem _panelSystem;

    // Bounds
    public SKRect HeaderRect { get; private set; }
    public SKRect LeftToolbarRect { get; private set; }
    public SKRect ChartRect { get; private set; }

    // Renderers
    private readonly SidebarRenderer _sidebar;
    private readonly LeftToolbarRenderer _leftToolbar;
    private readonly StatusBarRenderer _statusBar;
    
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

        // 2. Get chart area (área no ocupada por paneles dockeados)
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
        // El toolbar está dentro del ContentBounds del chart panel
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        if (chartPanel == null || chartPanel.IsClosed) return null;
        
        var contentArea = chartPanel.ContentBounds;
        var toolbarRect = new SKRect(contentArea.Left, contentArea.Top, 
            contentArea.Left + LeftToolbarRenderer.ToolbarWidth, contentArea.Bottom);
        
        if (toolbarRect.Contains(x, y))
        {
            float localY = y - contentArea.Top;
            float buttonY = 8;
            const float ButtonSize = 44;
            const float ButtonSpacing = 4;
            
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

        // CAPA 0: Panel backgrounds + chrome
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
    
    public void TogglePanel(string panelId) => _panelSystem.TogglePanel(panelId);

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

        switch (panel.Config.Id)
        {
            case PanelDefinitions.ORDERBOOK:
                _sidebar.RenderOrderBook(canvas, contentWidth, contentHeight, _lastOrderBook);
                break;
            case PanelDefinitions.TRADES:
                _sidebar.RenderTrades(canvas, contentWidth, contentHeight, _lastTrades);
                break;
            case PanelDefinitions.POSITIONS:
                RenderPositionsPanel(canvas, contentWidth, contentHeight);
                break;
            case PanelDefinitions.AI_ASSISTANT:
                RenderAIAssistantPanel(canvas, contentWidth, contentHeight);
                break;
            case PanelDefinitions.PORTFOLIO:
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
    }

    /// <summary>
    /// Re-renders dragging panel content on top of the overlay chrome.
    /// Called by PanelSystem.RenderOverlay via delegate.
    /// </summary>
    public void RenderDraggingPanelContent(SKCanvas canvas, DockablePanel panel)
    {
        if (panel.Config.Id == PanelDefinitions.CHART) return;
        RenderSinglePanelContent(canvas, panel);
    }

    private void RenderAIAssistantPanel(SKCanvas canvas, float width, float height)
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
            float chatTop = 48;
            float inputH = 42;
            
            // Header
            paint.Color = new SKColor(70, 140, 255);
            canvas.DrawText("Omnijure AI", 12, 18, fontBold, paint);
            
            // AI sparkle icon
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Star,
                fontBold.MeasureText("Omnijure AI") + 16, 6, 12, new SKColor(70, 140, 255));
            
            paint.Color = new SKColor(70, 75, 85);
            canvas.DrawText("GPT-4o  |  Connected", 12, 34, fontSmall, paint);
            
            // Online indicator
            paint.Color = new SKColor(46, 204, 113);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(width - 16, 26, 4, paint);

            // Separator
            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(8, 42, width - 8, 42, paint);

            // Chat area background
            paint.Style = SKPaintStyle.Fill;
            float chatBottom = height - inputH - 8;
            float y = chatTop + 12;

            // ?? User message 1 ??
            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "What pattern is forming on BTCUSDT 1h?", pad);
            
            // ?? AI response 1 ??
            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false, 
                "I see a bullish flag forming on BTC/USDT 1h chart. The price consolidated between $68,800-$69,100 after a strong impulse move.\n\n> Entry: Break above $69,100\n> Target: $69,800 (+1.0%)\n> Stop: $68,600 (-0.7%)\n> R:R ratio: 1.43",
                pad);

            // ?? User message 2 ??
            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Run RSI divergence scan on top 10 pairs", pad);

            // ?? AI response 2 (with code block) ??
            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Scanning 10 pairs for RSI divergence...\n\n[+] ETHUSDT - Bullish divergence (RSI 34)\n[+] SOLUSDT - Bullish divergence (RSI 29)\n[!] BNBUSDT - Neutral (RSI 48)\n[-] XRPUSDT - Bearish divergence (RSI 72)",
                pad);

            // Input box
            paint.Color = new SKColor(28, 32, 40);
            paint.Style = SKPaintStyle.Fill;
            var inputRect = new SKRect(8, height - inputH, width - 8, height - 8);
            canvas.DrawRoundRect(inputRect, 8, 8, paint);
            
            paint.Color = new SKColor(40, 45, 55);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawRoundRect(inputRect, 8, 8, paint);
            
            paint.Style = SKPaintStyle.Fill;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("Ask about patterns, strategies, or scan markets...", 16, height - 22, fontSmall, paint);
            
            // Send button
            paint.Color = new SKColor(56, 139, 253);
            canvas.DrawRoundRect(new SKRect(width - 40, height - inputH + 6, width - 14, height - 14), 4, 4, paint);
            paint.Color = SKColors.White;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            paint.StrokeCap = SKStrokeCap.Round;
            canvas.DrawLine(width - 32, height - 24, width - 22, height - 24, paint);
            canvas.DrawLine(width - 25, height - 30, width - 22, height - 24, paint);
            canvas.DrawLine(width - 25, height - 18, width - 22, height - 24, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private float DrawChatBubble(SKCanvas canvas, SKPaint paint, SKFont font, SKFont timeFont, 
        float panelW, float y, bool isUser, string text, float pad)
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
        string time = isUser ? "You \u2022 just now" : "AI \u2022 just now";
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

    private void RenderPositionsPanel(SKCanvas canvas, float width, float height)
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
            float rowH = 48;

            // ?? Summary bar ??
            float sumY = 4;
            paint.Color = new SKColor(22, 26, 34);
            canvas.DrawRoundRect(new SKRect(px, sumY, width - px, sumY + 28), 4, 4, paint);
            
            float sx = px + 10;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("Margin", sx, sumY + 12, fontSmall, paint);
            paint.Color = new SKColor(200, 205, 215);
            canvas.DrawText("$2,140.00", sx, sumY + 24, fontSmall, paint);
            
            sx += 80;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("Unrealized PnL", sx, sumY + 12, fontSmall, paint);
            paint.Color = new SKColor(46, 204, 113);
            canvas.DrawText("+$187.42", sx, sumY + 24, fontSmall, paint);
            
            sx += 90;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("ROE", sx, sumY + 12, fontSmall, paint);
            paint.Color = new SKColor(46, 204, 113);
            canvas.DrawText("+8.76%", sx, sumY + 24, fontSmall, paint);
            
            sx += 65;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("Positions", sx, sumY + 12, fontSmall, paint);
            paint.Color = new SKColor(200, 205, 215);
            canvas.DrawText("4 open", sx, sumY + 24, fontSmall, paint);

            // ?? Column headers ??
            float headerY = sumY + 38;
            paint.Color = new SKColor(65, 70, 80);
            
            float[] cols = [px + 4, px + 80, px + 155, px + 235, px + 320, width - 80];
            string[] headers = ["Symbol", "Side/Size", "Entry", "Mark", "PnL (ROE)", "Actions"];
            for (int i = 0; i < headers.Length && i < cols.Length; i++)
            {
                if (cols[i] < width)
                    canvas.DrawText(headers[i], cols[i], headerY, fontHeader, paint);
            }
            
            // Divider
            headerY += 6;
            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(px, headerY, width - px, headerY, paint);
            paint.Style = SKPaintStyle.Fill;
            
            float y = headerY + 6;

            // ?? Position rows ??
            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "BTCUSDT", "Long", "0.015 BTC",
                "$67,240.00", "$69,061.00",
                "+$27.32", "+2.71%", true, "10x");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "ETHUSDT", "Long", "0.85 ETH",
                "$3,420.00", "$3,512.40",
                "+$78.54", "+2.70%", true, "5x");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "SOLUSDT", "Short", "12.0 SOL",
                "$158.20", "$153.80",
                "+$52.80", "+2.78%", true, "10x");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "BNBUSDT", "Long", "1.5 BNB",
                "$612.40", "$608.50",
                "-$5.85", "-0.64%", false, "3x");
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private static void DrawPositionRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont, SKFont boldFont,
        float[] cols, float width, ref float y, float rowH,
        string symbol, string side, string size,
        string entry, string mark,
        string pnl, string roe, bool isProfit, string leverage)
    {
        float px = cols[0] - 4;
        
        // Hover-style row background (alternating subtle)
        paint.Color = new SKColor(18, 22, 30);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(px, y, width - 8, y + rowH - 2), 4, 4, paint);
        
        // Left accent bar (green for profit, red for loss)
        paint.Color = isProfit ? new SKColor(46, 204, 113, 180) : new SKColor(239, 83, 80, 180);
        canvas.DrawRoundRect(new SKRect(px, y + 4, px + 3, y + rowH - 6), 1.5f, 1.5f, paint);
        
        float midY = y + rowH / 2;
        float topY = midY - 6;
        float botY = midY + 8;

        // Symbol + leverage badge
        paint.Color = new SKColor(215, 220, 230);
        canvas.DrawText(symbol, cols[0] + 4, topY, boldFont, paint);
        
        // Leverage pill
        float levX = cols[0] + 4 + boldFont.MeasureText(symbol) + 4;
        float levW = smallFont.MeasureText(leverage) + 8;
        paint.Color = new SKColor(50, 55, 70);
        canvas.DrawRoundRect(new SKRect(levX, topY - 9, levX + levW, topY + 2), 3, 3, paint);
        paint.Color = new SKColor(160, 165, 175);
        canvas.DrawText(leverage, levX + 4, topY - 1, smallFont, paint);
        
        // Side + Size (second line under symbol)
        bool isLong = side == "Long";
        paint.Color = isLong ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
        canvas.DrawText(side, cols[0] + 4, botY, smallFont, paint);
        paint.Color = new SKColor(100, 105, 115);
        float sideW = smallFont.MeasureText(side);
        canvas.DrawText(" " + size, cols[0] + 4 + sideW, botY, smallFont, paint);

        // Entry price
        if (cols[2] < width - 40)
        {
            paint.Color = new SKColor(180, 185, 195);
            canvas.DrawText(entry, cols[2], topY, font, paint);
        }

        // Mark price
        if (cols[3] < width - 40)
        {
            paint.Color = new SKColor(210, 215, 225);
            canvas.DrawText(mark, cols[3], topY, boldFont, paint);
        }

        // PnL + ROE
        if (cols[4] < width - 40)
        {
            paint.Color = isProfit ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
            canvas.DrawText(pnl, cols[4], topY, boldFont, paint);
            canvas.DrawText(roe, cols[4], botY, smallFont, paint);
        }

        // Close button
        if (cols.Length > 5 && cols[5] < width)
        {
            float btnX = cols[5];
            float btnY = midY - 9;
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

    public bool IsDraggingPanel => _panelSystem.IsDraggingPanel;
    public bool IsResizingPanel => _panelSystem.IsResizing;
}
