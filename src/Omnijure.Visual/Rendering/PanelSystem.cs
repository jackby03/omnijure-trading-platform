using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Sistema de paneles moderno sin barras de título.
/// Interacción mediante handles minimalistas en las esquinas.
/// Estilo inspirado en herramientas profesionales como OBS/Figma.
/// </summary>
public class PanelSystem
{
    private readonly Dictionary<string, DockablePanel> _panels = new();
    private DockablePanel? _draggingPanel;
    private DockablePanel? _potentialDragPanel; // Panel que podría empezar a arrastrarse
    private SKPoint _mouseDownPosition; // Posición inicial del mouse
    private DockablePanel? _hoveredPanel;
    private string? _hoveredHandle;
    private SKPoint _dragOffset;
    private DockZone? _currentDockZone;
    
    private const float HandleSize = 24f;
    private const float CollapsedWidth = 32f;
    private const float HandlePadding = 6f;
    private const float DragThreshold = 5f; // Píxeles que debe moverse antes de empezar el drag

    public IReadOnlyCollection<DockablePanel> Panels => _panels.Values;

    public PanelSystem()
    {
        // Inicializar paneles por defecto
        CreatePanel(PanelDefinitions.WATCHLIST);
        CreatePanel(PanelDefinitions.ORDERBOOK);
        CreatePanel(PanelDefinitions.TRADES);
        CreatePanel(PanelDefinitions.POSITIONS);
    }

    private void CreatePanel(string panelId)
    {
        if (!PanelDefinitions.Panels.TryGetValue(panelId, out var config))
            return;

        var panel = new DockablePanel(config);
        _panels[panelId] = panel;
    }

    public void Update(int screenWidth, int screenHeight, float headerHeight)
    {
        float currentLeftX = 0;
        float currentRightX = screenWidth;
        float currentBottomY = screenHeight;

        // Update docked panels positions
        foreach (var panel in _panels.Values.Where(p => p.Position == PanelPosition.Left && !p.IsFloating).OrderBy(p => p.DockOrder))
        {
            float width = panel.IsCollapsed ? CollapsedWidth : panel.Width;
            panel.Bounds = new SKRect(currentLeftX, headerHeight, currentLeftX + width, screenHeight);
            currentLeftX += width;
        }

        foreach (var panel in _panels.Values.Where(p => p.Position == PanelPosition.Right && !p.IsFloating).OrderBy(p => p.DockOrder))
        {
            float width = panel.IsCollapsed ? CollapsedWidth : panel.Width;
            panel.Bounds = new SKRect(currentRightX - width, headerHeight, currentRightX, screenHeight);
            currentRightX -= width;
        }

        foreach (var panel in _panels.Values.Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating).OrderBy(p => p.DockOrder))
        {
            float height = panel.IsCollapsed ? CollapsedWidth : panel.Height;
            panel.Bounds = new SKRect(currentLeftX, currentBottomY - height, currentRightX, currentBottomY);
            currentBottomY -= height;
        }

        // Update handle positions for all panels
        foreach (var panel in _panels.Values)
        {
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

        // Close handle (al lado del collapse)
        if (panel.Config.CanClose && panel.IsFloating)
        {
            panel.CloseHandleBounds = new SKRect(
                panel.Bounds.Right - HandlePadding - HandleSize * 2 - 4,
                panel.Bounds.Top + HandlePadding,
                panel.Bounds.Right - HandlePadding - HandleSize - 4,
                panel.Bounds.Top + HandlePadding + HandleSize
            );
        }

        // Content bounds (área útil del panel)
        float topPadding = 40f; // Espacio para handles y nombre
        panel.ContentBounds = new SKRect(
            panel.Bounds.Left + 8,
            panel.Bounds.Top + topPadding,
            panel.Bounds.Right - 8,
            panel.Bounds.Bottom - 8
        );
    }

