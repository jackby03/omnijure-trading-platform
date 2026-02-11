using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Sistema de paneles moderno sin barras de t�tulo.
/// Interacci�n mediante handles minimalistas en las esquinas.
/// Estilo inspirado en herramientas profesionales como OBS/Figma.
/// </summary>
public class PanelSystem
{
    private readonly Dictionary<string, DockablePanel> _panels = new();
    private DockablePanel? _draggingPanel;
    private DockablePanel? _potentialDragPanel;
    private SKPoint _mouseDownPosition;
    private DockablePanel? _hoveredPanel;
    private string? _hoveredHandle;
    private DockablePanel? _activePanel;
    private SKPoint _dragOffset;
    private DockZone? _currentDockZone;
    
    // ? Guardar estado original para restaurar si se cancela el drag
    private PanelPosition _originalPosition;
    private bool _originalIsFloating;
    private SKRect _originalBounds;
    
    private const float HandleSize = 24f;
    private const float CollapsedWidth = 32f;
    private const float HandlePadding = 6f;
    private const float DragThreshold = 5f;
    private const float PanelGap = 4f;
    private const float ResizeEdgeWidth = 6f;
    
    // Dock guide constants (VS-style)
    private const float GuideButtonSize = 36f;
    private const float GuideHitRadius = 22f;
    private const float EdgeGuideMargin = 20f;
    
    // Cached layout for dock guides
    private float _lastHeaderHeight;
    private int _lastScreenWidth;
    private int _lastScreenHeight;
    
    // Resize state
    private DockablePanel? _resizingPanel;
    private ResizeEdge _resizeEdge;
    private float _resizeStartMousePos;
    private float _resizeStartSize;
    
    private enum ResizeEdge { None, Right, Left, Top, Bottom }
    
    // Bottom tab system
    private const float TabBarHeight = 28f;
    private string _activeBottomTabId = PanelDefinitions.ORDERBOOK;
    private SKRect _bottomTabBarRect;
    private List<(string id, SKRect rect)> _bottomTabRects = new();
    
    // Side tab system (Left/Right docking with tabs)
    private readonly Dictionary<PanelPosition, string> _activeTabIds = new()
    {
        [PanelPosition.Left] = PanelDefinitions.AI_ASSISTANT,
        [PanelPosition.Right] = PanelDefinitions.PORTFOLIO
    };
    private readonly Dictionary<PanelPosition, List<(string id, SKRect rect)>> _sideTabRects = new()
    {
        [PanelPosition.Left] = new(),
        [PanelPosition.Right] = new()
    };
    private readonly Dictionary<PanelPosition, SKRect> _sideTabBarRects = new();

    public IReadOnlyCollection<DockablePanel> Panels => _panels.Values;

    public PanelSystem()
    {
        foreach (var config in PanelDefinitions.Panels.Values)
        {
            CreatePanel(config.Id);
        }
    }

    private void CreatePanel(string panelId)
    {
        if (!PanelDefinitions.Panels.TryGetValue(panelId, out var config))
            return;

        var panel = new DockablePanel(config);
        if (config.StartClosed)
            panel.IsClosed = true;
        _panels[panelId] = panel;
    }

