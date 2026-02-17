using Omnijure.Core.Settings;
using Omnijure.Core.Scripting;
using Omnijure.Visual;
using SkiaSharp;
using Silk.NET.Maths;
using Omnijure.Core.DataStructures;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Rendering;

public class LayoutManager
{
    // Layout Config
    public float HeaderHeight { get; private set; } = 28;

    // Panel System
    private readonly PanelSystem _panelSystem;

    // Bounds
    public SKRect HeaderRect { get; private set; }
    public SKRect LeftToolbarRect { get; private set; }
    public SKRect ChartRect { get; private set; }

    // Renderers
    private readonly SidebarRenderer _sidebar;
    private readonly LeftToolbarRenderer _leftToolbar;
    private readonly StatusBarRenderer _statusBar;
    private readonly PanelContentRenderer _panelContent;
    private readonly SecondaryToolbarRenderer _secondaryToolbar;

    // Chart tab bar
    private ChartTabManager _chartTabs;
    private SKRect _chartTabBarRect;
    private readonly List<(int tabIndex, SKRect rect, SKRect closeRect)> _chartTabRects = new();
    private SKRect _addTabButtonRect;
    private const float ChartTabBarHeight = 28f;

    // Asset info for secondary toolbar
    private float _assetPrice;
    private float _assetChange;

    // Legacy properties for backward compatibility
    public bool IsResizingLeft => false;
    public bool IsResizingRight => false;

    public void SetChartTabs(ChartTabManager tabs) => _chartTabs = tabs;

    public LayoutManager()
    {
        _sidebar = new SidebarRenderer();
        _leftToolbar = new LeftToolbarRenderer();
        _statusBar = new StatusBarRenderer();
        _secondaryToolbar = new SecondaryToolbarRenderer();
        _panelSystem = new PanelSystem();
        _panelContent = new PanelContentRenderer(_panelSystem, _sidebar);
    }

    public float TotalHeaderHeight => HeaderHeight + SecondaryToolbarRenderer.ToolbarHeight;

    public void UpdateLayout(int width, int height)
    {
        // 0. Header (main + secondary toolbar)
        HeaderRect = new SKRect(0, 0, width, HeaderHeight);

        // 1. Update panel system (topEdgeY accounts for both toolbars)
        _panelSystem.Update(width, height, TotalHeaderHeight);

        // 2. ChartRect = actual chart panel bounds
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        if (chartPanel != null && !chartPanel.IsClosed)
        {
            ChartRect = chartPanel.Bounds;
        }
        else
        {
            var chartArea = _panelSystem.GetChartArea(width, height, TotalHeaderHeight);
            ChartRect = new SKRect(chartArea.Left, TotalHeaderHeight, chartArea.Right, chartArea.Bottom);
        }

        // 3. Left Toolbar inside chart
        LeftToolbarRect = new SKRect(ChartRect.Left, ChartRect.Top,
            ChartRect.Left + LeftToolbarRenderer.ToolbarWidth, ChartRect.Bottom);
    }

    public void HandleMouseDown(float x, float y)
    {
        _panelSystem.OnMouseDown(x, y);
    }

