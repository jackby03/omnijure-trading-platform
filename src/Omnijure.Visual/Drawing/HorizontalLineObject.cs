using SkiaSharp;
using Omnijure.Core.DataStructures;

namespace Omnijure.Visual.Drawing;

/// <summary>
/// Horizontal line drawing - represents a constant price level across time
/// </summary>
public class HorizontalLineObject : DrawingObject
{
    /// <summary>
    /// Price level for this horizontal line
    /// </summary>
    public float Price { get; set; }

    /// <summary>
    /// Optional label to display on the line
    /// </summary>
    public string? Label { get; set; }

    public HorizontalLineObject()
    {
        Color = new SKColor(255, 193, 7); // Default yellow/gold
    }

    public HorizontalLineObject(float price)
    {
        Price = price;
        Color = new SKColor(255, 193, 7);
    }

    public override void Draw(SKCanvas canvas, RingBuffer<Candle> buffer,
        int visibleCandles, int scrollOffset, float candleWidth,
        int chartHeight, float minPrice, float maxPrice)
    {
        // Convert price to screen Y coordinate
        float y = PriceToY(Price, minPrice, maxPrice, chartHeight);

        // Only draw if within visible range
        if (y < 0 || y > chartHeight) return;

        float chartWidth = visibleCandles * candleWidth;

        using var paint = GetPaint();
        canvas.DrawLine(0, y, chartWidth, y, paint);

        // Draw price label on the right side
        if (!string.IsNullOrEmpty(Label) || IsSelected)
        {
            using var font = new SKFont(SKTypeface.Default, 11);
            using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
            using var bgPaint = new SKPaint { Color = Color, Style = SKPaintStyle.Fill };

            string displayText = Label ?? Price.ToString("F2");
            float textWidth = font.MeasureText(displayText);

            // Draw background box
            SKRect labelRect = new SKRect(
                chartWidth - textWidth - 10,
                y - 9,
                chartWidth - 2,
                y + 9
            );
            canvas.DrawRect(labelRect, bgPaint);

            // Draw text
            canvas.DrawText(displayText, chartWidth - textWidth - 6, y + 4, font, textPaint);
        }

        // Draw selection handles if selected
        if (IsSelected)
        {
            float chartWidth2 = visibleCandles * candleWidth;
            DrawSelectionHandles(canvas, new[] {
                new SKPoint(chartWidth2 / 2, y)
            });
        }
    }

    public override bool HitTest(float x, float y, float tolerance)
    {
        // For hit testing, we need the chart parameters to convert price to Y
        // Since we don't have them in this method signature, we'll return false for now
        // Proper implementation would require adding chart parameters to HitTest
        // TODO: Refactor HitTest to include minPrice, maxPrice, chartHeight
        return false;
    }

    /// <summary>
    /// Performs hit test with chart parameters
    /// </summary>
    public bool HitTest(float x, float y, float minPrice, float maxPrice, int chartHeight, float tolerance)
    {
        float lineY = PriceToY(Price, minPrice, maxPrice, chartHeight);
        return System.Math.Abs(y - lineY) <= tolerance;
    }
}
