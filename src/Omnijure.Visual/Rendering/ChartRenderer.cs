
using SkiaSharp;
using Omnijure.Core.DataStructures;

namespace Omnijure.Visual.Rendering;

public class ChartRenderer
{
    private readonly SKPaint _checkeredPaint;
    private readonly SKPaint _bullishPaint;
    private readonly SKPaint _bearishPaint;
    
    // Performance: Allocate paints once
    public ChartRenderer()
    {
        _checkeredPaint = new SKPaint
        {
            Color = SKColors.DarkGray.WithAlpha(50),
            IsAntialias = false
        };
        
        _bullishPaint = new SKPaint { Color = SKColors.Green, IsAntialias = true, Style = SKPaintStyle.Fill };
        _bearishPaint = new SKPaint { Color = SKColors.Red, IsAntialias = true, Style = SKPaintStyle.Fill };
    }

    public void Render(SKCanvas canvas, int width, int height, RingBuffer<Candle> buffer, string decision, int scrollOffset, float zoom)
    {
        // PRODUCTION: Institutional Dark Background
        canvas.Clear(SKColors.Black);
        
        // Safety check
        if (buffer.Count == 0) return;

        // 1. Calculate Visible Range
        // Base view is ~150 candles. Zoom > 1.0 means MORE candles (Zoom Out) or FEWER?
        // Let's standard: Zoom 1.0 = 150 candles. Zoom 2.0 = 75 candles (Zoom In).
        // Wait, normally Zoom > 1 means Closer (fewer items).
        int baseView = 150;
        int visibleCandles = (int)(baseView / zoom);
        if (visibleCandles < 5) visibleCandles = 5;
        if (visibleCandles > buffer.Count) visibleCandles = buffer.Count;

        // Offset: 0 means Latest. 
        // Max offset = Count - visible
        if (scrollOffset > buffer.Count - visibleCandles) scrollOffset = buffer.Count - visibleCandles;
        if (scrollOffset < 0) scrollOffset = 0;

        // 2. Calculate Scale (Min/Max Price) over the VISIBLE range
        float maxPrice = float.MinValue;
        float minPrice = float.MaxValue;
        
        // Find min/max in the window [scrollOffset ... scrollOffset + visibleCandles]
        for (int i = 0; i < visibleCandles; i++)
        {
            if (i + scrollOffset >= buffer.Count) break;
            
            ref var c = ref buffer[i + scrollOffset];
            if (c.High > maxPrice) maxPrice = c.High;
            if (c.Low < minPrice) minPrice = c.Low;
        }

        // Padding
        float priceRange = maxPrice - minPrice;
        if (priceRange == 0) priceRange = 1;

        // 3. Draw Grid
        DrawGrid(canvas, width, height, minPrice, maxPrice); // Vertical grid needs update for timeline

        // 3.1 Draw Architecture (Order Blocks)
        // DrawOrderBlocks(canvas, buffer, visibleCandles, minPrice, maxPrice, width, height, scrollOffset);

        // 4. Draw Candles
        float candleWidth = (float)width / visibleCandles;
        float gap = candleWidth * 0.2f;
        float bodyWidth = candleWidth - gap;

        using var whalePaint = new SKPaint { Color = SKColors.Gold, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };

        for (int i = 0; i < visibleCandles; i++)
        {
            int index = i + scrollOffset;
            if (index >= buffer.Count) break;

            ref var c = ref buffer[index];
            
            // X matches visual index 'i', not data index
            float x = width - ((i + 1) * candleWidth) + (gap / 2);
            
            float yOpen = MapPriceToY(c.Open, minPrice, maxPrice, height);
            float yClose = MapPriceToY(c.Close, minPrice, maxPrice, height);
            float yHigh = MapPriceToY(c.High, minPrice, maxPrice, height);
            float yLow = MapPriceToY(c.Low, minPrice, maxPrice, height);

            var paint = c.Close >= c.Open ? _bullishPaint : _bearishPaint;
            
            // WHALE DETECTOR LOGIC (Index based on data, not view)
            // Need robust detection. For now, visual.
            bool isWhale = (index % 20 == 0); 
            if (isWhale)
            {
               canvas.DrawRect(x - 2, yHigh - 2, bodyWidth + 4, (yLow - yHigh) + 4, whalePaint);
            }

            canvas.DrawRect(x + bodyWidth/2 - 1, yHigh, 2, yLow - yHigh, paint);

            float rectTop = Math.Min(yOpen, yClose);
            float rectHeight = Math.Abs(yClose - yOpen);
            if (rectHeight < 1) rectHeight = 1; 
            
            canvas.DrawRect(x, rectTop, bodyWidth, rectHeight, paint);
        }

        DrawHud(canvas, visibleCandles, maxPrice, minPrice, decision);
    }
    