    // Secondary toolbar interaction
    public void UpdateSecondaryToolbarMouse(float x, float y) => _secondaryToolbar.UpdateMousePos(x, y);
    public string? HandleSecondaryToolbarClick(float x, float y) => _secondaryToolbar.GetButtonAtPosition(x, y);
    public bool IsInSecondaryToolbar(float x, float y) => _secondaryToolbar.Contains(x, y);

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
        _panelSystem.OnMouseMove(x, y, screenWidth, screenHeight, TotalHeaderHeight);
    }

    public Omnijure.Visual.Drawing.DrawingTool? HandleToolbarClick(float x, float y)
    {
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
        int screenWidth, int screenHeight, List<ScriptOutput>? scriptOutputs = null)
    {
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

        // CAPA 0.75: Secondary toolbar (context-sensitive action bar)
        var activeCenterForToolbar = _panelSystem.GetActiveCenterTabId();
        _secondaryToolbar.Render(canvas, screenWidth, activeCenterForToolbar, chartType, interval,
            symbol, _assetPrice, _assetChange);

        // CAPA 1: Center content (Chart or other active center panel)
        var activeCenterId = _panelSystem.GetActiveCenterTabId();
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        bool chartIsActiveCenter = activeCenterId == PanelDefinitions.CHART
            && chartPanel != null && !chartPanel.IsClosed;

        if (chartIsActiveCenter)
        {
            var contentArea = chartPanel.ContentBounds;

            // Render chart tab bar at the top of content area
            bool hasTabs = _chartTabs != null && _chartTabs.Count > 0;
            float tabBarH = hasTabs ? ChartTabBarHeight : 0;

            if (hasTabs)
            {
                _chartTabBarRect = new SKRect(contentArea.Left, contentArea.Top,
                    contentArea.Right, contentArea.Top + tabBarH);
                RenderChartTabBar(canvas, _chartTabBarRect);
            }

            // Adjusted content area (below tab bar)
            var chartArea = new SKRect(contentArea.Left, contentArea.Top + tabBarH,
                contentArea.Right, contentArea.Bottom);

            canvas.Save();
            canvas.ClipRect(chartArea);
            canvas.Translate(chartArea.Left, chartArea.Top);

            var toolbarMousePos = new Vector2D<float>(
                mousePos.X - chartArea.Left, mousePos.Y - chartArea.Top);
            _leftToolbar.Render(canvas, chartArea.Height,
                drawingState?.ActiveTool ?? Omnijure.Visual.Drawing.DrawingTool.None,
                toolbarMousePos.X, toolbarMousePos.Y);

            canvas.Save();
            canvas.Translate(LeftToolbarRenderer.ToolbarWidth, 0);
            canvas.ClipRect(new SKRect(0, 0,
                chartArea.Width - LeftToolbarRenderer.ToolbarWidth, chartArea.Height));

            var chartMousePos = new Vector2D<float>(
                mousePos.X - chartArea.Left - LeftToolbarRenderer.ToolbarWidth,
                mousePos.Y - chartArea.Top);

            chartRenderer.Render(canvas,
                (int)(chartArea.Width - LeftToolbarRenderer.ToolbarWidth),
                (int)chartArea.Height,
                buffer, decision, scrollOffset, zoom, symbol, interval, chartType, buttons,
                minPrice, maxPrice, chartMousePos, drawingState, scriptOutputs);

            canvas.Restore();
            canvas.Restore();
        }
        else if (activeCenterId != PanelDefinitions.CHART)
        {
            // Non-chart center panel (e.g. Script Editor) — rendered by PanelContentRenderer
        }
        else
        {
            RenderEmptyState(canvas, ChartRect);
        }

        // CAPA 2: Sidebar panel content
        _panelContent.RenderPanelContent(canvas, orderBook, trades, buffer);

        // CAPA 3: Dock zone preview + dragging panel
        _panelSystem.RenderOverlay(canvas, _panelContent.RenderDraggingPanelContent);

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

    public void SetActiveScriptManager(Omnijure.Core.Scripting.ScriptManager? scripts) => _panelContent.SetActiveScriptManager(scripts);

    public void UpdateAssetInfo(float price, float change) { _assetPrice = price; _assetChange = change; }

    public void UpdateFps(int fps) => _statusBar.UpdateFps(fps);

    public void UpdateChartTitle(string symbol, string interval, float price)
    {
        _panelSystem.UpdateChartTitle(symbol, interval, price);
    }

    // ═══════════════════════════════════════════════════════════════
    // Chart Tab Bar (Chrome-style tabs at top of center panel)
    // ═══════════════════════════════════════════════════════════════

    private void RenderChartTabBar(SKCanvas canvas, SKRect barRect)
    {
        if (_chartTabs == null || _chartTabs.Count == 0) return;

        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.IsAntialias = true;

            // Tab bar background
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(barRect, paint);

            // Bottom border
            paint.Color = new SKColor(40, 45, 55);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(barRect.Left, barRect.Bottom, barRect.Right, barRect.Bottom, paint);

            _chartTabRects.Clear();

            using var tabFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            float tabX = barRect.Left + 4;
            float tabY = barRect.Top + 2;
            float tabH = ChartTabBarHeight - 4;

            for (int i = 0; i < _chartTabs.Tabs.Count; i++)
            {
                var tab = _chartTabs.Tabs[i];
                bool isActive = i == _chartTabs.ActiveIndex;
                string label = $"{tab.Symbol} \u00B7 {tab.Timeframe}";
                float labelW = tabFont.MeasureText(label);
                float closeW = 16;
                float tabW = labelW + 28 + closeW;

                var tabRect = new SKRect(tabX, tabY, tabX + tabW, tabY + tabH);
                var closeRect = new SKRect(tabX + tabW - closeW - 4, tabY + (tabH - 12) / 2,
                    tabX + tabW - 4, tabY + (tabH - 12) / 2 + 12);
                _chartTabRects.Add((i, tabRect, closeRect));

                // Tab background
                paint.Style = SKPaintStyle.Fill;
                if (isActive)
                {
                    paint.Color = new SKColor(30, 34, 42);
                    canvas.DrawRoundRect(new SKRoundRect(tabRect, 4, 4), paint);

                    // Active indicator line at bottom
                    paint.Color = new SKColor(56, 139, 253);
                    canvas.DrawRect(tabX + 4, tabY + tabH - 2, tabW - 8, 2, paint);
                }

                // Label
                paint.Style = SKPaintStyle.Fill;
                paint.Color = isActive ? new SKColor(220, 225, 235) : new SKColor(100, 108, 118);
                canvas.DrawText(label, tabX + 8, tabY + tabH / 2 + 4, tabFont, paint);

                // Close button
                if (_chartTabs.Count > 1)
                {
                    paint.Color = isActive ? new SKColor(140, 145, 155) : new SKColor(80, 85, 95);
                    paint.Style = SKPaintStyle.Stroke;
                    paint.StrokeWidth = 1.5f;
                    float cx = closeRect.MidX;
                    float cy = closeRect.MidY;
                    float cs = 3.5f;
                    canvas.DrawLine(cx - cs, cy - cs, cx + cs, cy + cs, paint);
                    canvas.DrawLine(cx + cs, cy - cs, cx - cs, cy + cs, paint);
                }

                tabX += tabW + 2;
            }

            // (+) Add tab button
            float addBtnSize = tabH;
            _addTabButtonRect = new SKRect(tabX, tabY, tabX + addBtnSize, tabY + addBtnSize);

            paint.Style = SKPaintStyle.Fill;
            paint.Color = new SKColor(25, 29, 36);
            canvas.DrawRoundRect(new SKRoundRect(_addTabButtonRect, 4, 4), paint);

            paint.Color = new SKColor(100, 108, 118);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1.5f;
            float plusCx = _addTabButtonRect.MidX;
            float plusCy = _addTabButtonRect.MidY;
            float plusSize = 5f;
            canvas.DrawLine(plusCx - plusSize, plusCy, plusCx + plusSize, plusCy, paint);
            canvas.DrawLine(plusCx, plusCy - plusSize, plusCx, plusCy + plusSize, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    /// <summary>
    /// Handles mouse click on the chart tab bar. Returns true if click was consumed.
    /// </summary>
    public bool HandleChartTabClick(float x, float y, ChartTabManager chartTabs, Action onTabSwitch)
    {
        if (chartTabs == null || chartTabs.Count == 0) return false;

        if (_addTabButtonRect.Contains(x, y))
        {
            chartTabs.AddTab();
            onTabSwitch?.Invoke();
            return true;
        }

        foreach (var (tabIndex, tabRect, closeRect) in _chartTabRects)
        {
            if (tabRect.Contains(x, y))
            {
                if (chartTabs.Count > 1 && closeRect.Contains(x, y))
                {
                    chartTabs.CloseTab(tabIndex);
                    onTabSwitch?.Invoke();
                    return true;
                }

                if (tabIndex != chartTabs.ActiveIndex)
                {
                    chartTabs.SwitchTo(tabIndex);
                    onTabSwitch?.Invoke();
                }
                return true;
            }
        }

        return false;
    }

    private void RenderEmptyState(SKCanvas canvas, SKRect area)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(area, paint);

            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 18);
            paint.Color = new SKColor(100, 105, 115);
            paint.IsAntialias = true;

            string message = "No active panels";
            float textWidth = TextMeasureCache.Instance.MeasureText(message, font);
            canvas.DrawText(message, area.MidX - textWidth / 2, area.MidY, font, paint);

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

    public bool IsMouseOverPanel(float x, float y)
    {
        return _panelSystem.IsMouseOverPanel(x, y);
    }

    /// <summary>
    /// Delegates panel scroll handling to PanelContentRenderer.
    /// </summary>
    public bool HandlePanelScroll(float x, float y, float deltaY)
    {
        return _panelContent.HandlePanelScroll(x, y, deltaY);
    }

    /// <summary>
    /// Returns the price axis strip rect in screen coordinates (right 60px of chart content area).
    /// </summary>
    public SKRect GetPriceAxisRect()
    {
        var chartPanel = _panelSystem.GetPanel(PanelDefinitions.CHART);
        if (chartPanel == null || chartPanel.IsClosed) return SKRect.Empty;
        var c = chartPanel.ContentBounds;
        return new SKRect(c.Right - 60, c.Top, c.Right, c.Bottom);
    }

    public bool IsDraggingPanel => _panelSystem.IsDraggingPanel;
    public bool IsResizingPanel => _panelSystem.IsResizing;

    // Script editor passthrough
    public bool HandleScriptEditorClick(float x, float y) => _panelContent.HandleScriptEditorClick(x, y);
    public void ScriptEditorInsertChar(char ch) => _panelContent.InsertChar(ch);
    public void ScriptEditorHandleKey(PanelContentRenderer.EditorKey key) => _panelContent.HandleEditorKey(key);
    public bool IsScriptEditorFocused { get => _panelContent.IsEditorFocused; set => _panelContent.IsEditorFocused = value; }
    public int ScriptEditorActiveScript { get => _panelContent.EditorActiveScript; set => _panelContent.EditorActiveScript = value; }

    // Layout persistence
    public List<PanelState> ExportLayout() => _panelSystem.ExportLayout();
    public void ImportLayout(List<PanelState> states) => _panelSystem.ImportLayout(states);
    public void ImportActiveTabs(string bottom, string left, string right, string center = "") => _panelSystem.ImportActiveTabs(bottom, left, right, center);
    public (string bottom, string left, string right, string center) ExportActiveTabs() => _panelSystem.ExportActiveTabs();
}