    public void Update(int screenWidth, int screenHeight, float headerHeight)
    {
        _lastScreenWidth = screenWidth;
        _lastScreenHeight = screenHeight;
        _lastHeaderHeight = headerHeight;
        
        float statusBarHeight = StatusBarRenderer.Height;
        float availableBottom = screenHeight - statusBarHeight - PanelGap;
        
        float currentLeftX = PanelGap;
        float currentRightX = screenWidth - PanelGap;
        float currentBottomY = availableBottom;
        float topEdgeY = headerHeight + PanelGap;

        // PASO 1: Left panels (tabbed when multiple)
        var leftPanels = _panels.Values.Where(p => p.Position == PanelPosition.Left && !p.IsFloating && !p.IsClosed).OrderBy(p => p.DockOrder).ToList();
        if (leftPanels.Count > 0)
        {
            if (!leftPanels.Any(p => p.Config.Id == _activeTabIds.GetValueOrDefault(PanelPosition.Left)))
                _activeTabIds[PanelPosition.Left] = leftPanels[0].Config.Id;
            var activeLeft = leftPanels.First(p => p.Config.Id == _activeTabIds[PanelPosition.Left]);
            float lw = activeLeft.IsCollapsed ? CollapsedWidth : activeLeft.Width;
            if (leftPanels.Count > 1)
            {
                _sideTabBarRects[PanelPosition.Left] = new SKRect(currentLeftX, availableBottom - TabBarHeight, currentLeftX + lw - PanelGap, availableBottom);
                activeLeft.Bounds = new SKRect(currentLeftX, topEdgeY, currentLeftX + lw - PanelGap, availableBottom - TabBarHeight);
            }
            else
            {
                _sideTabBarRects.Remove(PanelPosition.Left);
                activeLeft.Bounds = new SKRect(currentLeftX, topEdgeY, currentLeftX + lw - PanelGap, availableBottom);
            }
            currentLeftX += lw;
        }
        else { _sideTabBarRects.Remove(PanelPosition.Left); }

        // PASO 2: Right panels (tabbed when multiple)
        var rightPanels = _panels.Values.Where(p => p.Position == PanelPosition.Right && !p.IsFloating && !p.IsClosed).OrderBy(p => p.DockOrder).ToList();
        if (rightPanels.Count > 0)
        {
            if (!rightPanels.Any(p => p.Config.Id == _activeTabIds.GetValueOrDefault(PanelPosition.Right)))
                _activeTabIds[PanelPosition.Right] = rightPanels[0].Config.Id;
            var activeRight = rightPanels.First(p => p.Config.Id == _activeTabIds[PanelPosition.Right]);
            float rw = activeRight.IsCollapsed ? CollapsedWidth : activeRight.Width;
            if (rightPanels.Count > 1)
            {
                _sideTabBarRects[PanelPosition.Right] = new SKRect(currentRightX - rw + PanelGap, availableBottom - TabBarHeight, currentRightX, availableBottom);
                activeRight.Bounds = new SKRect(currentRightX - rw + PanelGap, topEdgeY, currentRightX, availableBottom - TabBarHeight);
            }
            else
            {
                _sideTabBarRects.Remove(PanelPosition.Right);
                activeRight.Bounds = new SKRect(currentRightX - rw + PanelGap, topEdgeY, currentRightX, availableBottom);
            }
            currentRightX -= rw;
        }
        else { _sideTabBarRects.Remove(PanelPosition.Right); }

        // ???????????????????????????????????????????????????????????
        // PASO 3: Bottom tab group (tabbed, only active panel gets bounds)
        // ???????????????????????????????????????????????????????????
        var bottomTabs = _panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (bottomTabs.Count > 0)
        {
            // Ensure active tab is valid
            if (!bottomTabs.Any(t => t.Config.Id == _activeBottomTabId))
                _activeBottomTabId = bottomTabs[0].Config.Id;
            
            var activeTab = bottomTabs.First(t => t.Config.Id == _activeBottomTabId);
            float contentHeight = activeTab.IsCollapsed ? CollapsedWidth : activeTab.Height;
            float totalHeight = contentHeight + TabBarHeight;
            
            // Active panel bounds (content area above tab bar)
            activeTab.Bounds = new SKRect(
                currentLeftX, currentBottomY - totalHeight,
                currentRightX, currentBottomY - TabBarHeight);
            
            // Tab bar rect (below panel content)
            _bottomTabBarRect = new SKRect(
                currentLeftX, currentBottomY - TabBarHeight,
                currentRightX, currentBottomY);
            
            currentBottomY -= totalHeight;
        }
        else
        {
            _bottomTabBarRect = SKRect.Empty;
        }

        // ???????????????????????????????????????????????????????????
        // PASO 4: Center panel ocupa el espacio restante
        // ???????????????????????????????????????????????????????????
        var centerPanel = _panels.Values.FirstOrDefault(p => p.Position == PanelPosition.Center && !p.IsClosed);
        if (centerPanel != null)
        {
            centerPanel.Bounds = new SKRect(currentLeftX, topEdgeY, currentRightX, currentBottomY);
        }

        // Update handle positions for visible panels (skip inactive tabs)
        foreach (var panel in _panels.Values.Where(p => !p.IsClosed))
        {
            if (panel.Position == PanelPosition.Bottom && !panel.IsFloating && panel.Config.Id != _activeBottomTabId)
                continue;
            if ((panel.Position == PanelPosition.Left || panel.Position == PanelPosition.Right)
                && !panel.IsFloating
                && panel.Config.Id != _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id))
                continue;
            UpdatePanelHandles(panel);
        }
    }

    private void UpdatePanelHandles(DockablePanel panel)
    {
        // Drag handle (esquina superior izquierda)
        panel.DragHandleBounds = new SKRect(
            panel.Bounds.Left + HandlePadding,
            panel.Bounds.Top + HandlePadding,
            panel.Bounds.Left + HandlePadding + HandleSize,
            panel.Bounds.Top + HandlePadding + HandleSize
        );

        // Collapse handle (esquina superior derecha)
        if (panel.Config.CanCollapse)
        {
            panel.CollapseHandleBounds = new SKRect(
                panel.Bounds.Right - HandlePadding - HandleSize,
                panel.Bounds.Top + HandlePadding,
                panel.Bounds.Right - HandlePadding,
                panel.Bounds.Top + HandlePadding + HandleSize
            );
        }

        // Close handle (al lado del collapse) - SIEMPRE si puede cerrarse
        if (panel.Config.CanClose)
        {
            panel.CloseHandleBounds = new SKRect(
                panel.Bounds.Right - HandlePadding - HandleSize * 2 - 4,
                panel.Bounds.Top + HandlePadding,
                panel.Bounds.Right - HandlePadding - HandleSize - 4,
                panel.Bounds.Top + HandlePadding + HandleSize
            );
        }

        // Content bounds (�rea �til del panel)
        float topPadding = 40f; // Espacio para handles y nombre
        panel.ContentBounds = new SKRect(
            panel.Bounds.Left + 8,
            panel.Bounds.Top + topPadding,
            panel.Bounds.Right - 8,
            panel.Bounds.Bottom - 8
        );
    }

    /// <summary>
    /// Phase 1: Panel backgrounds and chrome (BEFORE chart content)
    /// </summary>
    public void Render(SKCanvas canvas)
    {
        // CAPA 1: Center panel chrome
        var centerPanel = _panels.Values.FirstOrDefault(p => p.Position == PanelPosition.Center && !p.IsClosed && p != _draggingPanel);
        if (centerPanel != null)
        {
            RenderPanel(canvas, centerPanel);
        }
        
        // CAPA 2: Bottom tab bar + active bottom panel
        RenderBottomTabBar(canvas);
        RenderSideTabBars(canvas);
        var activeBottomPanel = _panels.Values.FirstOrDefault(p => 
            p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed && 
            p.Config.Id == _activeBottomTabId && p != _draggingPanel);
        if (activeBottomPanel != null)
        {
            RenderBottomPanelContent(canvas, activeBottomPanel);
        }
        
        // CAPA 3: Docked panels (Left/Right - only active tab visible)
        foreach (var panel in _panels.Values.Where(p => !p.IsFloating && p != _draggingPanel && !p.IsClosed 
            && p.Position != PanelPosition.Center && p.Position != PanelPosition.Bottom))
        {
            if ((panel.Position == PanelPosition.Left || panel.Position == PanelPosition.Right)
                && panel.Config.Id != _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id))
                continue;
            RenderPanel(canvas, panel);
        }

        // CAPA 3.5: Resize edge indicators
        RenderResizeEdges(canvas);

        // CAPA 4: Floating panels (not dragging)
        foreach (var panel in _panels.Values.Where(p => p.IsFloating && p != _draggingPanel && !p.IsClosed))
        {
            RenderPanel(canvas, panel);
        }
    }

    private void RenderBottomTabBar(SKCanvas canvas)
    {
        var bottomTabs = _panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (bottomTabs.Count == 0 || _bottomTabBarRect.Width <= 0) return;

        var paint = PaintPool.Instance.Rent();
        try
        {
            // Tab bar background
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(_bottomTabBarRect, paint);
            
            // Top border (separator between content and tabs)
            paint.Color = new SKColor(40, 45, 55);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(_bottomTabBarRect.Left, _bottomTabBarRect.Top, 
                _bottomTabBarRect.Right, _bottomTabBarRect.Top, paint);

            _bottomTabRects.Clear();
            
            using var tabFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            float tabX = _bottomTabBarRect.Left + 4;
            float tabY = _bottomTabBarRect.Top + 2;
            float tabH = TabBarHeight - 4;
            
            foreach (var tab in bottomTabs)
            {
                bool isActive = tab.Config.Id == _activeBottomTabId;
                string label = tab.Config.DisplayName;
                float labelW = tabFont.MeasureText(label);
                float tabW = labelW + 28; // padding + icon space
                
                var tabRect = new SKRect(tabX, tabY, tabX + tabW, tabY + tabH);
                _bottomTabRects.Add((tab.Config.Id, tabRect));
                
                // Tab background
                if (isActive)
                {
                    paint.Style = SKPaintStyle.Fill;
                    paint.Color = new SKColor(30, 34, 42);
                    canvas.DrawRoundRect(new SKRoundRect(tabRect, 4, 4), paint);
                    
                    // Active indicator line at top (VS-style, tabs at bottom)
                    paint.Color = new SKColor(56, 139, 253);
                    canvas.DrawRect(tabX + 4, tabY, tabW - 8, 2, paint);
                }
                
                // Icon
                SvgIconRenderer.DrawIcon(canvas, tab.Config.Icon, 
                    tabX + 6, tabY + (tabH - 12) / 2, 12,
                    isActive ? new SKColor(56, 139, 253) : new SKColor(100, 108, 118));
                
                // Label
                paint.Style = SKPaintStyle.Fill;
                paint.Color = isActive ? new SKColor(220, 225, 235) : new SKColor(100, 108, 118);
                paint.IsAntialias = true;
                canvas.DrawText(label, tabX + 22, tabY + tabH / 2 + 4, tabFont, paint);
                
                tabX += tabW + 2;
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private void RenderBottomPanelContent(SKCanvas canvas, DockablePanel panel)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Panel background (seamless with tab bar below)
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(panel.Bounds, paint);

            // Border (top and sides only, bottom connects to tab bar)
            paint.Color = new SKColor(35, 38, 45);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(panel.Bounds.Left, panel.Bounds.Top, panel.Bounds.Right, panel.Bounds.Top, paint);
            canvas.DrawLine(panel.Bounds.Left, panel.Bounds.Top, panel.Bounds.Left, panel.Bounds.Bottom, paint);
            canvas.DrawLine(panel.Bounds.Right, panel.Bounds.Top, panel.Bounds.Right, panel.Bounds.Bottom, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
        
        // Content bounds for bottom panel
        panel.ContentBounds = new SKRect(
            panel.Bounds.Left + 8,
            panel.Bounds.Top + 8,
            panel.Bounds.Right - 8,
            panel.Bounds.Bottom - 4
        );
    }

    private void RenderSideTabBars(SKCanvas canvas)
    {
        foreach (var kvp in _sideTabBarRects)
        {
            var position = kvp.Key;
            var barRect = kvp.Value;
            
            var sideTabs = _panels.Values
                .Where(p => p.Position == position && !p.IsFloating && !p.IsClosed)
                .OrderBy(p => p.DockOrder)
                .ToList();

            if (sideTabs.Count < 2) continue;

            var paint = PaintPool.Instance.Rent();
            try
            {
                paint.Color = new SKColor(18, 20, 24);
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(barRect, paint);
                
                paint.Color = new SKColor(40, 45, 55);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 1;
                canvas.DrawLine(barRect.Left, barRect.Top, barRect.Right, barRect.Top, paint);

                _sideTabRects[position].Clear();
                
                using var tabFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
                float tabX = barRect.Left + 4;
                float tabY = barRect.Top + 2;
                float tabH = TabBarHeight - 4;
                string activeId = _activeTabIds.GetValueOrDefault(position, "");
                
                foreach (var tab in sideTabs)
                {
                    bool isActive = tab.Config.Id == activeId;
                    string label = tab.Config.DisplayName;
                    float labelW = tabFont.MeasureText(label);
                    float tabW = labelW + 28;
                    
                    var tabRect = new SKRect(tabX, tabY, tabX + tabW, tabY + tabH);
                    _sideTabRects[position].Add((tab.Config.Id, tabRect));
                    
                    if (isActive)
                    {
                        paint.Style = SKPaintStyle.Fill;
                        paint.Color = new SKColor(30, 34, 42);
                        canvas.DrawRoundRect(new SKRoundRect(tabRect, 4, 4), paint);
                        
                        // Active indicator on top (VS-style, tabs at bottom)
                        paint.Color = new SKColor(56, 139, 253);
                        canvas.DrawRect(tabX + 4, tabY, tabW - 8, 2, paint);
                    }
                    
                    SvgIconRenderer.DrawIcon(canvas, tab.Config.Icon, 
                        tabX + 6, tabY + (tabH - 12) / 2, 12,
                        isActive ? new SKColor(56, 139, 253) : new SKColor(100, 108, 118));
                    
                    paint.Style = SKPaintStyle.Fill;
                    paint.Color = isActive ? new SKColor(220, 225, 235) : new SKColor(100, 108, 118);
                    paint.IsAntialias = true;
                    canvas.DrawText(label, tabX + 22, tabY + tabH / 2 + 4, tabFont, paint);
                    
                    tabX += tabW + 2;
                }
            }
            finally
            {
                PaintPool.Instance.Return(paint);
            }
        }
    }

    /// <summary>
    /// Phase 2: Dock guides + preview + dragging panel (AFTER all content, highest z-index)
    /// </summary>
    public void RenderOverlay(SKCanvas canvas, Action<SKCanvas, DockablePanel>? renderDraggingContent = null)
    {
        if (_draggingPanel != null)
        {
            // Dim background
            var paint = PaintPool.Instance.Rent();
            try
            {
                paint.Color = new SKColor(0, 0, 0, 60);
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(0, 0, _lastScreenWidth, _lastScreenHeight, paint);
            }
            finally { PaintPool.Instance.Return(paint); }

            // Dock zone preview (translucent highlight)
            if (_currentDockZone != null)
            {
                RenderDockZonePreview(canvas, _currentDockZone);
            }

            // VS-style dock guides
            RenderDockGuides(canvas);

            // Dragging panel (z-index m�ximo)
            RenderDraggingPanel(canvas, _draggingPanel);
            renderDraggingContent?.Invoke(canvas, _draggingPanel);
        }
    }

    private void RenderDockGuides(SKCanvas canvas)
    {
        var chartArea = GetChartArea(_lastScreenWidth, _lastScreenHeight, _lastHeaderHeight);
        float cx = chartArea.MidX;
        float cy = chartArea.MidY;
        float statusBarH = StatusBarRenderer.Height;
        float availH = _lastScreenHeight - statusBarH;

        // ???????????????????????????????????????????
        // CENTER COMPASS (diamond layout inside chart)
        // ???????????????????????????????????????????
        
        // Compass background
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Draw connecting diamond shape
            paint.Color = new SKColor(30, 35, 45, 200);
            paint.Style = SKPaintStyle.Fill;
            
            using var diamond = new SKPath();
            diamond.MoveTo(cx, cy - 68);       // top
            diamond.LineTo(cx + 68, cy);       // right
            diamond.LineTo(cx, cy + 68);       // bottom
            diamond.LineTo(cx - 68, cy);       // left
            diamond.Close();
            canvas.DrawPath(diamond, paint);
            
            paint.Color = new SKColor(60, 70, 85, 180);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawPath(diamond, paint);
        }
        finally { PaintPool.Instance.Return(paint); }

        // Guide buttons on the compass
        bool hoveredTop = _currentDockZone != null && DistanceTo(0, 0, 0, 0) >= 0 && // always render
            _currentDockZone.PreviewRect.Top == chartArea.Top && _currentDockZone.PreviewRect.Bottom < chartArea.Bottom && _currentDockZone.PreviewRect.Bottom > chartArea.Top;
        
        RenderGuideButton(canvas, cx, cy - 50, PanelPosition.Top, _currentDockZone);
        RenderGuideButton(canvas, cx, cy + 50, PanelPosition.Bottom, _currentDockZone);
        RenderGuideButton(canvas, cx - 50, cy, PanelPosition.Left, _currentDockZone);
        RenderGuideButton(canvas, cx + 50, cy, PanelPosition.Right, _currentDockZone);
        RenderGuideButton(canvas, cx, cy, PanelPosition.Center, _currentDockZone);

        // ???????????????????????????????????????????
        // EDGE GUIDES (at screen borders)
        // ???????????????????????????????????????????
        float edgeCy = (_lastHeaderHeight + availH) / 2;
        float edgeCx = _lastScreenWidth / 2f;
        
        RenderEdgeGuide(canvas, EdgeGuideMargin + GuideButtonSize / 2, edgeCy, PanelPosition.Left, _currentDockZone);
        RenderEdgeGuide(canvas, _lastScreenWidth - EdgeGuideMargin - GuideButtonSize / 2, edgeCy, PanelPosition.Right, _currentDockZone);
        RenderEdgeGuide(canvas, edgeCx, availH - EdgeGuideMargin - GuideButtonSize / 2, PanelPosition.Bottom, _currentDockZone);
    }

    private void RenderGuideButton(SKCanvas canvas, float cx, float cy, PanelPosition position, DockZone? activeZone)
    {
        float half = GuideButtonSize / 2;
        var rect = new SKRect(cx - half, cy - half, cx + half, cy + half);
        
        bool isActive = activeZone != null && IsGuideForZone(cx, cy, position, activeZone);
        
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Button background
            paint.Color = isActive ? new SKColor(56, 139, 253, 240) : new SKColor(45, 52, 65, 220);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(rect, 4, 4, paint);
            
            // Border
            paint.Color = isActive ? new SKColor(100, 170, 255) : new SKColor(80, 90, 105);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1.5f;
            canvas.DrawRoundRect(rect, 4, 4, paint);
            
            // Arrow icon
            paint.Color = isActive ? SKColors.White : new SKColor(180, 190, 200);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2.5f;
            paint.StrokeCap = SKStrokeCap.Round;
            
            float s = 7; // arrow size
            switch (position)
            {
                case PanelPosition.Top:
                    canvas.DrawLine(cx, cy - s, cx, cy + s, paint);
                    canvas.DrawLine(cx - s, cy - s + 4, cx, cy - s, paint);
                    canvas.DrawLine(cx + s, cy - s + 4, cx, cy - s, paint);
                    break;
                case PanelPosition.Bottom:
                    canvas.DrawLine(cx, cy - s, cx, cy + s, paint);
                    canvas.DrawLine(cx - s, cy + s - 4, cx, cy + s, paint);
                    canvas.DrawLine(cx + s, cy + s - 4, cx, cy + s, paint);
                    break;
                case PanelPosition.Left:
                    canvas.DrawLine(cx - s, cy, cx + s, cy, paint);
                    canvas.DrawLine(cx - s + 4, cy - s, cx - s, cy, paint);
                    canvas.DrawLine(cx - s + 4, cy + s, cx - s, cy, paint);
                    break;
                case PanelPosition.Right:
                    canvas.DrawLine(cx - s, cy, cx + s, cy, paint);
                    canvas.DrawLine(cx + s - 4, cy - s, cx + s, cy, paint);
                    canvas.DrawLine(cx + s - 4, cy + s, cx + s, cy, paint);
                    break;
                case PanelPosition.Center:
                    // Grid/window icon
                    paint.StrokeWidth = 2;
                    float r = 8;
                    canvas.DrawRoundRect(new SKRect(cx - r, cy - r, cx + r, cy + r), 2, 2, paint);
                    canvas.DrawLine(cx, cy - r, cx, cy + r, paint);
                    canvas.DrawLine(cx - r, cy, cx + r, cy, paint);
                    break;
            }
        }
        finally { PaintPool.Instance.Return(paint); }
    }

    private void RenderEdgeGuide(SKCanvas canvas, float cx, float cy, PanelPosition position, DockZone? activeZone)
    {
        float half = GuideButtonSize / 2;
        var rect = new SKRect(cx - half, cy - half, cx + half, cy + half);
        
        bool isActive = activeZone != null && IsEdgeGuideActive(cx, cy, position, activeZone);
        
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Pill-shaped background
            paint.Color = isActive ? new SKColor(56, 139, 253, 220) : new SKColor(35, 40, 50, 200);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(rect, 6, 6, paint);
            
            paint.Color = isActive ? new SKColor(100, 170, 255) : new SKColor(60, 70, 85);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawRoundRect(rect, 6, 6, paint);
            
            // Arrow
            paint.Color = isActive ? SKColors.White : new SKColor(160, 170, 180);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2.5f;
            paint.StrokeCap = SKStrokeCap.Round;
            
            float s = 8;
            switch (position)
            {
                case PanelPosition.Left:
                    canvas.DrawLine(cx + s, cy, cx - s, cy, paint);
                    canvas.DrawLine(cx - s + 4, cy - 5, cx - s, cy, paint);
                    canvas.DrawLine(cx - s + 4, cy + 5, cx - s, cy, paint);
                    break;
                case PanelPosition.Right:
                    canvas.DrawLine(cx - s, cy, cx + s, cy, paint);
                    canvas.DrawLine(cx + s - 4, cy - 5, cx + s, cy, paint);
                    canvas.DrawLine(cx + s - 4, cy + 5, cx + s, cy, paint);
                    break;
                case PanelPosition.Bottom:
                    canvas.DrawLine(cx, cy - s, cx, cy + s, paint);
                    canvas.DrawLine(cx - 5, cy + s - 4, cx, cy + s, paint);
                    canvas.DrawLine(cx + 5, cy + s - 4, cx, cy + s, paint);
                    break;
            }
        }
        finally { PaintPool.Instance.Return(paint); }
    }

    private bool IsGuideForZone(float gx, float gy, PanelPosition guidePos, DockZone zone)
    {
        if (zone.Position != guidePos) return false;
        var chartArea = GetChartArea(_lastScreenWidth, _lastScreenHeight, _lastHeaderHeight);
        // Center compass zones have preview rects relative to chart area
        return zone.PreviewRect.Width < _lastScreenWidth * 0.5f || zone.PreviewRect.Height < (_lastScreenHeight * 0.5f);
    }
    
    private bool IsEdgeGuideActive(float gx, float gy, PanelPosition guidePos, DockZone zone)
    {
        if (zone.Position != guidePos) return false;
        // Edge zones span full width/height
        return zone.PreviewRect.Width >= _lastScreenWidth * 0.5f || zone.PreviewRect.Height >= (_lastScreenHeight * 0.5f);
    }

    private void RenderPanel(SKCanvas canvas, DockablePanel panel)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Panel background
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(panel.Bounds, 6, 6, paint);

            // Border � highlight on active (clicked) or handle hover
            bool isActive = _activePanel == panel;
            bool isHandleHovered = _hoveredHandle != null && _hoveredHandle.StartsWith(panel.Config.Id);
            bool highlight = isActive || isHandleHovered;
            paint.Color = highlight ? new SKColor(70, 140, 255, isActive ? (byte)120 : (byte)150) : new SKColor(35, 38, 45);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = highlight ? 1.5f : 1;
            canvas.DrawRoundRect(panel.Bounds, 6, 6, paint);

            if (panel.IsCollapsed)
            {
                RenderCollapsedPanel(canvas, panel, paint);
            }
            else
            {
                RenderExpandedPanel(canvas, panel, paint);
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private void RenderCollapsedPanel(SKCanvas canvas, DockablePanel panel, SKPaint paint)
    {
        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 11);
        paint.Color = new SKColor(140, 145, 155);
        paint.Style = SKPaintStyle.Fill;

        string displayText = panel.Config.DisplayName.ToUpper();
        float textWidth = TextMeasureCache.Instance.MeasureText(displayText, nameFont);

        // ?????????????????????????????????????????????????????????????????
        // ?  CRUCIAL: Paneles Bottom/Top ? HORIZONTAL                     ?
        // ?           Paneles Left/Right ? VERTICAL                       ?
        // ?????????????????????????????????????????????????????????????????
        
        if (panel.Position == PanelPosition.Bottom || panel.Position == PanelPosition.Top)
        {
            // ??? HORIZONTAL (como VS Code debugger) ???
            
            // Icono a la izquierda
            SvgIconRenderer.DrawIcon(canvas, panel.Config.Icon, 
                panel.Bounds.Left + 10, panel.Bounds.MidY - 8, 
                14, new SKColor(140, 145, 155));
            
            // Texto HORIZONTAL al lado del icono
            canvas.DrawText(displayText, panel.Bounds.Left + 32, panel.Bounds.MidY + 5, nameFont, paint);
        }
        else
        {
            // ??? VERTICAL (paneles laterales) ???
            
            canvas.Save();
            canvas.RotateDegrees(-90, panel.Bounds.MidX, panel.Bounds.MidY);
            
            // Icono arriba del texto (rotado)
            SvgIconRenderer.DrawIcon(canvas, panel.Config.Icon, 
                panel.Bounds.MidX - textWidth / 2 - 20, panel.Bounds.MidY - 8, 
                14, new SKColor(140, 145, 155));
            
            // Texto vertical
            canvas.DrawText(displayText, panel.Bounds.MidX - textWidth / 2 + 2, 
                panel.Bounds.MidY + 5, nameFont, paint);
            
            canvas.Restore();
        }

        // Collapse handle
        RenderHandle(canvas, panel.CollapseHandleBounds, "chevron_expand", 
            _hoveredHandle == $"{panel.Config.Id}_collapse", panel.Position);
    }

    private void RenderExpandedPanel(SKCanvas canvas, DockablePanel panel, SKPaint paint)
    {
        // Icono SVG + Nombre del panel
        float nameX = panel.Bounds.Left + 40;
        float nameY = panel.Bounds.Top + 22;
        
        SvgIconRenderer.DrawIcon(canvas, panel.Config.Icon, 
            panel.Bounds.Left + 10, panel.Bounds.Top + 8, 
            16, new SKColor(140, 145, 155));
        
        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 11);
        paint.Color = new SKColor(140, 145, 155);
        paint.Style = SKPaintStyle.Fill;
        
        // Usar DynamicTitle si existe, sino DisplayName
        string nameText = panel.DynamicTitle ?? panel.Config.DisplayName.ToUpper();
        canvas.DrawText(nameText, nameX, nameY, nameFont, paint);

        // Drag handle (dots)
        RenderHandle(canvas, panel.DragHandleBounds, "drag", 
            _hoveredHandle == $"{panel.Config.Id}_drag" || _draggingPanel == panel, panel.Position);

        // Collapse handle
        if (panel.Config.CanCollapse)
        {
            RenderHandle(canvas, panel.CollapseHandleBounds, "chevron_collapse", 
                _hoveredHandle == $"{panel.Config.Id}_collapse", panel.Position);
        }

        // Close handle (X)
        if (panel.Config.CanClose)
        {
            RenderHandle(canvas, panel.CloseHandleBounds, "close", 
                _hoveredHandle == $"{panel.Config.Id}_close", panel.Position);
        }

        // Separator line
        paint.Color = new SKColor(30, 33, 38);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        canvas.DrawLine(panel.Bounds.Left + 8, panel.Bounds.Top + 36, 
            panel.Bounds.Right - 8, panel.Bounds.Top + 36, paint);
    }

    private void RenderHandle(SKCanvas canvas, SKRect bounds, string icon, bool isHovered, PanelPosition position = PanelPosition.Left)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Background
            paint.Color = isHovered ? new SKColor(50, 55, 65) : new SKColor(28, 31, 36);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(bounds, 4, 4, paint);

            // Border en hover
            if (isHovered)
            {
                paint.Color = new SKColor(70, 140, 255, 100);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 1;
                canvas.DrawRoundRect(bounds, 4, 4, paint);
            }

            // Icon
            paint.Color = isHovered ? new SKColor(200, 205, 215) : new SKColor(120, 125, 135);
            paint.StrokeWidth = 2;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeCap = SKStrokeCap.Round;
            paint.StrokeJoin = SKStrokeJoin.Round;

            float cx = bounds.MidX;
            float cy = bounds.MidY;
            float size = 8;

            switch (icon)
            {
                case "drag":
                    // 6 dots (2x3) - grip handle
                    paint.Style = SKPaintStyle.Fill;
                    float dotSize = 2;
                    for (int row = 0; row < 3; row++)
                    {
                        for (int col = 0; col < 2; col++)
                        {
                            float x = cx - 3 + col * 6;
                            float y = cy - 6 + row * 6;
                            canvas.DrawCircle(x, y, dotSize, paint);
                        }
                    }
                    break;

                case "close":
                    // X icon
                    canvas.DrawLine(cx - size/2, cy - size/2, cx + size/2, cy + size/2, paint);
                    canvas.DrawLine(cx + size/2, cy - size/2, cx - size/2, cy + size/2, paint);
                    break;

                case "chevron_collapse":
                    // Flecha hacia afuera seg�n posici�n
                    using (var path = new SKPath())
                    {
                        if (position == PanelPosition.Left)
                        {
                            path.MoveTo(cx + 3, cy - 5);
                            path.LineTo(cx - 3, cy);
                            path.LineTo(cx + 3, cy + 5);
                        }
                        else if (position == PanelPosition.Right)
                        {
                            path.MoveTo(cx - 3, cy - 5);
                            path.LineTo(cx + 3, cy);
                            path.LineTo(cx - 3, cy + 5);
                        }
                        else // Bottom
                        {
                            path.MoveTo(cx - 5, cy - 3);
                            path.LineTo(cx, cy + 3);
                            path.LineTo(cx + 5, cy - 3);
                        }
                        canvas.DrawPath(path, paint);
                    }
                    break;

                case "chevron_expand":
                    // Flecha hacia adentro seg�n posici�n
                    using (var path = new SKPath())
                    {
                        if (position == PanelPosition.Left)
                        {
                            path.MoveTo(cx - 3, cy - 5);
                            path.LineTo(cx + 3, cy);
                            path.LineTo(cx - 3, cy + 5);
                        }
                        else if (position == PanelPosition.Right)
                        {
                            path.MoveTo(cx + 3, cy - 5);
                            path.LineTo(cx - 3, cy);
                            path.LineTo(cx + 3, cy + 5);
                        }
                        else // Bottom
                        {
                            path.MoveTo(cx - 5, cy + 3);
                            path.LineTo(cx, cy - 3);
                            path.LineTo(cx + 5, cy + 3);
                        }
                        canvas.DrawPath(path, paint);
                    }
                    break;
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private void RenderResizeEdges(SKCanvas canvas)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            foreach (var panel in _panels.Values.Where(p => !p.IsClosed && !p.IsFloating && !p.IsCollapsed && p.Position != PanelPosition.Center))
            {
                // Skip inactive bottom tabs
                if (panel.Position == PanelPosition.Bottom && panel.Config.Id != _activeBottomTabId)
                    continue;

                var b = panel.Bounds;
                bool isActive = _resizingPanel == panel;
                
                SKRect edgeRect = default;
                switch (panel.Position)
                {
                    case PanelPosition.Left:
                        edgeRect = new SKRect(b.Right - 1, b.Top, b.Right + 1, b.Bottom);
                        break;
                    case PanelPosition.Right:
                        edgeRect = new SKRect(b.Left - 1, b.Top, b.Left + 1, b.Bottom);
                        break;
                    case PanelPosition.Bottom:
                        // Resize indicator at the top of the bottom panel area
                        edgeRect = new SKRect(b.Left, b.Top - 1, b.Right, b.Top + 1);
                        break;
                }

                paint.Color = isActive ? new SKColor(70, 140, 255, 180) : new SKColor(50, 55, 65, 100);
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(edgeRect, paint);
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private void RenderDraggingPanel(SKCanvas canvas, DockablePanel panel)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Shadow
            paint.Color = new SKColor(0, 0, 0, 120);
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 16);
            paint.Style = SKPaintStyle.Fill;
            var shadowRect = new SKRect(
                panel.Bounds.Left + 6, panel.Bounds.Top + 6, 
                panel.Bounds.Right + 6, panel.Bounds.Bottom + 6
            );
            canvas.DrawRoundRect(shadowRect, 6, 6, paint);
            
            // Accent border
            paint.MaskFilter = null;
            paint.Color = new SKColor(56, 139, 253, 200);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            canvas.DrawRoundRect(panel.Bounds, 6, 6, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }

        RenderPanel(canvas, panel);
    }

    private void RenderDockZonePreview(SKCanvas canvas, DockZone zone)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Translucent fill
            paint.Color = new SKColor(56, 139, 253, 50);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(zone.PreviewRect, 4, 4, paint);

            // Solid border
            paint.Color = new SKColor(56, 139, 253, 200);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            canvas.DrawRoundRect(zone.PreviewRect, 4, 4, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public void OnMouseDown(float x, float y)
    {
        _mouseDownPosition = new SKPoint(x, y);
        
        // Set active panel on click
        _activePanel = null;
        foreach (var ap in _panels.Values.OrderByDescending(ap => ap.IsFloating))
        {
            if (ap.IsClosed || ap.Position == PanelPosition.Center) continue;
            if (ap.Position == PanelPosition.Bottom && !ap.IsFloating && ap.Config.Id != _activeBottomTabId) continue;
            if (ap.Position is PanelPosition.Left or PanelPosition.Right && !ap.IsFloating
                && ap.Config.Id != _activeTabIds.GetValueOrDefault(ap.Position, ap.Config.Id)) continue;
            if (ap.Bounds.Contains(x, y)) { _activePanel = ap; break; }
        }
        
        if (_bottomTabBarRect.Contains(x, y))
        {
            foreach (var (id, rect) in _bottomTabRects)
            {
                if (rect.Contains(x, y))
                {
                    _activeBottomTabId = id;
                    // Prepare potential tear-off drag (like VS tab dragging)
                    var tabPanel = GetPanel(id);
                    if (tabPanel != null && tabPanel.Config.CanFloat)
                    {
                        _potentialDragPanel = tabPanel;
                        _dragOffset = new SKPoint(
                            tabPanel.Width / 2,
                            TabBarHeight / 2);
                    }
                    return;
                }
            }
            return;
        }
        
        // Check side tab bar clicks (Left/Right)
        foreach (var kvp in _sideTabBarRects)
        {
            if (kvp.Value.Contains(x, y))
            {
                foreach (var (id, rect) in _sideTabRects[kvp.Key])
                {
                    if (rect.Contains(x, y))
                    {
                        _activeTabIds[kvp.Key] = id;
                        var tabPanel = GetPanel(id);
                        if (tabPanel != null && tabPanel.Config.CanFloat)
                        {
                            _potentialDragPanel = tabPanel;
                            _dragOffset = new SKPoint(tabPanel.Width / 2, TabBarHeight / 2);
                        }
                        return;
                    }
                }
                return;
            }
        }
        
        // Check resize edges
        foreach (var panel in _panels.Values.Where(p => !p.IsClosed && !p.IsFloating && !p.IsCollapsed && p.Position != PanelPosition.Center))
        {
            if (panel.Position == PanelPosition.Bottom && panel.Config.Id != _activeBottomTabId)
                continue;
            var edge = GetResizeEdge(panel, x, y);
            if (edge != ResizeEdge.None)
            {
                _resizingPanel = panel;
                _resizeEdge = edge;
                _resizeStartMousePos = (edge == ResizeEdge.Right || edge == ResizeEdge.Left) ? x : y;
                _resizeStartSize = (edge == ResizeEdge.Right || edge == ResizeEdge.Left) ? panel.Width : panel.Height;
                return;
            }
        }
        
        // Check handles
        foreach (var panel in _panels.Values.OrderByDescending(p => p.IsFloating))
        {
            if (panel.IsClosed) continue;
            // Skip inactive bottom tabs (they don't have valid bounds)
            if (panel.Position == PanelPosition.Bottom && !panel.IsFloating && panel.Config.Id != _activeBottomTabId)
                continue;
            
            // Close handle - CERRAR panel (no flotar)
            if (panel.Config.CanClose && panel.CloseHandleBounds.Contains(x, y))
            {
                panel.IsClosed = true;  // ? Ocultar completamente
                return;
            }

            // Collapse handle
            if (panel.Config.CanCollapse && panel.CollapseHandleBounds.Contains(x, y))
            {
                panel.IsCollapsed = !panel.IsCollapsed;
                return;
            }

            // Drag handle - preparar para arrastrar pero NO mover todav�a
            if (panel.Config.CanFloat && panel.DragHandleBounds.Contains(x, y))
            {
                _potentialDragPanel = panel;
                _dragOffset = new SKPoint(x - panel.Bounds.Left, y - panel.Bounds.Top);
                return;
            }
        }
    }

    public void OnMouseUp(float x, float y, int screenWidth, int screenHeight)
    {
        _resizingPanel = null;
        _resizeEdge = ResizeEdge.None;
        
        if (_draggingPanel != null)
        {
            if (_currentDockZone != null)
            {
                _draggingPanel.Position = _currentDockZone.Position;
                _draggingPanel.IsFloating = false;
                _draggingPanel.DockOrder = GetNextDockOrder(_currentDockZone.Position);
                
                // Auto-activate as the active tab when docking
                if (_currentDockZone.Position == PanelPosition.Bottom)
                    _activeBottomTabId = _draggingPanel.Config.Id;
                else if (_currentDockZone.Position is PanelPosition.Left or PanelPosition.Right)
                    _activeTabIds[_currentDockZone.Position] = _draggingPanel.Config.Id;
            }
            else
            {
                // No valid zone � restore to original position
                _draggingPanel.Position = _originalPosition;
                _draggingPanel.IsFloating = _originalIsFloating;
                _draggingPanel.Bounds = _originalBounds;
                
                // Re-activate as bottom tab if restoring to bottom
                if (_originalPosition == PanelPosition.Bottom && !_originalIsFloating)
                    _activeBottomTabId = _draggingPanel.Config.Id;
            }
        }

        _draggingPanel = null;
        _potentialDragPanel = null;
        _currentDockZone = null;
    }

    public void OnMouseMove(float x, float y, int screenWidth, int screenHeight, float headerHeight)
    {
        // Handle resize
        if (_resizingPanel != null)
        {
            float delta;
            if (_resizeEdge == ResizeEdge.Right || _resizeEdge == ResizeEdge.Left)
            {
                delta = x - _resizeStartMousePos;
                if (_resizingPanel.Position == PanelPosition.Left)
                    _resizingPanel.Width = Math.Clamp(_resizeStartSize + delta, 100, screenWidth * 0.5f);
                else if (_resizingPanel.Position == PanelPosition.Right)
                    _resizingPanel.Width = Math.Clamp(_resizeStartSize - delta, 100, screenWidth * 0.5f);
            }
            else
            {
                delta = y - _resizeStartMousePos;
                if (_resizingPanel.Position == PanelPosition.Bottom)
                    _resizingPanel.Height = Math.Clamp(_resizeStartSize - delta, 80, screenHeight * 0.5f);
                else if (_resizingPanel.Position == PanelPosition.Top)
                    _resizingPanel.Height = Math.Clamp(_resizeStartSize + delta, 80, screenHeight * 0.5f);
            }
            return;
        }
        
        // Update hovered panel and handle
        if (_draggingPanel == null)
        {
            _hoveredPanel = null;
            _hoveredHandle = null;

            foreach (var panel in _panels.Values.OrderByDescending(p => p.IsFloating))
            {
                if (panel.IsClosed) continue;
                if (panel.Position == PanelPosition.Bottom && !panel.IsFloating && panel.Config.Id != _activeBottomTabId)
                    continue;
                if (panel.Bounds.Contains(x, y))
                {
                    _hoveredPanel = panel;

                    if (panel.DragHandleBounds.Contains(x, y))
                        _hoveredHandle = $"{panel.Config.Id}_drag";
                    else if (panel.CollapseHandleBounds.Contains(x, y))
                        _hoveredHandle = $"{panel.Config.Id}_collapse";
                    else if (panel.CloseHandleBounds.Contains(x, y))
                        _hoveredHandle = $"{panel.Config.Id}_close";

                    break;
                }
            }
        }

        // Si hay un panel potencial para arrastrar, verificar threshold
        if (_potentialDragPanel != null && _draggingPanel == null)
        {
            float distance = (float)Math.Sqrt(
                Math.Pow(x - _mouseDownPosition.X, 2) + 
                Math.Pow(y - _mouseDownPosition.Y, 2)
            );

            // Solo empezar a arrastrar si se movi� m�s que el threshold
            if (distance > DragThreshold)
            {
                _draggingPanel = _potentialDragPanel;
                _potentialDragPanel = null;
                
                // ? GUARDAR estado original para restaurar si se cancela
                _originalPosition = _draggingPanel.Position;
                _originalIsFloating = _draggingPanel.IsFloating;
                _originalBounds = _draggingPanel.Bounds;
                
                // Hacer flotante si no lo era
                if (!_draggingPanel.IsFloating)
                {
                    _draggingPanel.IsFloating = true;
                    
                    // For bottom tabs: use default floating size, switch to next tab
                    if (_draggingPanel.Position == PanelPosition.Bottom)
                    {
                        _draggingPanel.FloatingWidth = Math.Min(_draggingPanel.Width, 400);
                        _draggingPanel.FloatingHeight = Math.Min(_draggingPanel.Height, 300);
                        
                        var remainingTabs = _panels.Values
                            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed && p != _draggingPanel)
                            .OrderBy(p => p.DockOrder).ToList();
                        if (remainingTabs.Count > 0)
                            _activeBottomTabId = remainingTabs[0].Config.Id;
                    }
                    else if (_draggingPanel.Position is PanelPosition.Left or PanelPosition.Right)
                    {
                        _draggingPanel.FloatingWidth = _draggingPanel.Bounds.Width;
                        _draggingPanel.FloatingHeight = Math.Min(_draggingPanel.Bounds.Height, 400);
                        
                        var remainingSide = _panels.Values
                            .Where(p => p.Position == _draggingPanel.Position && !p.IsFloating && !p.IsClosed && p != _draggingPanel)
                            .OrderBy(p => p.DockOrder).ToList();
                        if (remainingSide.Count > 0)
                            _activeTabIds[_draggingPanel.Position] = remainingSide[0].Config.Id;
                    }
                    else
                    {
                        _draggingPanel.FloatingWidth = _draggingPanel.Bounds.Width;
                        _draggingPanel.FloatingHeight = _draggingPanel.Bounds.Height;
                    }
                }
            }
        }

        // Si ya estamos arrastrando, actualizar posici�n
        if (_draggingPanel != null)
        {
            float newX = x - _dragOffset.X;
            float newY = y - _dragOffset.Y;
            float width = _draggingPanel.FloatingWidth > 0 ? _draggingPanel.FloatingWidth : _draggingPanel.Bounds.Width;
            float height = _draggingPanel.FloatingHeight > 0 ? _draggingPanel.FloatingHeight : _draggingPanel.Bounds.Height;
            
            _draggingPanel.Bounds = new SKRect(newX, newY, newX + width, newY + height);
            _currentDockZone = CalculateDockZone(x, y, screenWidth, screenHeight, headerHeight);
        }
    }

    private DockZone? CalculateDockZone(float x, float y, int screenWidth, int screenHeight, float headerHeight)
    {
        float statusBarH = StatusBarRenderer.Height;
        float availH = screenHeight - statusBarH;
        
        // Calculate center area (chart panel bounds)
        var chartArea = GetChartArea(screenWidth, screenHeight, headerHeight);
        float cx = chartArea.MidX;
        float cy = chartArea.MidY;

        // Center compass guides (inside chart area)
        if (DistanceTo(x, y, cx, cy - 50) < GuideHitRadius) // Top guide
            return new DockZone(PanelPosition.Top, 
                new SKRect(chartArea.Left, chartArea.Top, chartArea.Right, chartArea.MidY));
        
        if (DistanceTo(x, y, cx, cy + 50) < GuideHitRadius) // Bottom guide
            return new DockZone(PanelPosition.Bottom,
                new SKRect(chartArea.Left, chartArea.MidY, chartArea.Right, chartArea.Bottom));
        
        if (DistanceTo(x, y, cx - 50, cy) < GuideHitRadius) // Left guide
            return new DockZone(PanelPosition.Left,
                new SKRect(chartArea.Left, chartArea.Top, chartArea.MidX, chartArea.Bottom));
        
        if (DistanceTo(x, y, cx + 50, cy) < GuideHitRadius) // Right guide
            return new DockZone(PanelPosition.Right,
                new SKRect(chartArea.MidX, chartArea.Top, chartArea.Right, chartArea.Bottom));
        
        if (DistanceTo(x, y, cx, cy) < GuideHitRadius) // Center guide
            return new DockZone(PanelPosition.Center,
                chartArea);

        // Edge guides (at screen edges)
        float edgeCy = (headerHeight + availH) / 2;

        if (DistanceTo(x, y, EdgeGuideMargin + GuideButtonSize / 2, edgeCy) < GuideHitRadius)
            return new DockZone(PanelPosition.Left,
                new SKRect(0, headerHeight, screenWidth * 0.25f, availH));
        
        if (DistanceTo(x, y, screenWidth - EdgeGuideMargin - GuideButtonSize / 2, edgeCy) < GuideHitRadius)
            return new DockZone(PanelPosition.Right,
                new SKRect(screenWidth * 0.75f, headerHeight, screenWidth, availH));
        
        float edgeCx = screenWidth / 2f;
        if (DistanceTo(x, y, edgeCx, availH - EdgeGuideMargin - GuideButtonSize / 2) < GuideHitRadius)
            return new DockZone(PanelPosition.Bottom,
                new SKRect(0, availH * 0.7f, screenWidth, availH));

        return null;
    }

    private static float DistanceTo(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }

    private int GetNextDockOrder(PanelPosition position)
    {
        var panelsInPosition = _panels.Values.Where(p => p.Position == position && !p.IsFloating);
        return panelsInPosition.Any() ? panelsInPosition.Max(p => p.DockOrder) + 1 : 0;
    }

    public SKRect GetChartArea(int screenWidth, int screenHeight, float headerHeight)
    {
        float statusBarHeight = StatusBarRenderer.Height;
        float availableBottom = screenHeight - statusBarHeight;
        
        float leftMargin = 0;
        float rightMargin = 0;
        float bottomMargin = 0;

        foreach (var panel in _panels.Values.Where(p => !p.IsFloating && !p.IsClosed))
        {
            float width = panel.IsCollapsed ? CollapsedWidth : panel.Width;
            float height = panel.IsCollapsed ? CollapsedWidth : panel.Height;

            if (panel.Position == PanelPosition.Left) leftMargin += width;
            if (panel.Position == PanelPosition.Right) rightMargin += width;
        }
        
        // Bottom: only count active tab + tab bar height
        var bottomTabs = _panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .ToList();
        if (bottomTabs.Count > 0)
        {
            var activeTab = bottomTabs.FirstOrDefault(t => t.Config.Id == _activeBottomTabId) ?? bottomTabs[0];
            float tabContentH = activeTab.IsCollapsed ? CollapsedWidth : activeTab.Height;
            bottomMargin = tabContentH + TabBarHeight;
        }

        // Ensure chart never collapses to zero — leave at least 200x120 px
        float chartRight  = Math.Max(screenWidth - rightMargin, leftMargin + 200);
        float chartBottom = Math.Max(availableBottom - bottomMargin, headerHeight + 120);
        return new SKRect(leftMargin, headerHeight, chartRight, chartBottom);
    }

    public bool IsMouseOverPanel(float x, float y) => _panels.Values.Any(p => p.Bounds.Contains(x, y));
    public bool IsDraggingPanel => _draggingPanel != null;
    public bool IsResizing => _resizingPanel != null;
    public bool IsPanelBeingDragged(DockablePanel panel) => _draggingPanel == panel;
    public DockablePanel? GetPanel(string panelId) => _panels.GetValueOrDefault(panelId);
    
    public void TogglePanel(string panelId)
    {
        var panel = GetPanel(panelId);
        if (panel != null)
        {
            panel.IsClosed = !panel.IsClosed;
            if (!panel.IsClosed)
            {
                panel.IsCollapsed = false;
                // If it's a bottom panel, make it the active tab
                if (panel.Position == PanelPosition.Bottom)
                    _activeBottomTabId = panelId;
            }
        }
    }
    
    public bool IsBottomTabActive(DockablePanel panel)
    {
        if (panel.Position == PanelPosition.Bottom)
            return panel.Config.Id == _activeBottomTabId;
        if (panel.Position == PanelPosition.Left || panel.Position == PanelPosition.Right)
            return panel.Config.Id == _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id);
        return true;
    }
    
    private ResizeEdge GetResizeEdge(DockablePanel panel, float x, float y)
    {
        var b = panel.Bounds;
        
        switch (panel.Position)
        {
            case PanelPosition.Left:
                // Right edge of left panel
                if (x >= b.Right - ResizeEdgeWidth && x <= b.Right + ResizeEdgeWidth && y >= b.Top && y <= b.Bottom)
                    return ResizeEdge.Right;
                break;
            case PanelPosition.Right:
                // Left edge of right panel
                if (x >= b.Left - ResizeEdgeWidth && x <= b.Left + ResizeEdgeWidth && y >= b.Top && y <= b.Bottom)
                    return ResizeEdge.Left;
                break;
            case PanelPosition.Bottom:
                // Top edge of bottom panel content area
                if (y >= b.Top - ResizeEdgeWidth && y <= b.Top + ResizeEdgeWidth && x >= b.Left && x <= b.Right)
                    return ResizeEdge.Top;
                break;
            case PanelPosition.Top:
                // Bottom edge of top panel
                if (y >= b.Bottom - ResizeEdgeWidth && y <= b.Bottom + ResizeEdgeWidth && x >= b.Left && x <= b.Right)
                    return ResizeEdge.Bottom;
                break;
        }
        return ResizeEdge.None;
    }
    
    public void UpdateChartTitle(string symbol, string interval, float price)
    {
        var chart = GetPanel(PanelDefinitions.CHART);
        if (chart != null)
        {
            chart.DynamicTitle = $"{symbol} \u2022 {interval} \u2022 {price:F2}";
        }
    }
}

