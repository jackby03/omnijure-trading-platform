using SkiaSharp;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Vector-based icon rendering system using SKPath for professional, crisp icons at any size
/// </summary>
public static class SvgIconRenderer
{
    /// <summary>
    /// Icon types available in the system
    /// </summary>
    public enum Icon
    {
        // Drawing Tools
        Cursor,
        TrendLine,
        HorizontalLine,
        VerticalLine,
        Rectangle,
        Circle,
        Fibonacci,
        Text,

        // Chart Types
        Candles,
        LineChart,
        AreaChart,
        Bars,
        Chart,

        // UI Controls
        Settings,
        Search,
        Indicators,
        ZoomIn,
        ZoomOut,
        Screenshot,
        Fullscreen,
        Timeframe,
        
        // Panel Icons
        OrderBook,
        Exchange,
        Star,
        Wallet,
        Bell
    }

    /// <summary>
    /// Draws a vector icon at the specified position
    /// </summary>
    public static void DrawIcon(SKCanvas canvas, Icon icon, float x, float y, float size, SKColor color)
    {
        canvas.Save();
        canvas.Translate(x, y);

        // Scale from 24x24 design space to desired size
        float scale = size / 24f;
        canvas.Scale(scale, scale);

        using var paint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 2f,
            StrokeCap = SKStrokeCap.Round,
            StrokeJoin = SKStrokeJoin.Round
        };

        using var fillPaint = new SKPaint
        {
            Color = color,
            IsAntialias = true,
            Style = SKPaintStyle.Fill
        };

        switch (icon)
        {
            case Icon.Cursor:
                DrawCursor(canvas, fillPaint);
                break;
            case Icon.TrendLine:
                DrawTrendLine(canvas, paint, fillPaint);
                break;
            case Icon.HorizontalLine:
                DrawHorizontalLine(canvas, paint, fillPaint);
                break;
            case Icon.VerticalLine:
                DrawVerticalLine(canvas, paint, fillPaint);
                break;
            case Icon.Rectangle:
                DrawRectangleIcon(canvas, paint);
                break;
            case Icon.Circle:
                DrawCircleIcon(canvas, paint);
                break;
            case Icon.Fibonacci:
                DrawFibonacci(canvas, paint);
                break;
            case Icon.Text:
                DrawTextIcon(canvas, paint);
                break;
            case Icon.Candles:
                DrawCandles(canvas, paint);
                break;
            case Icon.LineChart:
                DrawLineChart(canvas, paint);
                break;
            case Icon.AreaChart:
                DrawAreaChart(canvas, paint, fillPaint);
                break;
            case Icon.Bars:
                DrawBars(canvas, fillPaint);
                break;
            case Icon.Chart:
                DrawCandles(canvas, paint); // Alias para Candles
                break;
            case Icon.Settings:
                DrawSettings(canvas, paint);
                break;
            case Icon.Search:
                DrawSearch(canvas, paint);
                break;
            case Icon.Indicators:
                DrawIndicators(canvas, paint);
                break;
            case Icon.ZoomIn:
                DrawZoomIn(canvas, paint);
                break;
            case Icon.ZoomOut:
                DrawZoomOut(canvas, paint);
                break;
            case Icon.Screenshot:
                DrawScreenshot(canvas, paint, fillPaint);
                break;
            case Icon.Fullscreen:
                DrawFullscreen(canvas, paint);
                break;
            case Icon.Timeframe:
                DrawTimeframe(canvas, paint);
                break;
            case Icon.OrderBook:
                DrawOrderBook(canvas, paint);
                break;
            case Icon.Exchange:
                DrawExchange(canvas, paint);
                break;
            case Icon.Star:
                DrawStar(canvas, fillPaint);
                break;
            case Icon.Wallet:
                DrawWallet(canvas, paint, fillPaint);
                break;
            case Icon.Bell:
                DrawBell(canvas, paint);
                break;
        }

