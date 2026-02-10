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
        // ORDEN DE RENDERIZADO (tiling window manager / IDE):
        //   CAPA 0: Todos los panel backgrounds + chrome (incluyendo chart)
        //   CAPA 1: Chart content DENTRO del panel (sobre su fondo)
        //   CAPA 2: Panel content (sidebars, sobre todo)
        //   CAPA 3: Dock zone preview + dragging panel
        //   CAPA 4: Status bar
        // ???????????????????????????????????????????????????????????

        // CAPA 0: Panel backgrounds + chrome (TODOS, incluyendo center/chart)
        _panelSystem.Render(canvas);

        // CAPA 1: Chart content DENTRO del panel ya dibujado
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        bool hasChart = chartPanel != null && !chartPanel.IsClosed;

        if (hasChart)
        {
            // Usar ContentBounds del panel (ya excluye el header del panel)
            var contentArea = chartPanel.ContentBounds;
            
            canvas.Save();
            canvas.ClipRect(contentArea);
            canvas.Translate(contentArea.Left, contentArea.Top);
            
            // Toolbar dentro del chart
            var toolbarMousePos = new Vector2D<float>(
                mousePos.X - contentArea.Left, mousePos.Y - contentArea.Top);
            _leftToolbar.Render(canvas, contentArea.Height,
                drawingState?.ActiveTool ?? Omnijure.Visual.Drawing.DrawingTool.None, 
                toolbarMousePos.X, toolbarMousePos.Y);
            
            // Chart (offset por toolbar)
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

        // CAPA 2: Sidebar panel content (SOBRE el chart)
        RenderPanelContent(canvas, orderBook, trades, buffer);

        // CAPA 4: Status bar
        _statusBar.Render(canvas, screenWidth, screenHeight);
        
        // CAPA 5: Window border (1px, respeta paleta)
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

    private void RenderPanelContent(SKCanvas canvas, OrderBook orderBook, RingBuffer<MarketTrade> trades, RingBuffer<Candle> buffer)
    {
        foreach (var panel in _panelSystem.Panels)
        {
            // Skip: cerrados, colapsados, el chart (se renderiza aparte)
            if (panel.IsClosed || panel.IsCollapsed) continue;
            if (panel.Config.Id == PanelDefinitions.CHART) continue;

            canvas.Save();
            canvas.ClipRect(panel.ContentBounds);
            canvas.Translate(panel.ContentBounds.Left, panel.ContentBounds.Top);

            var contentWidth = panel.ContentBounds.Width;
            var contentHeight = panel.ContentBounds.Height;

            switch (panel.Config.Id)
            {
                case PanelDefinitions.ORDERBOOK:
                    _sidebar.RenderOrderBook(canvas, contentWidth, contentHeight, orderBook);
                    break;
                case PanelDefinitions.TRADES:
                    _sidebar.RenderTrades(canvas, contentWidth, contentHeight, trades);
                    break;
                case PanelDefinitions.POSITIONS:
                    _sidebar.RenderPositions(canvas, contentWidth, contentHeight);
                    break;
                case PanelDefinitions.ALERTS:
                    RenderAlertsPanel(canvas, contentWidth, contentHeight);
                    break;
            }

            canvas.Restore();
        }
    }

    private void RenderAlertsPanel(SKCanvas canvas, float width, float height)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            paint.Color = new SKColor(160, 165, 175);
            canvas.DrawText("No active alerts", 10, 30, font, paint);
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
}
