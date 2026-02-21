using SkiaSharp;

namespace Omnijure.Visual.Shared.Lib.Drawing;

/// <summary>
/// Trend line drawing - connects two points on the chart
/// </summary>
public class TrendLineObject : DrawingObject
{
    /// <summary>
    /// Starting point: (candle index, price)
    /// </summary>
    public (int Index, float Price) Start { get; set; }

    /// <summary>
    /// Ending point: (candle index, price)
    /// </summary>
    public (int Index, float Price) End { get; set; }

    /// <summary>
    /// Whether to extend the line infinitely
    /// </summary>
    public bool ExtendLine { get; set; } = false;

    public TrendLineObject()
    {
        Color = new SKColor(120, 130, 255); // Default blue
    }

    public override void Draw(SKCanvas canvas, RingBuffer<Candle> buffer,
        int visibleCandles, int scrollOffset, float candleWidth,
        int chartHeight, float minPrice, float maxPrice)
    {
        // Convert data coordinates to screen coordinates
        float x1 = IndexToX(Start.Index, visibleCandles, scrollOffset, candleWidth);
        float y1 = PriceToY(Start.Price, minPrice, maxPrice, chartHeight);
        float x2 = IndexToX(End.Index, visibleCandles, scrollOffset, candleWidth);
        float y2 = PriceToY(End.Price, minPrice, maxPrice, chartHeight);

        var paint = GetPaint();

        try
        {
            if (ExtendLine)
            {
                // Calculate line slope and extend to chart edges
                float dx = x2 - x1;
                float dy = y2 - y1;

                if (System.Math.Abs(dx) > 0.01f) // Avoid division by zero
                {
                    float slope = dy / dx;

                    // Extend to left edge (x=0)
                    float leftY = y1 - (x1 * slope);

                    // Extend to right edge (x=chartWidth)
                    float chartWidth = visibleCandles * candleWidth;
                    float rightY = y1 + ((chartWidth - x1) * slope);

                    canvas.DrawLine(0, leftY, chartWidth, rightY, paint);
                }
                else
                {
                    // Vertical line
                    canvas.DrawLine(x1, 0, x1, chartHeight, paint);
                }
            }
            else
            {
                // Draw line segment only between the two points
                canvas.DrawLine(x1, y1, x2, y2, paint);
            }

            // Draw selection handles if selected
            if (IsSelected)
            {
                DrawSelectionHandles(canvas, new[] {
                    new SKPoint(x1, y1),
                    new SKPoint(x2, y2)
                });
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public override bool HitTest(float x, float y, float tolerance)
    {
        // Hit testing without chart parameters - return false
        // Use the overload with chart parameters for proper hit testing
        return false;
    }

    /// <summary>
    /// Performs hit test with chart parameters
    /// </summary>
    public bool HitTest(float x, float y, int visibleCandles, int scrollOffset, float candleWidth,
        int chartHeight, float minPrice, float maxPrice, float tolerance)
    {
        // Convert data coordinates to screen coordinates
        float x1 = IndexToX(Start.Index, visibleCandles, scrollOffset, candleWidth);
        float y1 = PriceToY(Start.Price, minPrice, maxPrice, chartHeight);
        float x2 = IndexToX(End.Index, visibleCandles, scrollOffset, candleWidth);
        float y2 = PriceToY(End.Price, minPrice, maxPrice, chartHeight);

        // Calculate perpendicular distance from point to line segment
        float dx = x2 - x1;
        float dy = y2 - y1;
        float lengthSquared = dx * dx + dy * dy;

        if (lengthSquared < 0.0001f) // Line is basically a point
        {
            float dist = (float)System.Math.Sqrt((x - x1) * (x - x1) + (y - y1) * (y - y1));
            return dist <= tolerance;
        }

        // Calculate projection parameter t
        float t = System.Math.Max(0, System.Math.Min(1, ((x - x1) * dx + (y - y1) * dy) / lengthSquared));

        // Find closest point on line segment
        float closestX = x1 + t * dx;
        float closestY = y1 + t * dy;

        // Calculate distance to closest point
        float distance = (float)System.Math.Sqrt((x - closestX) * (x - closestX) + (y - closestY) * (y - closestY));

        return distance <= tolerance;
    }
}
