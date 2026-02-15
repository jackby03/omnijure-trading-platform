using System;
using System.Collections.Generic;
using Omnijure.Core.DataStructures;
using Omnijure.Core.Scripting;
using Omnijure.Visual.Rendering;
using SkiaSharp;

namespace Omnijure.Visual.Chart;

/// <summary>
/// Renders SharpScript outputs (plots, hlines, shapes, bgcolors, strategy signals)
/// onto the chart canvas. Uses same coordinate system as ChartRenderer.
/// </summary>
public class ScriptOverlayRenderer
{
    /// <summary>
    /// Renders all overlay outputs onto the main price chart area.
    /// </summary>
    public void RenderOverlays(
        SKCanvas canvas,
        List<ScriptOutput> outputs,
        int visibleCandles,
        int scrollOffset,
        float candleWidth,
        int chartHeight,
        float minPrice,
        float maxPrice)
    {
        if (outputs == null || outputs.Count == 0) return;

        foreach (var output in outputs)
        {
            if (output.Error != null) continue;
            if (!output.IsOverlay) continue;

            DrawBgColors(canvas, output.Backgrounds, visibleCandles, scrollOffset, candleWidth, chartHeight, minPrice, maxPrice);
            DrawPlotSeries(canvas, output.Plots, visibleCandles, scrollOffset, candleWidth, chartHeight, minPrice, maxPrice);
            DrawHLines(canvas, output.HLines, visibleCandles, candleWidth, chartHeight, minPrice, maxPrice);
            DrawShapes(canvas, output.Shapes, visibleCandles, scrollOffset, candleWidth, chartHeight, minPrice, maxPrice);
            DrawStrategySignals(canvas, output.Signals, visibleCandles, scrollOffset, candleWidth, chartHeight, minPrice, maxPrice);
        }
    }

    private void DrawPlotSeries(
        SKCanvas canvas,
        List<PlotSeries> plots,
        int visible,
        int offset,
        float candleWidth,
        int height,
        float min,
        float max)
    {
        foreach (var plot in plots)
        {
            if (plot.Values == null || plot.Values.Length == 0) continue;

            byte a = (byte)((plot.Color >> 24) & 0xFF);
            byte r = (byte)((plot.Color >> 16) & 0xFF);
            byte g = (byte)((plot.Color >> 8) & 0xFF);
            byte b = (byte)(plot.Color & 0xFF);

            var paint = PaintPool.Instance.Rent();
            try
            {
                paint.Color = new SKColor(r, g, b, a);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = plot.LineWidth;
                paint.IsAntialias = true;

                using var path = new SKPath();
                bool started = false;

                for (int i = 0; i < visible; i++)
                {
                    int idx = i + offset;
                    if (idx < 0) continue;
                    if (idx >= plot.Values.Length) break;

                    float val = plot.Values[idx];
                    if (float.IsNaN(val)) { started = false; continue; }

                    float x = (visible - 1 - i) * candleWidth + (candleWidth / 2);
                    float y = MapPriceToY(val, min, max, height);

                    if (!started) { path.MoveTo(x, y); started = true; }
                    else path.LineTo(x, y);
                }

                canvas.DrawPath(path, paint);
            }
            finally { PaintPool.Instance.Return(paint); }
        }
    }