        canvas.Restore();
    }

    /// <summary>
    /// Draws a vector icon centered in a rectangle
    /// </summary>
    public static void DrawIconCentered(SKCanvas canvas, Icon icon, SKRect rect, float size, SKColor color)
    {
        float x = rect.Left + (rect.Width - size) / 2;
        float y = rect.Top + (rect.Height - size) / 2;
        DrawIcon(canvas, icon, x, y, size, color);
    }

    // Icon drawing implementations (all in 24x24 coordinate space)

    private static void DrawCursor(SKCanvas canvas, SKPaint fillPaint)
    {
        using var path = new SKPath();
        path.MoveTo(3, 3);
        path.LineTo(10.07f, 19.97f);
        path.LineTo(12.58f, 12.58f);
        path.LineTo(19.97f, 10.07f);
        path.Close();
        canvas.DrawPath(path, fillPaint);
    }

    private static void DrawTrendLine(SKCanvas canvas, SKPaint paint, SKPaint fillPaint)
    {
        using var path = new SKPath();
        path.MoveTo(3, 17);
        path.LineTo(9, 11);
        path.LineTo(13, 15);
        path.LineTo(22, 6);
        canvas.DrawPath(path, paint);

        canvas.DrawCircle(3, 17, 2, fillPaint);
        canvas.DrawCircle(22, 6, 2, fillPaint);
    }

    private static void DrawHorizontalLine(SKCanvas canvas, SKPaint paint, SKPaint fillPaint)
    {
        canvas.DrawLine(4, 12, 20, 12, paint);
        canvas.DrawCircle(4, 12, 2, fillPaint);
        canvas.DrawCircle(20, 12, 2, fillPaint);
    }

    private static void DrawVerticalLine(SKCanvas canvas, SKPaint paint, SKPaint fillPaint)
    {
        canvas.DrawLine(12, 4, 12, 20, paint);
        canvas.DrawCircle(12, 4, 2, fillPaint);
        canvas.DrawCircle(12, 20, 2, fillPaint);
    }

    private static void DrawRectangleIcon(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawRoundRect(new SKRect(4, 6, 20, 18), 2, 2, paint);
    }

    private static void DrawCircleIcon(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(12, 12, 8, paint);
    }

    private static void DrawFibonacci(SKCanvas canvas, SKPaint paint)
    {
        paint.StrokeWidth = 1.5f;
        var oldAlpha = paint.Color.Alpha;
        paint.Color = paint.Color.WithAlpha((byte)(oldAlpha * 0.8f));

        canvas.DrawLine(3, 20, 21, 4, paint);
        canvas.DrawLine(3, 16, 21, 8, paint);
        canvas.DrawLine(3, 12, 21, 12, paint);
        canvas.DrawLine(3, 8, 21, 16, paint);
        canvas.DrawLine(3, 4, 21, 20, paint);

        paint.Color = paint.Color.WithAlpha(oldAlpha);
        paint.StrokeWidth = 2f;
    }

    private static void DrawTextIcon(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawLine(4, 5, 20, 5, paint);
        canvas.DrawLine(12, 5, 12, 19, paint);
        canvas.DrawLine(8, 19, 16, 19, paint);
    }

    private static void DrawCandles(SKCanvas canvas, SKPaint paint)
    {
        paint.StrokeWidth = 1.5f;

        // Candle 1
        canvas.DrawLine(6, 4, 6, 6, paint);
        canvas.DrawRect(new SKRect(5, 6, 7, 18), paint);
        canvas.DrawLine(6, 18, 6, 20, paint);

        // Candle 2
        canvas.DrawLine(12, 2, 12, 5, paint);
        canvas.DrawRect(new SKRect(11, 5, 13, 16), paint);
        canvas.DrawLine(12, 16, 12, 22, paint);

        // Candle 3
        canvas.DrawLine(18, 7, 18, 9, paint);
        canvas.DrawRect(new SKRect(17, 9, 19, 17), paint);
        canvas.DrawLine(18, 17, 18, 19, paint);

        paint.StrokeWidth = 2f;
    }

    private static void DrawLineChart(SKCanvas canvas, SKPaint paint)
    {
        paint.StrokeWidth = 2.5f;
        using var path = new SKPath();
        path.MoveTo(3, 17);
        path.LineTo(9, 11);
        path.LineTo(13, 15);
        path.LineTo(21, 7);
        canvas.DrawPath(path, paint);
        paint.StrokeWidth = 2f;
    }

    private static void DrawAreaChart(SKCanvas canvas, SKPaint paint, SKPaint fillPaint)
    {
        // Fill area
        using var fillPath = new SKPath();
        fillPath.MoveTo(3, 17);
        fillPath.LineTo(9, 11);
        fillPath.LineTo(13, 15);
        fillPath.LineTo(21, 7);
        fillPath.LineTo(21, 20);
        fillPath.LineTo(3, 20);
        fillPath.Close();

        var oldAlpha = fillPaint.Color.Alpha;
        fillPaint.Color = fillPaint.Color.WithAlpha((byte)(oldAlpha * 0.3f));
        canvas.DrawPath(fillPath, fillPaint);
        fillPaint.Color = fillPaint.Color.WithAlpha(oldAlpha);

        // Stroke line
        using var linePath = new SKPath();
        linePath.MoveTo(3, 17);
        linePath.LineTo(9, 11);
        linePath.LineTo(13, 15);
        linePath.LineTo(21, 7);
        canvas.DrawPath(linePath, paint);
    }

    private static void DrawBars(SKCanvas canvas, SKPaint fillPaint)
    {
        canvas.DrawRect(new SKRect(4, 8, 7, 20), fillPaint);
        canvas.DrawRect(new SKRect(10, 4, 13, 20), fillPaint);
        canvas.DrawRect(new SKRect(16, 11, 19, 20), fillPaint);
    }

    private static void DrawSettings(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(12, 12, 3, paint);

        // Outer ring with notches (simplified gear)
        for (int i = 0; i < 8; i++)
        {
            float angle = i * 45f * (float)System.Math.PI / 180f;
            float x1 = 12 + 7 * (float)System.Math.Cos(angle);
            float y1 = 12 + 7 * (float)System.Math.Sin(angle);
            float x2 = 12 + 9 * (float)System.Math.Cos(angle);
            float y2 = 12 + 9 * (float)System.Math.Sin(angle);
            canvas.DrawLine(x1, y1, x2, y2, paint);
        }
    }

    private static void DrawSearch(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(11, 11, 8, paint);
        canvas.DrawLine(16.5f, 16.5f, 21, 21, paint);
    }

    private static void DrawIndicators(SKCanvas canvas, SKPaint paint)
    {
        // Axes
        canvas.DrawLine(3, 3, 3, 21, paint);
        canvas.DrawLine(3, 21, 21, 21, paint);

        // Line chart
        using var path = new SKPath();
        path.MoveTo(7, 14);
        path.LineTo(11, 10);
        path.LineTo(14, 13);
        path.LineTo(19, 7);
        canvas.DrawPath(path, paint);
    }

    private static void DrawZoomIn(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(11, 11, 8, paint);
        canvas.DrawLine(16.5f, 16.5f, 21, 21, paint);
        canvas.DrawLine(11, 8, 11, 14, paint);
        canvas.DrawLine(8, 11, 14, 11, paint);
    }

    private static void DrawZoomOut(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(11, 11, 8, paint);
        canvas.DrawLine(16.5f, 16.5f, 21, 21, paint);
        canvas.DrawLine(8, 11, 14, 11, paint);
    }

    private static void DrawScreenshot(SKCanvas canvas, SKPaint paint, SKPaint fillPaint)
    {
        canvas.DrawRoundRect(new SKRect(3, 3, 21, 21), 2, 2, paint);
        canvas.DrawCircle(12, 12, 3, fillPaint);
    }

    private static void DrawFullscreen(SKCanvas canvas, SKPaint paint)
    {
        // Top-left corner
        canvas.DrawLine(3, 8, 3, 5, paint);
        canvas.DrawLine(3, 5, 8, 5, paint);

        // Top-right corner
        canvas.DrawLine(16, 5, 21, 5, paint);
        canvas.DrawLine(21, 5, 21, 8, paint);

        // Bottom-right corner
        canvas.DrawLine(21, 16, 21, 21, paint);
        canvas.DrawLine(21, 21, 16, 21, paint);

        // Bottom-left corner
        canvas.DrawLine(8, 21, 3, 21, paint);
        canvas.DrawLine(3, 21, 3, 16, paint);
    }

    private static void DrawTimeframe(SKCanvas canvas, SKPaint paint)
    {
        canvas.DrawCircle(12, 12, 10, paint);
        canvas.DrawLine(12, 6, 12, 12, paint);
        canvas.DrawLine(12, 12, 16, 14, paint);
    }

    private static void DrawOrderBook(SKCanvas canvas, SKPaint paint)
    {
        // Book icon
        canvas.DrawRoundRect(new SKRect(5, 3, 19, 21), 2, 2, paint);
        canvas.DrawLine(12, 3, 12, 21, paint);
        canvas.DrawLine(7, 8, 10, 8, paint);
        canvas.DrawLine(14, 8, 17, 8, paint);
        canvas.DrawLine(7, 12, 10, 12, paint);
        canvas.DrawLine(14, 12, 17, 12, paint);
        canvas.DrawLine(7, 16, 10, 16, paint);
        canvas.DrawLine(14, 16, 17, 16, paint);
    }

    private static void DrawExchange(SKCanvas canvas, SKPaint paint)
    {
        // Two arrows (exchange)
        paint.StrokeWidth = 2.5f;
        // Arrow up-right
        canvas.DrawLine(5, 14, 15, 4, paint);
        canvas.DrawLine(15, 4, 11, 4, paint);
        canvas.DrawLine(15, 4, 15, 8, paint);
        
        // Arrow down-left
        canvas.DrawLine(19, 10, 9, 20, paint);
        canvas.DrawLine(9, 20, 13, 20, paint);
        canvas.DrawLine(9, 20, 9, 16, paint);
        paint.StrokeWidth = 2f;
    }

    private static void DrawStar(SKCanvas canvas, SKPaint fillPaint)
    {
        // 5-point star
        using var path = new SKPath();
        float centerX = 12f;
        float centerY = 12f;
        float outerRadius = 10f;
        float innerRadius = 4f;
        
        for (int i = 0; i < 10; i++)
        {
            float radius = (i % 2 == 0) ? outerRadius : innerRadius;
            float angle = (i * 36 - 90) * (float)System.Math.PI / 180f;
            float x = centerX + radius * (float)System.Math.Cos(angle);
            float y = centerY + radius * (float)System.Math.Sin(angle);
            
            if (i == 0) path.MoveTo(x, y);
            else path.LineTo(x, y);
        }
        path.Close();
        canvas.DrawPath(path, fillPaint);
    }

    private static void DrawWallet(SKCanvas canvas, SKPaint paint, SKPaint fillPaint)
    {
        // Wallet shape
        canvas.DrawRoundRect(new SKRect(3, 7, 21, 19), 2, 2, paint);
        canvas.DrawLine(3, 10, 21, 10, paint);
        
        // Card slot
        canvas.DrawRect(new SKRect(17, 12, 20, 16), fillPaint);
    }

    private static void DrawBell(SKCanvas canvas, SKPaint paint)
    {
        // Bell shape
        using var path = new SKPath();
        path.MoveTo(12, 3);
        path.LineTo(10, 5);
        path.LineTo(10, 12);
        path.CubicTo(10, 15, 10.5f, 17, 12, 18);
        path.CubicTo(13.5f, 17, 14, 15, 14, 12);
        path.LineTo(14, 5);
        path.Close();
        canvas.DrawPath(path, paint);
        
        // Clapper
        canvas.DrawCircle(12, 18, 1, paint);
        
        // Bottom arc
        canvas.DrawLine(10, 19, 14, 19, paint);
    }
}