/// <summary>
/// Panel dockable individual
/// </summary>
public class DockablePanel
{
    public PanelConfig Config { get; }
    public PanelPosition Position { get; set; }
    public SKRect Bounds { get; set; }
    public SKRect ContentBounds { get; set; }
    public SKRect DragHandleBounds { get; set; }
    public SKRect CollapseHandleBounds { get; set; }
    public SKRect CloseHandleBounds { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float FloatingWidth { get; set; }
    public float FloatingHeight { get; set; }
    public bool IsCollapsed { get; set; }
    public bool IsFloating { get; set; }
    public bool IsClosed { get; set; }
    public int DockOrder { get; set; }
    
    /// <summary>
    /// T�tulo din�mico (ej: "BTCUSDT � 1m" para el chart)
    /// </summary>
    public string? DynamicTitle { get; set; }

    public DockablePanel(PanelConfig config)
    {
        Config = config;
        Position = config.DefaultPosition;
        Width = config.DefaultWidth;
        Height = config.DefaultHeight;
        IsCollapsed = false;
        IsFloating = false;
        IsClosed = false;
        DockOrder = 0;
    }
}

/// <summary>
/// Zona de docking visual
/// </summary>
public class DockZone
{
    public PanelPosition Position { get; set; }
    public SKRect PreviewRect { get; set; }

    public DockZone(PanelPosition position, SKRect previewRect)
    {
        Position = position;
        PreviewRect = previewRect;
    }
}
