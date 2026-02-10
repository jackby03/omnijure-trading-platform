using SkiaSharp;
using Silk.NET.Maths;
using Omnijure.Core.DataStructures;
using System.Linq;

namespace Omnijure.Visual.Rendering;

public class LayoutManager
{
    // Layout Config
    public float HeaderHeight { get; private set; } = 50;
    
    // NEW: Panel System sin barras de título
    private readonly PanelSystem _panelSystem;

    // Bounds
    public SKRect HeaderRect { get; private set; }
    public SKRect LeftToolbarRect { get; private set; }
    public SKRect ChartRect { get; private set; }

    // Renderers
    private readonly SidebarRenderer _sidebar;
    private readonly LeftToolbarRenderer _leftToolbar;
    
    // Legacy properties for backward compatibility
    public bool IsResizingLeft => false;
    public bool IsResizingRight => false;
    
    public LayoutManager()
    {
        _sidebar = new SidebarRenderer();
        _leftToolbar = new LeftToolbarRenderer();
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
        if (LeftToolbarRect.Contains(x, y))
        {
            float localY = y - LeftToolbarRect.Top;
            // Calculate which button was clicked
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
        RingBuffer<MarketTrade> trades, Omnijure.Visual.Drawing.DrawingToolState? drawingState)
    {
        // ???????????????????????????????????????????????????????????
        // ORDEN DE RENDERIZADO (como tiling window manager / IDE):
        //   CAPA 0: Chart (fondo, se pinta PRIMERO)
        //   CAPA 1: Panel backgrounds + handles (sobre chart)
        //   CAPA 2: Panel content (dentro de cada panel)
        //   CAPA 3: Dock zone preview (sobre todo)
        //   CAPA 4: Panel arrastrándose (capa superior)
        // ???????????????????????????????????????????????????????????

        // CAPA 0: Render Chart PRIMERO (fondo, por debajo de todo)
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        bool hasChart = chartPanel != null && !chartPanel.IsClosed;

        if (hasChart)
        {
            canvas.Save();
            canvas.ClipRect(ChartRect); // Clip en coordenadas absolutas
            canvas.Translate(ChartRect.Left, ChartRect.Top);
            
            // Toolbar interno
            var toolbarMousePos = new Vector2D<float>(mousePos.X - ChartRect.Left, mousePos.Y - ChartRect.Top);
            _leftToolbar.Render(canvas, ChartRect.Height,
                drawingState?.ActiveTool ?? Omnijure.Visual.Drawing.DrawingTool.None, 
                toolbarMousePos.X, toolbarMousePos.Y);
            
            // Chart (offset por toolbar)
            canvas.Save();
            canvas.Translate(LeftToolbarRenderer.ToolbarWidth, 0);
            canvas.ClipRect(new SKRect(0, 0, ChartRect.Width - LeftToolbarRenderer.ToolbarWidth, ChartRect.Height));
            
            var chartMousePos = new Vector2D<float>(
                mousePos.X - ChartRect.Left - LeftToolbarRenderer.ToolbarWidth, 
                mousePos.Y - ChartRect.Top);
            
            chartRenderer.Render(canvas, (int)(ChartRect.Width - LeftToolbarRenderer.ToolbarWidth), (int)ChartRect.Height, 
                buffer, decision, scrollOffset, zoom, symbol, interval, chartType, buttons, minPrice, maxPrice, 
                chartMousePos, drawingState);
            
            canvas.Restore();
            canvas.Restore();
        }
        else
        {
            RenderEmptyState(canvas, ChartRect);
        }

        // CAPA 1+2: Render panels y su contenido (SOBRE el chart)
        _panelSystem.Render(canvas);
        RenderPanelContent(canvas, orderBook, trades, buffer);
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
                case PanelDefinitions.WATCHLIST:
                    _sidebar.RenderWatchlist(canvas, contentWidth, contentHeight);
                    break;
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