    public void Render(SKCanvas canvas)
    {
        // Render docked panels
        foreach (var panel in _panels.Values.Where(p => !p.IsFloating && p != _draggingPanel))
        {
            RenderPanel(canvas, panel);
        }

        // Render floating panels
        foreach (var panel in _panels.Values.Where(p => p.IsFloating && p != _draggingPanel))
        {
            RenderPanel(canvas, panel);
        }

        // Render dock zone preview
        if (_draggingPanel != null && _currentDockZone != null)
        {
            RenderDockZonePreview(canvas, _currentDockZone);
        }

        // Render dragging panel last (with shadow)
        if (_draggingPanel != null)
        {
            RenderDraggingPanel(canvas, _draggingPanel);
        }
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

            // Border (más grueso si está hover)
            bool isHovered = panel == _hoveredPanel;
            paint.Color = isHovered ? new SKColor(70, 140, 255, 150) : new SKColor(35, 38, 45);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = isHovered ? 2 : 1;
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
        // Icono SVG + Nombre del panel (esquina superior izquierda)
        float nameX = panel.Bounds.Left + 40;
        float nameY = panel.Bounds.Top + 22;
        
        // Dibujar icono SVG
        SvgIconRenderer.DrawIcon(canvas, panel.Config.Icon, 
            panel.Bounds.Left + 10, panel.Bounds.Top + 8, 
            16, new SKColor(140, 145, 155));
        
        // Panel name
        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 11);
        paint.Color = new SKColor(140, 145, 155);
        paint.Style = SKPaintStyle.Fill;
        
        string nameText = panel.Config.DisplayName.ToUpper();
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
        if (panel.Config.CanClose && panel.IsFloating)
        {
            RenderHandle(canvas, panel.CloseHandleBounds, "close", 
                _hoveredHandle == $"{panel.Config.Id}_close", panel.Position);
        }

        // Subtle separator line
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
                    // Flecha hacia afuera según posición
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
                    // Flecha hacia adentro según posición
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