    private void DrawHLines(
        SKCanvas canvas,
        List<HLineDef> hlines,
        int visible,
        float candleWidth,
        int height,
        float min,
        float max)
    {
        foreach (var hl in hlines)
        {
            if (hl.Price < min || hl.Price > max) continue;

            float y = MapPriceToY(hl.Price, min, max, height);
            float chartW = visible * candleWidth;

            var paint = PaintPool.Instance.Rent();
            try
            {
                byte a = (byte)((hl.Color >> 24) & 0xFF);
                byte r = (byte)((hl.Color >> 16) & 0xFF);
                byte g = (byte)((hl.Color >> 8) & 0xFF);
                byte b = (byte)(hl.Color & 0xFF);

                paint.Color = new SKColor(r, g, b, a);
                paint.Style = SKPaintStyle.Stroke;
                paint.StrokeWidth = 1;
                paint.IsAntialias = true;

                switch (hl.Style)
                {
                    case HLineStyle.Dashed:
                        paint.PathEffect = SKPathEffect.CreateDash(new float[] { 6, 4 }, 0);
                        break;
                    case HLineStyle.Dotted:
                        paint.PathEffect = SKPathEffect.CreateDash(new float[] { 2, 3 }, 0);
                        break;
                }

                canvas.DrawLine(0, y, chartW, y, paint);

                // Label
                if (!string.IsNullOrEmpty(hl.Title))
                {
                    paint.Style = SKPaintStyle.Fill;
                    paint.PathEffect = null;
                    using var font = new SKFont(SKTypeface.Default, 10);
                    canvas.DrawText(hl.Title, 4, y - 4, font, paint);
                }
            }
            finally { PaintPool.Instance.Return(paint); }
        }
    }

