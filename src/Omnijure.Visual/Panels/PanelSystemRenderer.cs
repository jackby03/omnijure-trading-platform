using SkiaSharp;
using System;
using System.Linq;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

public class PanelSystemRenderer
{
    private const float GuideButtonSize = 36f;
    private const float EdgeGuideMargin = 20f;
    private const float GuideHitRadius = 22f;
    private const float TabBarHeight = 28f;

    public void Render(SKCanvas canvas, PanelSystem system)
    {
        var activeCenterId = system.ActiveTabIds.GetValueOrDefault(PanelPosition.Center, PanelDefinitions.CHART);
        var centerPanel = system.Panels.FirstOrDefault(p =>
            p.Position == PanelPosition.Center && !p.IsFloating && !p.IsClosed
            && p.Config.Id == activeCenterId && p != system.DraggingPanel);
        if (centerPanel != null)
        {
            RenderPanel(canvas, system, centerPanel);
        }

        RenderBottomTabBar(canvas, system);
        RenderSideTabBars(canvas, system);
        var activeBottomPanel = system.Panels.FirstOrDefault(p => 
            p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed && 
            p.Config.Id == system.ActiveBottomTabId && p != system.DraggingPanel);
        if (activeBottomPanel != null)
        {
            RenderBottomPanelContent(canvas, activeBottomPanel);
        }
        
        foreach (var panel in system.Panels.Where(p => !p.IsFloating && p != system.DraggingPanel && !p.IsClosed 
            && p.Position != PanelPosition.Center && p.Position != PanelPosition.Bottom))
        {
            if ((panel.Position == PanelPosition.Left || panel.Position == PanelPosition.Right)
                && panel.Config.Id != system.ActiveTabIds.GetValueOrDefault(panel.Position, panel.Config.Id))
                continue;
            RenderPanel(canvas, system, panel);
        }

        RenderResizeEdges(canvas, system);

        foreach (var panel in system.Panels.Where(p => p.IsFloating && p != system.DraggingPanel && !p.IsClosed))
        {
            RenderPanel(canvas, system, panel);
        }
    }