    private void RenderDraggingPanel(SKCanvas canvas, DockablePanel panel)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            // Drop shadow profunda
            paint.Color = new SKColor(0, 0, 0, 120);
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 20);
            paint.Style = SKPaintStyle.Fill;
            var shadowRect = new SKRect(panel.Bounds.Left + 8, panel.Bounds.Top + 8, 
                panel.Bounds.Right + 8, panel.Bounds.Bottom + 8);
            canvas.DrawRoundRect(shadowRect, 6, 6, paint);
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
            // Animated glow (breathing effect)
            float pulse = (float)(Math.Sin(DateTime.Now.Millisecond / 150.0) * 30 + 70);
            
            paint.Color = new SKColor(70, 140, 255, (byte)pulse);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(zone.PreviewRect, paint);

            // Dashed border animated
            paint.Color = new SKColor(70, 140, 255, 220);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 3;
            paint.PathEffect = SKPathEffect.CreateDash(new float[] { 12, 6 }, 0);
            canvas.DrawRect(zone.PreviewRect, paint);

            // Icon indicator grande
            using var iconFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 48);
            paint.PathEffect = null;
            paint.Color = new SKColor(70, 140, 255, 200);
            paint.Style = SKPaintStyle.Fill;
            
            string icon = zone.Position switch
            {
                PanelPosition.Left => "?",
                PanelPosition.Right => "?",
                PanelPosition.Bottom => "?",
                _ => "?"
            };
            
            float iconWidth = TextMeasureCache.Instance.MeasureText(icon, iconFont);
            canvas.DrawText(icon, zone.PreviewRect.MidX - iconWidth / 2, 
                zone.PreviewRect.MidY + 18, iconFont, paint);

            // Texto descriptivo
            using var textFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 20);
            string text = zone.Position switch
            {
                PanelPosition.Left => "DOCK LEFT",
                PanelPosition.Right => "DOCK RIGHT",
                PanelPosition.Bottom => "DOCK BOTTOM",
                _ => "DOCK HERE"
            };
            
            float textWidth = TextMeasureCache.Instance.MeasureText(text, textFont);
            canvas.DrawText(text, zone.PreviewRect.MidX - textWidth / 2, 
                zone.PreviewRect.MidY + 60, textFont, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public void OnMouseDown(float x, float y)
    {
        // Guardar posición del mouse para threshold de drag
        _mouseDownPosition = new SKPoint(x, y);
        
        // Check handles first (priority order)
        foreach (var panel in _panels.Values.OrderByDescending(p => p.IsFloating))
        {
            // Close handle
            if (panel.Config.CanClose && panel.IsFloating && panel.CloseHandleBounds.Contains(x, y))
            {
                panel.IsFloating = false;
                panel.Position = panel.Config.DefaultPosition;
                return;
            }

            // Collapse handle
            if (panel.Config.CanCollapse && panel.CollapseHandleBounds.Contains(x, y))
            {
                panel.IsCollapsed = !panel.IsCollapsed;
                return;
            }

            // Drag handle - preparar para arrastrar pero NO mover todavía
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
        if (_draggingPanel != null)
        {
            if (_currentDockZone != null)
            {
                _draggingPanel.Position = _currentDockZone.Position;
                _draggingPanel.IsFloating = false;
                _draggingPanel.DockOrder = GetNextDockOrder(_currentDockZone.Position);
            }
        }

        _draggingPanel = null;
        _potentialDragPanel = null;
        _currentDockZone = null;
    }

    public void OnMouseMove(float x, float y, int screenWidth, int screenHeight, float headerHeight)
    {
        // Update hovered panel and handle (solo si no estamos arrastrando)
        if (_draggingPanel == null)
        {
            _hoveredPanel = null;
            _hoveredHandle = null;

            foreach (var panel in _panels.Values.OrderByDescending(p => p.IsFloating))
            {
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

            // Solo empezar a arrastrar si se movió más que el threshold
            if (distance > DragThreshold)
            {
                _draggingPanel = _potentialDragPanel;
                _potentialDragPanel = null;
                
                // Hacer flotante si no lo era
                if (!_draggingPanel.IsFloating)
                {
                    _draggingPanel.IsFloating = true;
                    _draggingPanel.FloatingWidth = _draggingPanel.Bounds.Width;
                    _draggingPanel.FloatingHeight = _draggingPanel.Bounds.Height;
                }
            }
        }

        // Si ya estamos arrastrando, actualizar posición
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
        const float EdgeThreshold = 100;
        const float Margin = 40;

        if (x < EdgeThreshold)
            return new DockZone(PanelPosition.Left, 
                new SKRect(Margin, headerHeight + Margin, screenWidth * 0.25f, screenHeight - Margin));
        
        if (x > screenWidth - EdgeThreshold)
            return new DockZone(PanelPosition.Right, 
                new SKRect(screenWidth * 0.75f, headerHeight + Margin, screenWidth - Margin, screenHeight - Margin));
        
        if (y > screenHeight - EdgeThreshold)
            return new DockZone(PanelPosition.Bottom, 
                new SKRect(Margin, screenHeight * 0.75f, screenWidth - Margin, screenHeight - Margin));

        return null;
    }

    private int GetNextDockOrder(PanelPosition position)
    {
        var panelsInPosition = _panels.Values.Where(p => p.Position == position && !p.IsFloating);
        return panelsInPosition.Any() ? panelsInPosition.Max(p => p.DockOrder) + 1 : 0;
    }

    public SKRect GetChartArea(int screenWidth, int screenHeight, float headerHeight)
    {
        float leftMargin = 0;
        float rightMargin = 0;
        float bottomMargin = 0;

        foreach (var panel in _panels.Values.Where(p => !p.IsFloating))
        {
            float width = panel.IsCollapsed ? CollapsedWidth : panel.Width;
            float height = panel.IsCollapsed ? CollapsedWidth : panel.Height;

            if (panel.Position == PanelPosition.Left) leftMargin += width;
            if (panel.Position == PanelPosition.Right) rightMargin += width;
            if (panel.Position == PanelPosition.Bottom) bottomMargin += height;
        }

        return new SKRect(leftMargin, headerHeight, screenWidth - rightMargin, screenHeight - bottomMargin);
    }

    public bool IsMouseOverPanel(float x, float y) => _panels.Values.Any(p => p.Bounds.Contains(x, y));
    public bool IsDraggingPanel => _draggingPanel != null;
    public DockablePanel? GetPanel(string panelId) => _panels.GetValueOrDefault(panelId);
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
    public int DockOrder { get; set; }

    public DockablePanel(PanelConfig config)
    {
        Config = config;
        Position = config.DefaultPosition;
        Width = config.DefaultWidth;
        Height = config.DefaultHeight;
        IsCollapsed = false;
        IsFloating = false;
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