    private void DrawBgColors(
        SKCanvas canvas,
        List<BgColorEntry> backgrounds,
        int visible,
        int offset,
        float candleWidth,
        int height,
        float min,
        float max)
    {
        if (backgrounds.Count == 0) return;

        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Style = SKPaintStyle.Fill;

            foreach (var bg in backgrounds)
            {
                int screenIdx = bg.BarIndex - offset;
                if (screenIdx < 0 || screenIdx >= visible) continue;

                byte a = (byte)((bg.Color >> 24) & 0xFF);
                byte r = (byte)((bg.Color >> 16) & 0xFF);
                byte g = (byte)((bg.Color >> 8) & 0xFF);
                byte b = (byte)(bg.Color & 0xFF);

                paint.Color = new SKColor(r, g, b, a);

                float x = (visible - 1 - screenIdx) * candleWidth;
                canvas.DrawRect(x, 0, candleWidth, height, paint);
            }
        }
        finally { PaintPool.Instance.Return(paint); }
    }

    private void DrawShapes(
        SKCanvas canvas,
        List<ShapeMark> shapes,
        int visible,
        int offset,
        float candleWidth,
        int height,
        float min,
        float max)
    {
        if (shapes.Count == 0) return;

        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Style = SKPaintStyle.Fill;
            paint.IsAntialias = true;

            foreach (var shape in shapes)
            {
                int screenIdx = shape.BarIndex - offset;
                if (screenIdx < 0 || screenIdx >= visible) continue;

                byte a = (byte)((shape.Color >> 24) & 0xFF);
                byte r = (byte)((shape.Color >> 16) & 0xFF);
                byte g = (byte)((shape.Color >> 8) & 0xFF);
                byte b = (byte)(shape.Color & 0xFF);

                paint.Color = new SKColor(r, g, b, a);

                float x = (visible - 1 - screenIdx) * candleWidth + (candleWidth / 2);
                float y = MapPriceToY(shape.Price, min, max, height);
                float size = 6f;

                switch (shape.Style)
                {
                    case ShapeStyle.TriangleUp:
                        DrawTriangle(canvas, x, y, size, true, paint);
                        break;
                    case ShapeStyle.TriangleDown:
                        DrawTriangle(canvas, x, y, size, false, paint);
                        break;
                    case ShapeStyle.ArrowUp:
                        DrawArrow(canvas, x, y, size, true, paint);
                        break;
                    case ShapeStyle.ArrowDown:
                        DrawArrow(canvas, x, y, size, false, paint);
                        break;
                    case ShapeStyle.Circle:
                        canvas.DrawCircle(x, y, size / 2, paint);
                        break;
                    case ShapeStyle.Cross:
                        paint.Style = SKPaintStyle.Stroke;
                        paint.StrokeWidth = 2;
                        canvas.DrawLine(x - size / 2, y - size / 2, x + size / 2, y + size / 2, paint);
                        canvas.DrawLine(x + size / 2, y - size / 2, x - size / 2, y + size / 2, paint);
                        paint.Style = SKPaintStyle.Fill;
                        break;
                    case ShapeStyle.Diamond:
                        using (var path = new SKPath())
                        {
                            path.MoveTo(x, y - size);
                            path.LineTo(x + size * 0.6f, y);
                            path.LineTo(x, y + size);
                            path.LineTo(x - size * 0.6f, y);
                            path.Close();
                            canvas.DrawPath(path, paint);
                        }
                        break;
                }
            }
        }
        finally { PaintPool.Instance.Return(paint); }
    }

    private void DrawStrategySignals(
        SKCanvas canvas,
        List<StrategySignal> signals,
        int visible,
        int offset,
        float candleWidth,
        int height,
        float min,
        float max)
    {
        if (signals.Count == 0) return;

        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.Style = SKPaintStyle.Fill;
            paint.IsAntialias = true;

            foreach (var signal in signals)
            {
                int screenIdx = signal.BarIndex - offset;
                if (screenIdx < 0 || screenIdx >= visible) continue;

                float x = (visible - 1 - screenIdx) * candleWidth + (candleWidth / 2);
                float size = 8f;

                switch (signal.Direction)
                {
                    case SignalDirection.Long:
                        paint.Color = new SKColor(46, 204, 113); // green
                        // Draw below candle low
                        float yLong = height - 10; // near bottom
                        DrawArrow(canvas, x, yLong, size, true, paint);
                        break;

                    case SignalDirection.Short:
                        paint.Color = new SKColor(231, 76, 60); // red
                        float yShort = 10; // near top
                        DrawArrow(canvas, x, yShort, size, false, paint);
                        break;

                    case SignalDirection.Close:
                        paint.Color = new SKColor(149, 165, 166); // gray
                        float yClose = height / 2f;
                        canvas.DrawCircle(x, yClose, size / 2, paint);
                        break;
                }
            }
        }
        finally { PaintPool.Instance.Return(paint); }
    }

    // ═══════════════════════════════════════════════════════════
    // Helper Methods
    // ═══════════════════════════════════════════════════════════

    private static float MapPriceToY(float price, float min, float max, float height)
    {
        float range = max - min;
        if (range == 0) return height / 2;
        float normalized = (price - min) / range;
        return height - (normalized * height);
    }

    private static void DrawTriangle(SKCanvas canvas, float cx, float cy, float size, bool up, SKPaint paint)
    {
        using var path = new SKPath();
        if (up)
        {
            path.MoveTo(cx, cy - size);
            path.LineTo(cx - size * 0.7f, cy + size * 0.5f);
            path.LineTo(cx + size * 0.7f, cy + size * 0.5f);
        }
        else
        {
            path.MoveTo(cx, cy + size);
            path.LineTo(cx - size * 0.7f, cy - size * 0.5f);
            path.LineTo(cx + size * 0.7f, cy - size * 0.5f);
        }
        path.Close();
        canvas.DrawPath(path, paint);
    }

    private static void DrawArrow(SKCanvas canvas, float cx, float cy, float size, bool up, SKPaint paint)
    {
        using var path = new SKPath();
        if (up)
        {
            // Arrow pointing up
            path.MoveTo(cx, cy - size);
            path.LineTo(cx - size * 0.5f, cy);
            path.LineTo(cx - size * 0.15f, cy);
            path.LineTo(cx - size * 0.15f, cy + size);
            path.LineTo(cx + size * 0.15f, cy + size);
            path.LineTo(cx + size * 0.15f, cy);
            path.LineTo(cx + size * 0.5f, cy);
        }
        else
        {
            // Arrow pointing down
            path.MoveTo(cx, cy + size);
            path.LineTo(cx - size * 0.5f, cy);
            path.LineTo(cx - size * 0.15f, cy);
            path.LineTo(cx - size * 0.15f, cy - size);
            path.LineTo(cx + size * 0.15f, cy - size);
            path.LineTo(cx + size * 0.15f, cy);
            path.LineTo(cx + size * 0.5f, cy);
        }
        path.Close();
        canvas.DrawPath(path, paint);
    }
}