    private float MapPriceToY(float price, float min, float max, float height)
    {
        // Invert Y because 0 is top
        float range = max - min;
        if (range == 0) return height / 2;
        float normalized = (price - min) / range;
        return height - (normalized * height); 
    }

    private void DrawHud(SKCanvas canvas, int count, float max, float min, string decision)
    {
        using var textPaint = new SKPaint
        {
            Color = SKColors.White,
            TextSize = 12,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas")
        };
        
        canvas.DrawText($"BUFFER: {count}", 10, 20, textPaint);
        canvas.DrawText($"HIGH: {max:F2}", 10, 40, textPaint);
        canvas.DrawText($"LOW:  {min:F2}", 10, 60, textPaint);
        
        // DECISION DISPLAY
        using var decisionPaint = new SKPaint 
        { 
            Color = decision.Contains("sell") ? SKColors.Red : (decision.Contains("buy") ? SKColors.Green : SKColors.Yellow),
            TextSize = 20,
            IsAntialias = true,
            Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold)
        };
        canvas.DrawText($"AI: {decision.ToUpper()}", 10, 90, decisionPaint);
    }

    private void DrawGrid(SKCanvas canvas, int width, int height, float min, float max)
    {
        using var gridPaint = new SKPaint { Color = SKColors.Gray.WithAlpha(50), StrokeWidth = 1 };
        
        // Draw Horizontal Price Lines
        int lines = 10;
        for (int i = 0; i < lines; i++)
        {
            float y = (height / (float)lines) * i;
            canvas.DrawLine(0, y, width, y, gridPaint);
        }
    }

    // NEW: "The Brain" Layer
    private void DrawOrderBlocks(SKCanvas canvas, RingBuffer<Candle> buffer, int visible, float min, float max, float width, float height)
    {
        // Conceptual: Identifying pivots. For visual demo, we simulate OBs based on local extrema.
        float candleWidth = width / visible;
        
        using var obBullish = new SKPaint { Color = SKColors.Green.WithAlpha(50), Style = SKPaintStyle.Fill };
        using var obBearish = new SKPaint { Color = SKColors.Red.WithAlpha(50), Style = SKPaintStyle.Fill };

        for (int i = 5; i < visible - 5; i++)
        {
            ref var c = ref buffer[i];
            
            // Simple Swing High Detection (visual test)
            if (c.High > buffer[i+1].High && c.High > buffer[i+2].High && c.High > buffer[i-1].High && c.High > buffer[i-2].High)
            {
                // Draw Bearish Order Block zone extended to right
                float yTop = MapPriceToY(c.High, min, max, height);
                float yBottom = MapPriceToY(c.Low, min, max, height); // Candle body range
                
                float xStart = width - ((i + 1) * candleWidth);
                
                canvas.DrawRect(xStart, yTop, width - xStart, yBottom - yTop, obBearish);
            }
            
            // Simple Swing Low Detection
             if (c.Low < buffer[i+1].Low && c.Low < buffer[i+2].Low && c.Low < buffer[i-1].Low && c.Low < buffer[i-2].Low)
            {
                // Draw Bullish Order Block
                float yTop = MapPriceToY(c.High, min, max, height);
                float yBottom = MapPriceToY(c.Low, min, max, height);
                
                float xStart = width - ((i + 1) * candleWidth);
                
                canvas.DrawRect(xStart, yTop, width - xStart, yBottom - yTop, obBullish);
            }
        }
    }
}