    private void RenderBottomTabBar(SKCanvas canvas, PanelSystem system)
    {
        var bottomTabs = system.Panels
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (bottomTabs.Count == 0 || system.BottomTabBarRect.Width <= 0) return;

        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(system.BottomTabBarRect, paint);
            
            paint.Color = new SKColor(40, 45, 55);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(system.BottomTabBarRect.Left, system.BottomTabBarRect.Top, 
                system.BottomTabBarRect.Right, system.BottomTabBarRect.Top, paint);

            using var tabFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            
            foreach (var tab in bottomTabs)
            {
                bool isActive = tab.Config.Id == system.ActiveBottomTabId;
                string label = tab.Config.DisplayName;
                var (id, tabRect) = system.BottomTabRects.FirstOrDefault(t => t.id == tab.Config.Id);
                if (tabRect.IsEmpty) continue;

                if (isActive)
                {
                    paint.Style = SKPaintStyle.Fill;
                    paint.Color = new SKColor(30, 34, 42);
                    canvas.DrawRoundRect(new SKRoundRect(tabRect, 4, 4), paint);
                    
                    paint.Color = new SKColor(56, 139, 253);
                    canvas.DrawRect(tabRect.Left + 4, tabRect.Top, tabRect.Width - 8, 2, paint);
                }
                
                SvgIconRenderer.DrawIcon(canvas, tab.Config.Icon, 
                    tabRect.Left + 6, tabRect.Top + (tabRect.Height - 12) / 2, 12,
                    isActive ? new SKColor(56, 139, 253) : new SKColor(100, 108, 118));
                
                paint.Style = SKPaintStyle.Fill;
                paint.Color = isActive ? new SKColor(220, 225, 235) : new SKColor(100, 108, 118);
                paint.IsAntialias = true;
                canvas.DrawText(label, tabRect.Left + 22, tabRect.Top + tabRect.Height / 2 + 4, tabFont, paint);
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
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(panel.Bounds, paint);

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
        
        panel.ContentBounds = new SKRect(
            panel.Bounds.Left + 8,
            panel.Bounds.Top + 8,
            panel.Bounds.Right - 8,
            panel.Bounds.Bottom - 4
        );
    }

    private void RenderSideTabBars(SKCanvas canvas, PanelSystem system)
    {
        foreach (var kvp in system.SideTabBarRects)
        {
            var position = kvp.Key;
            var barRect = kvp.Value;
            
            var sideTabs = system.Panels
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
                if (position == PanelPosition.Center)
                    canvas.DrawLine(barRect.Left, barRect.Bottom, barRect.Right, barRect.Bottom, paint);
                else
                    canvas.DrawLine(barRect.Left, barRect.Top, barRect.Right, barRect.Top, paint);

                using var tabFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
                string activeId = system.ActiveTabIds.GetValueOrDefault(position, "");
                
                foreach (var tab in sideTabs)
                {
                    bool isActive = tab.Config.Id == activeId;
                    string label = tab.Config.DisplayName;
                    var tRects = system.SideTabRects.GetValueOrDefault(position);
                    var tabRect = tRects?.FirstOrDefault(t => t.id == tab.Config.Id).rect ?? SKRect.Empty;
                    if (tabRect.IsEmpty) continue;
                    
                    if (isActive)
                    {
                        paint.Style = SKPaintStyle.Fill;
                        paint.Color = new SKColor(30, 34, 42);
                        canvas.DrawRoundRect(new SKRoundRect(tabRect, 4, 4), paint);

                        paint.Color = new SKColor(56, 139, 253);
                        if (position == PanelPosition.Center)
                            canvas.DrawRect(tabRect.Left + 4, tabRect.Top + tabRect.Height - 2, tabRect.Width - 8, 2, paint);
                        else
                            canvas.DrawRect(tabRect.Left + 4, tabRect.Top, tabRect.Width - 8, 2, paint);
                    }

                    SvgIconRenderer.DrawIcon(canvas, tab.Config.Icon,
                        tabRect.Left + 6, tabRect.Top + (tabRect.Height - 12) / 2, 12,
                        isActive ? new SKColor(56, 139, 253) : new SKColor(100, 108, 118));

                    paint.Style = SKPaintStyle.Fill;
                    paint.Color = isActive ? new SKColor(220, 225, 235) : new SKColor(100, 108, 118);
                    paint.IsAntialias = true;
                    canvas.DrawText(label, tabRect.Left + 22, tabRect.Top + tabRect.Height / 2 + 4, tabFont, paint);
                }
            }
            finally
            {
                PaintPool.Instance.Return(paint);
            }
        }
    }

    public void RenderOverlay(SKCanvas canvas, PanelSystem system, Action<SKCanvas, DockablePanel>? renderDraggingContent = null)
    {
        if (system.DraggingPanel != null)
        {
            var paint = PaintPool.Instance.Rent();
            try
            {
                paint.Color = new SKColor(0, 0, 0, 60);
                paint.Style = SKPaintStyle.Fill;
                canvas.DrawRect(0, 0, system.LastScreenWidth, system.LastScreenHeight, paint);
            }
            finally { PaintPool.Instance.Return(paint); }

            if (system.CurrentDockZone != null)
            {
                RenderDockZonePreview(canvas, system.CurrentDockZone);
            }

            RenderDockGuides(canvas, system);

            RenderDraggingPanel(canvas, system, system.DraggingPanel);
            renderDraggingContent?.Invoke(canvas, system.DraggingPanel);
        }
    }

