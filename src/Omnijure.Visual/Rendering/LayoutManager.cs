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
                RenderPlaceholderPanel(canvas, contentWidth, contentHeight, "No open positions", "Positions will appear here when trading");
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
            
            paint.IsAntialias = true;
            paint.Color = new SKColor(70, 140, 255);
            canvas.DrawText("\u2726 Omnijure AI", 12, 20, fontBold, paint);
            
            paint.Color = new SKColor(100, 105, 115);
            canvas.DrawText("Ask about patterns, strategies, or analysis", 12, 40, fontNormal, paint);
            
            // Chat area
            paint.Color = new SKColor(25, 28, 35);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(new SKRect(8, 54, width - 8, height - 50), 6, 6, paint);
            
            paint.Color = new SKColor(70, 75, 85);
            canvas.DrawText("No messages yet", 20, 82, fontNormal, paint);
            
            // Input box
            paint.Color = new SKColor(30, 34, 42);
            canvas.DrawRoundRect(new SKRect(8, height - 42, width - 8, height - 8), 6, 6, paint);
            
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("Ask Omnijure AI...", 16, height - 20, fontNormal, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private void RenderPortfolioPanel(SKCanvas canvas, float width, float height)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 11);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            
            paint.IsAntialias = true;
            float y = 14;
            
            paint.Color = new SKColor(130, 135, 145);
            canvas.DrawText("ACCOUNTS", 10, y, fontBold, paint);
            y += 24;
            
            paint.Color = new SKColor(46, 204, 113);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(18, y - 4, 4, paint);
            paint.Color = new SKColor(200, 205, 215);
            canvas.DrawText("Binance Spot", 28, y, fontNormal, paint);
            y += 28;
            
            paint.Color = new SKColor(130, 135, 145);
            canvas.DrawText("ACTIVE BOTS", 10, y, fontBold, paint);
            y += 22;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("No active bots", 10, y, fontNormal, paint);
            y += 28;
            
            paint.Color = new SKColor(130, 135, 145);
            canvas.DrawText("DATASETS", 10, y, fontBold, paint);
            y += 22;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("No loaded datasets", 10, y, fontNormal, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
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
