using SkiaSharp;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Renders individual panel chrome: frame, title bar, handles, collapsed state.
/// </summary>
public class PanelChromeRenderer
{
    public void RenderPanel(SKCanvas canvas, IDockingManager system, DockablePanel panel)
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

    private void RenderCollapsedPanel(SKCanvas canvas, DockablePanel panel, SKPaint paint, IDockingManager system)
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

    private void RenderExpandedPanel(SKCanvas canvas, DockablePanel panel, SKPaint paint, IDockingManager system)
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
}