    private void RenderDockGuides(SKCanvas canvas, PanelSystem system)
    {
        var chartArea = system.GetChartArea(system.LastScreenWidth, system.LastScreenHeight, system.LastHeaderHeight);
        float cx = chartArea.MidX;
        float cy = chartArea.MidY;
        float statusBarH = StatusBarRenderer.Height;
        float availH = system.LastScreenHeight - statusBarH;

        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = new SKColor(30, 35, 45, 200);
            paint.Style = SKPaintStyle.Fill;
            
            using var diamond = new SKPath();
            diamond.MoveTo(cx, cy - 68);
            diamond.LineTo(cx + 68, cy);
            diamond.LineTo(cx, cy + 68);
            diamond.LineTo(cx - 68, cy);
            diamond.Close();
            canvas.DrawPath(diamond, paint);
            
            paint.Color = new SKColor(60, 70, 85, 180);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawPath(diamond, paint);
        }
        finally { PaintPool.Instance.Return(paint); }

        bool hoveredTop = system.CurrentDockZone != null && 
            system.CurrentDockZone.PreviewRect.Top == chartArea.Top && system.CurrentDockZone.PreviewRect.Bottom < chartArea.Bottom && system.CurrentDockZone.PreviewRect.Bottom > chartArea.Top;
        
        RenderGuideButton(canvas, system, cx, cy - 50, PanelPosition.Top, system.CurrentDockZone);
        RenderGuideButton(canvas, system, cx, cy + 50, PanelPosition.Bottom, system.CurrentDockZone);
        RenderGuideButton(canvas, system, cx - 50, cy, PanelPosition.Left, system.CurrentDockZone);
        RenderGuideButton(canvas, system, cx + 50, cy, PanelPosition.Right, system.CurrentDockZone);
        RenderGuideButton(canvas, system, cx, cy, PanelPosition.Center, system.CurrentDockZone);

        float edgeCy = (system.LastHeaderHeight + availH) / 2;
        float edgeCx = system.LastScreenWidth / 2f;
        
