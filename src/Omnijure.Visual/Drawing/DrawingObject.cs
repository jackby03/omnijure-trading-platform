using SkiaSharp;
using Omnijure.Core.DataStructures;

namespace Omnijure.Visual.Drawing;

/// <summary>
/// Base class for all drawable objects on the chart.
/// Drawings are stored in price/time coordinates, not pixel coordinates,
/// so they scale correctly with zoom and pan operations.
/// </summary>
public abstract class DrawingObject
{
    /// <summary>
    /// Color of the drawing
    /// </summary>
    public SKColor Color { get; set; }

    /// <summary>
    /// Line thickness in pixels
    /// </summary>
    public float Thickness { get; set; } = 2f;

    /// <summary>
    /// Whether this drawing is currently selected
    /// </summary>
    public bool IsSelected { get; set; } = false;

    /// <summary>
    /// Drawing style (solid, dashed, dotted)
    /// </summary>
    public LineStyle Style { get; set; } = LineStyle.Solid;

    /// <summary>
    /// Renders the drawing on the canvas
    /// </summary>
    /// <param name="canvas">Canvas to draw on</param>
    /// <param name="buffer">Price data buffer for coordinate conversion</param>
    /// <param name="visibleCandles">Number of visible candles</param>
    /// <param name="scrollOffset">Current scroll offset</param>
    /// <param name="candleWidth">Width of each candle in pixels</param>
    /// <param name="chartHeight">Height of the chart in pixels</param>
    /// <param name="minPrice">Minimum visible price</param>
    /// <param name="maxPrice">Maximum visible price</param>
    public abstract void Draw(SKCanvas canvas, RingBuffer<Candle> buffer,
        int visibleCandles, int scrollOffset, float candleWidth,
        int chartHeight, float minPrice, float maxPrice);

    /// <summary>
    /// Tests if a screen point hits this drawing
    /// </summary>
    /// <param name="x">Screen x coordinate</param>
    /// <param name="y">Screen y coordinate</param>
    /// <param name="tolerance">Hit tolerance in pixels</param>
    /// <returns>True if the point hits this drawing</returns>
    public abstract bool HitTest(float x, float y, float tolerance);

    /// <summary>
    /// Gets a paint object for this drawing's style
    /// </summary>
    protected SKPaint GetPaint()
    {
        var paint = new SKPaint
        {
            Color = Color,
            StrokeWidth = IsSelected ? Thickness + 1 : Thickness,
            Style = SKPaintStyle.Stroke,
            IsAntialias = true
        };

        // Apply line style
        switch (Style)
        {
            case LineStyle.Dashed:
                paint.PathEffect = SKPathEffect.CreateDash(new float[] { 8, 4 }, 0);
                break;
            case LineStyle.Dotted:
                paint.PathEffect = SKPathEffect.CreateDash(new float[] { 2, 2 }, 0);
                break;
        }

        return paint;
    }

    /// <summary>
    /// Converts price to screen Y coordinate
    /// </summary>
    protected float PriceToY(float price, float minPrice, float maxPrice, int chartHeight)
    {
        if (maxPrice <= minPrice) return chartHeight / 2;
        float range = maxPrice - minPrice;
        float normalized = (price - minPrice) / range;
        return chartHeight - (normalized * chartHeight);
    }

    /// <summary>
    /// Converts screen Y coordinate to price
    /// </summary>
    protected float YToPrice(float y, float minPrice, float maxPrice, int chartHeight)
    {
        if (chartHeight <= 0) return minPrice;
        float normalized = (chartHeight - y) / chartHeight;
        return minPrice + (normalized * (maxPrice - minPrice));
    }

    /// <summary>
    /// Converts candle index to screen X coordinate
    /// </summary>
    protected float IndexToX(int index, int visibleCandles, int scrollOffset, float candleWidth)
    {
        int screenIndex = index - scrollOffset;
        return (visibleCandles - 1 - screenIndex) * candleWidth + (candleWidth / 2);
    }

    /// <summary>
    /// Converts screen X coordinate to candle index
    /// </summary>
    protected int XToIndex(float x, int visibleCandles, int scrollOffset, float candleWidth)
    {
        int screenIndex = (int)((visibleCandles - 1) - (x - candleWidth / 2) / candleWidth);
        return screenIndex + scrollOffset;
    }

    /// <summary>
    /// Draws selection handles for this object
    /// </summary>
    protected void DrawSelectionHandles(SKCanvas canvas, SKPoint[] points)
    {
        if (!IsSelected) return;

        using var handlePaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };

        using var borderPaint = new SKPaint
        {
            Color = Color,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };

        foreach (var point in points)
        {
            canvas.DrawCircle(point, 4, handlePaint);
            canvas.DrawCircle(point, 4, borderPaint);
        }
    }
}

/// <summary>
/// Line drawing styles
/// </summary>
public enum LineStyle
{
    Solid,
    Dashed,
    Dotted
}