        RenderEdgeGuide(canvas, system, EdgeGuideMargin + GuideButtonSize / 2, edgeCy, PanelPosition.Left, system.CurrentDockZone);
        RenderEdgeGuide(canvas, system, system.LastScreenWidth - EdgeGuideMargin - GuideButtonSize / 2, edgeCy, PanelPosition.Right, system.CurrentDockZone);
        RenderEdgeGuide(canvas, system, edgeCx, availH - EdgeGuideMargin - GuideButtonSize / 2, PanelPosition.Bottom, system.CurrentDockZone);
    }

    private void RenderGuideButton(SKCanvas canvas, PanelSystem system, float cx, float cy, PanelPosition position, DockZone? activeZone)
    {
        float half = GuideButtonSize / 2;
        var rect = new SKRect(cx - half, cy - half, cx + half, cy + half);
        
        bool isActive = activeZone != null && IsGuideForZone(cx, cy, position, activeZone, system);
        
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = isActive ? new SKColor(56, 139, 253, 240) : new SKColor(45, 52, 65, 220);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(rect, 4, 4, paint);
            
            paint.Color = isActive ? new SKColor(100, 170, 255) : new SKColor(80, 90, 105);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1.5f;
            canvas.DrawRoundRect(rect, 4, 4, paint);
            
            paint.Color = isActive ? SKColors.White : new SKColor(180, 190, 200);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2.5f;
            paint.StrokeCap = SKStrokeCap.Round;
            
            float s = 7;
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

    private void RenderEdgeGuide(SKCanvas canvas, PanelSystem system, float cx, float cy, PanelPosition position, DockZone? activeZone)
    {
        float half = GuideButtonSize / 2;
        var rect = new SKRect(cx - half, cy - half, cx + half, cy + half);
        
        bool isActive = activeZone != null && IsEdgeGuideActive(cx, cy, position, activeZone, system);
        
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = isActive ? new SKColor(56, 139, 253, 220) : new SKColor(35, 40, 50, 200);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(rect, 6, 6, paint);
            
            paint.Color = isActive ? new SKColor(100, 170, 255) : new SKColor(60, 70, 85);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawRoundRect(rect, 6, 6, paint);
            
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

    private bool IsGuideForZone(float gx, float gy, PanelPosition guidePos, DockZone zone, PanelSystem system)
    {
        if (zone.Position != guidePos) return false;
        return zone.PreviewRect.Width < system.LastScreenWidth * 0.5f || zone.PreviewRect.Height < (system.LastScreenHeight * 0.5f);
    }
    
    private bool IsEdgeGuideActive(float gx, float gy, PanelPosition guidePos, DockZone zone, PanelSystem system)
    {
        if (zone.Position != guidePos) return false;
        return zone.PreviewRect.Width >= system.LastScreenWidth * 0.5f || zone.PreviewRect.Height >= (system.LastScreenHeight * 0.5f);
    }

    private void RenderPanel(SKCanvas canvas, PanelSystem system, DockablePanel panel)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(panel.Bounds, 6, 6, paint);

            bool isActive = system.ActivePanel == panel;
            bool isHandleHovered = system.HoveredHandle != null && system.HoveredHandle.StartsWith(panel.Config.Id);
            bool highlight = isActive || isHandleHovered;
            paint.Color = highlight ? new SKColor(70, 140, 255, isActive ? (byte)120 : (byte)150) : new SKColor(35, 38, 45);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = highlight ? 1.5f : 1;
            canvas.DrawRoundRect(panel.Bounds, 6, 6, paint);

            if (panel.IsCollapsed)
            {
                RenderCollapsedPanel(canvas, panel, paint, system);
            }
            else
            {
                RenderExpandedPanel(canvas, panel, paint, system);
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private void RenderCollapsedPanel(SKCanvas canvas, DockablePanel panel, SKPaint paint, PanelSystem system)
    {
        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 11);
        paint.Color = new SKColor(140, 145, 155);
        paint.Style = SKPaintStyle.Fill;

        string displayText = panel.Config.DisplayName.ToUpper();
        float textWidth = TextMeasureCache.Instance.MeasureText(displayText, nameFont);

        if (panel.Position == PanelPosition.Bottom || panel.Position == PanelPosition.Top)
        {
            SvgIconRenderer.DrawIcon(canvas, panel.Config.Icon, 
                panel.Bounds.Left + 10, panel.Bounds.MidY - 8, 
                14, new SKColor(140, 145, 155));
            canvas.DrawText(displayText, panel.Bounds.Left + 32, panel.Bounds.MidY + 5, nameFont, paint);
        }
        else
        {
            canvas.Save();
            canvas.RotateDegrees(-90, panel.Bounds.MidX, panel.Bounds.MidY);
            SvgIconRenderer.DrawIcon(canvas, panel.Config.Icon, 
                panel.Bounds.MidX - textWidth / 2 - 20, panel.Bounds.MidY - 8, 
                14, new SKColor(140, 145, 155));
            canvas.DrawText(displayText, panel.Bounds.MidX - textWidth / 2 + 2, 
                panel.Bounds.MidY + 5, nameFont, paint);
            canvas.Restore();
        }

        RenderHandle(canvas, panel.CollapseHandleBounds, "chevron_expand", 
            system.HoveredHandle == $"{panel.Config.Id}_collapse", panel.Position);
    }

    private void RenderExpandedPanel(SKCanvas canvas, DockablePanel panel, SKPaint paint, PanelSystem system)
    {
        float nameX = panel.Bounds.Left + 40;
        float nameY = panel.Bounds.Top + 22;
        
        SvgIconRenderer.DrawIcon(canvas, panel.Config.Icon, 
            panel.Bounds.Left + 10, panel.Bounds.Top + 8, 
            16, new SKColor(140, 145, 155));
        
        using var nameFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 11);
        paint.Color = new SKColor(140, 145, 155);
        paint.Style = SKPaintStyle.Fill;
        
        string nameText = panel.DynamicTitle ?? panel.Config.DisplayName.ToUpper();
        canvas.DrawText(nameText, nameX, nameY, nameFont, paint);

        RenderHandle(canvas, panel.DragHandleBounds, "drag", 
            system.HoveredHandle == $"{panel.Config.Id}_drag" || system.DraggingPanel == panel, panel.Position);

        if (panel.Config.CanCollapse)
        {
            RenderHandle(canvas, panel.CollapseHandleBounds, "chevron_collapse", 
                system.HoveredHandle == $"{panel.Config.Id}_collapse", panel.Position);
        }

        if (panel.Config.CanClose)
        {
            RenderHandle(canvas, panel.CloseHandleBounds, "close", 
                system.HoveredHandle == $"{panel.Config.Id}_close", panel.Position);
        }

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
            paint.Color = isHovered ? new SKColor(50, 55, 65) : new SKColor(28, 31, 36);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(bounds, 4, 4, paint);

            if (isHovered)
            {
                paint.Color = new SKColor(70, 140, 255, 100);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 1;
                canvas.DrawRoundRect(bounds, 4, 4, paint);
            }

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
                    canvas.DrawLine(cx - size/2, cy - size/2, cx + size/2, cy + size/2, paint);
                    canvas.DrawLine(cx + size/2, cy - size/2, cx - size/2, cy + size/2, paint);
                    break;
                case "chevron_collapse":
                    using (var path = new SKPath())
                    {
                        if (position == PanelPosition.Left) { path.MoveTo(cx + 3, cy - 5); path.LineTo(cx - 3, cy); path.LineTo(cx + 3, cy + 5); }
                        else if (position == PanelPosition.Right) { path.MoveTo(cx - 3, cy - 5); path.LineTo(cx + 3, cy); path.LineTo(cx - 3, cy + 5); }
                        else { path.MoveTo(cx - 5, cy - 3); path.LineTo(cx, cy + 3); path.LineTo(cx + 5, cy - 3); }
                        canvas.DrawPath(path, paint);
                    }
                    break;
                case "chevron_expand":
                    using (var path = new SKPath())
                    {
                        if (position == PanelPosition.Left) { path.MoveTo(cx - 3, cy - 5); path.LineTo(cx + 3, cy); path.LineTo(cx - 3, cy + 5); }
                        else if (position == PanelPosition.Right) { path.MoveTo(cx + 3, cy - 5); path.LineTo(cx - 3, cy); path.LineTo(cx + 3, cy + 5); }
                        else { path.MoveTo(cx - 5, cy + 3); path.LineTo(cx, cy - 3); path.LineTo(cx + 5, cy + 3); }
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

    private void RenderResizeEdges(SKCanvas canvas, PanelSystem system)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            foreach (var panel in system.Panels.Where(p => !p.IsClosed && !p.IsFloating && !p.IsCollapsed && p.Position != PanelPosition.Center))
            {
                if (panel.Position == PanelPosition.Bottom && panel.Config.Id != system.ActiveBottomTabId)
                    continue;

                var b = panel.Bounds;
                bool isActive = system.ResizingPanel == panel;
                
                SKRect edgeRect = default;
                switch (panel.Position)
                {
                    case PanelPosition.Left: edgeRect = new SKRect(b.Right - 1, b.Top, b.Right + 1, b.Bottom); break;
                    case PanelPosition.Right: edgeRect = new SKRect(b.Left - 1, b.Top, b.Left + 1, b.Bottom); break;
                    case PanelPosition.Bottom: edgeRect = new SKRect(b.Left, b.Top - 1, b.Right, b.Top + 1); break;
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

    private void RenderDraggingPanel(SKCanvas canvas, PanelSystem system, DockablePanel panel)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = new SKColor(0, 0, 0, 120);
            paint.MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 16);
            paint.Style = SKPaintStyle.Fill;
            var shadowRect = new SKRect(panel.Bounds.Left + 6, panel.Bounds.Top + 6, panel.Bounds.Right + 6, panel.Bounds.Bottom + 6);
            canvas.DrawRoundRect(shadowRect, 6, 6, paint);
            
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

        RenderPanel(canvas, system, panel);
    }

    private void RenderDockZonePreview(SKCanvas canvas, DockZone zone)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Color = new SKColor(56, 139, 253, 50);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(zone.PreviewRect, 4, 4, paint);

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
}
