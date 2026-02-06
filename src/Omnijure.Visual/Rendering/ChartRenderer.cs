
using SkiaSharp;
using Omnijure.Core.DataStructures;

namespace Omnijure.Visual.Rendering;



public enum ChartType { Candles, Line, Area, Bars }

public class UiButton 
{
    public SKRect Rect;
    public string Text;
    public Action Action;
    public bool IsHovered;

    public UiButton(float x, float y, float w, float h, string text, Action action)
    {
        Rect = new SKRect(x, y, x + w, y + h);
        Text = text;
        Action = action;
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);
}

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

    // Render method...

    public void Render(SKCanvas canvas, int width, int height, RingBuffer<Candle> buffer, string decision, int scrollOffset, float zoom, string symbol, string interval, ChartType chartType, System.Collections.Generic.List<UiButton> buttons, float minPrice, float maxPrice)
    {
        // 1. Layout Margins for Axes
        // We want axes OUTSIDE the chart body.
        const int RightAxisWidth = 60;
        const int BottomAxisHeight = 30;
        
        int chartW = width - RightAxisWidth;
        int chartH = height - BottomAxisHeight;
        
        // Clear background area (Sidebar is handled by LayoutManager, but this is Chart Area)
        using var bgPaint = new SKPaint { Color = new SKColor(13, 17, 23) };
        canvas.DrawRect(0, 0, width, height, bgPaint);

        if (buffer.Count == 0 && scrollOffset >= 0) {
             DrawHeader(canvas, 0, symbol, interval, 0, 0, decision, buttons);
             return;
        }

        // 2. Metrics
        int baseView = 150;
        int visibleCandles = (int)(baseView / zoom);
        if (visibleCandles < 10) visibleCandles = 10; 
        
        float candleWidth = (float)chartW / visibleCandles;
        
        // 3. Grid & Axes
        // CLIPPING: Draw content only in Chart Body
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, chartW, chartH));
        
        DrawGrid(canvas, chartW, chartH, minPrice, maxPrice, visibleCandles, candleWidth);
        
        // Switch Draw methods
        switch(chartType)
        {
            case ChartType.Candles: DrawCandles(canvas, buffer, visibleCandles, scrollOffset, candleWidth, chartH, minPrice, maxPrice); break;
            case ChartType.Line: DrawLineChart(canvas, buffer, visibleCandles, scrollOffset, candleWidth, chartH, minPrice, maxPrice); break;
            case ChartType.Area: DrawAreaChart(canvas, buffer, visibleCandles, scrollOffset, candleWidth, chartH, minPrice, maxPrice); break;
        }
        
        canvas.Restore(); // End Clip
        
        // 5. Draw Axes (Outside Clip)
        DrawPriceAxis(canvas, chartW, chartH, width, height, minPrice, maxPrice);
        DrawTimeAxis(canvas, chartW, chartH, buffer, scrollOffset, visibleCandles, candleWidth, interval);

        // 6. Header
        float curPrice = buffer.Count > 0 ? buffer[0].Close : 0;
        DrawHeader(canvas, buffer.Count, symbol, interval, curPrice, maxPrice, decision, buttons);
    }
    
    // NEW: Separated Drawing Logic
    private void DrawCandles(SKCanvas canvas, RingBuffer<Candle> buffer, int visible, int offset, float candleWidth, int height, float min, float max)
    {
        using var greenPaint = new SKPaint { Color = new SKColor(38, 166, 154), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var redPaint = new SKPaint { Color = new SKColor(239, 83, 80), IsAntialias = true, Style = SKPaintStyle.Fill };
        using var wickPaint = new SKPaint { IsAntialias = true, StrokeWidth = 1 };

        float halfW = candleWidth * 0.4f;

        for (int i = 0; i < visible; i++)
        {
            int idx = i + offset;
            
            // Handle FUTURE (Negative Index) -> Skip
            if (idx < 0) continue;
            if (idx >= buffer.Count) break;

            ref var c = ref buffer[idx];

            float x = (visible - 1 - i) * candleWidth + (candleWidth / 2);
            
            float yOpen = MapPriceToY(c.Open, min, max, height);
            float yClose = MapPriceToY(c.Close, min, max, height);
            float yHigh = MapPriceToY(c.High, min, max, height);
            float yLow = MapPriceToY(c.Low, min, max, height);

            bool isGreen = c.Close >= c.Open;
            var paint = isGreen ? greenPaint : redPaint;
            wickPaint.Color = paint.Color;

            canvas.DrawLine(x, yHigh, x, yLow, wickPaint);

            float rectTop = System.Math.Min(yOpen, yClose);
            float rectBot = System.Math.Max(yOpen, yClose);
            if (System.Math.Abs(rectBot - rectTop) < 1) rectBot = rectTop + 1;

            canvas.DrawRect(x - halfW, rectTop, halfW * 2, rectBot - rectTop, paint);
        }
    }

    private void DrawLineChart(SKCanvas canvas, RingBuffer<Candle> buffer, int visible, int offset, float candleWidth, int height, float min, float max)
    {
        using var linePaint = new SKPaint { Color = SKColors.Cyan, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true };
        using var path = new SKPath();
        
        bool started = false;
        for (int i = 0; i < visible; i++)
        {
            int idx = i + offset;
            if (idx < 0) continue; // Future
            if (idx >= buffer.Count) break;
            
            float x = (visible - 1 - i) * candleWidth + (candleWidth / 2);
            float y = MapPriceToY(buffer[idx].Close, min, max, height);
            
            if (!started) { path.MoveTo(x, y); started = true; }
            else path.LineTo(x, y);
        }
        canvas.DrawPath(path, linePaint);
    }

    private void DrawAreaChart(SKCanvas canvas, RingBuffer<Candle> buffer, int visible, int offset, float candleWidth, int height, float min, float max)
    {
         // Same as line but fill
         // Defer to Line for now
         DrawLineChart(canvas, buffer, visible, offset, candleWidth, height, min, max);
    }

    private void DrawPriceAxis(SKCanvas canvas, int chartW, int chartH, int totalW, int totalH, float minPrice, float maxPrice)
    {
        // Draw Axis Background
        using var bgPaint = new SKPaint { Color = new SKColor(22, 27, 34), Style = SKPaintStyle.Fill };
        canvas.DrawRect(chartW, 0, totalW - chartW, totalH, bgPaint); // Right strip
        
        using var linePaint = new SKPaint { Color = new SKColor(48, 54, 61), StrokeWidth = 1 };
        using var font = new SKFont(SKTypeface.Default, 11);
        using var textPaint = new SKPaint { Color = SKColors.Gray, IsAntialias = true };

        float range = maxPrice - minPrice;
        if (range <= 0) return;
        
        // Nice Numbers Logic
        double roughStep = range / 10.0;
        double magnitude = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(roughStep)));
        double normalizedStep = roughStep / magnitude;
        
        double stepSize;
        if (normalizedStep < 1.5) stepSize = 1 * magnitude;
        else if (normalizedStep < 3) stepSize = 2 * magnitude;
        else if (normalizedStep < 7) stepSize = 5 * magnitude;
        else stepSize = 10 * magnitude;
        
        float startPrice = (float)(System.Math.Floor(minPrice / stepSize) * stepSize);
        
        for (float p = startPrice; p <= maxPrice; p += (float)stepSize)
        {
            if (p < minPrice) continue;
            float y = MapPriceToY(p, minPrice, maxPrice, chartH);
            
            // Axis Label
            string label = p < 10 ? p.ToString("F4") : p.ToString("F2");
            
            float mx = chartW + 5; 
            float my = y + 4; 
            
            canvas.DrawText(label, mx, my, font, textPaint);
            
            // Draw Tick
            canvas.DrawLine(chartW, y, chartW + 4, y, linePaint);
        }
    }

    private void DrawTimeAxis(SKCanvas canvas, int chartW, int chartH, RingBuffer<Candle> buffer, int scrollOffset, int visibleCandles, float candleWidth, string interval)
    {
        using var linePaint = new SKPaint { Color = new SKColor(48, 54, 61), StrokeWidth = 1 };
        using var font = new SKFont(SKTypeface.Default, 11);
        using var textPaint = new SKPaint { Color = SKColors.Gray, IsAntialias = true };

        if (buffer.Count == 0 && scrollOffset >= 0) return;
        
        int skip = (int)(100 / candleWidth); 
        if (skip < 1) skip = 1;

        var latestTs = (buffer.Count > 0) ? buffer[0].Timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DateTime latestTime = DateTimeOffset.FromUnixTimeMilliseconds(latestTs).LocalDateTime;
        TimeSpan intervalSpan = ParseInterval(interval);

        for (int i = 0; i < visibleCandles; i += skip)
        {
            int idx = i + scrollOffset;
            
            DateTime time;
            if (idx < 0)
            {
                // Future
                time = latestTime.Add(intervalSpan * (-idx));
            }
            else if (idx < buffer.Count)
            {
                long ts = buffer[idx].Timestamp;
                time = DateTimeOffset.FromUnixTimeMilliseconds(ts).LocalDateTime;
            }
            else
            {
                continue; 
            }
            
            float x = (visibleCandles - 1 - i) * candleWidth + (candleWidth / 2);
            
            // Draw Tick
            canvas.DrawLine(x, chartH, x, chartH + 4, linePaint);
            
            // Label
            string label = time.ToString("HH:mm");
            if (time.Hour == 0 && time.Minute == 0) label = time.ToString("dd MMM");
            
            float tw = font.MeasureText(label);
            canvas.DrawText(label, x - tw/2, chartH + 18, font, textPaint);
        }
    }

    private void DrawGrid(SKCanvas canvas, int chartW, int chartH, float minPrice, float maxPrice, int visibleCandles, float candleWidth)
    {
        using var paint = new SKPaint { Color = new SKColor(30, 34, 40), IsAntialias = true, StrokeWidth = 1 };

        // Vertical Lines
        int skip = (int)(100 / candleWidth); 
        if (skip < 1) skip = 1;
        
        for (int i = 0; i < visibleCandles; i += skip)
        {
             float x = (visibleCandles - 1 - i) * candleWidth + (candleWidth / 2);
             canvas.DrawLine(x, 0, x, chartH, paint);
        }
        
        // Horizontal Lines
        float range = maxPrice - minPrice;
        if (range <= 0) return;
        double roughStep = range / 10.0;
        double magnitude = System.Math.Pow(10, System.Math.Floor(System.Math.Log10(roughStep)));
        double normalizedStep = roughStep / magnitude;
        double stepSize = (normalizedStep < 1.5) ? 1 * magnitude : (normalizedStep < 3 ? 2 * magnitude : (normalizedStep < 7 ? 5 * magnitude : 10 * magnitude));
        
        float startPrice = (float)(System.Math.Floor(minPrice / stepSize) * stepSize);
        for (float p = startPrice; p <= maxPrice; p += (float)stepSize)
        {
            if (p < minPrice) continue;
            float y = MapPriceToY(p, minPrice, maxPrice, chartH);
            canvas.DrawLine(0, y, chartW, y, paint);
        }
    }
    
    private TimeSpan ParseInterval(string interval)
    {
        if (interval == "1m") return TimeSpan.FromMinutes(1);
        if (interval == "5m") return TimeSpan.FromMinutes(5);
        if (interval == "15m") return TimeSpan.FromMinutes(15);
        if (interval == "1h") return TimeSpan.FromHours(1);
        return TimeSpan.FromMinutes(1);
    }

    private void DrawHeader(SKCanvas canvas, int count, string symbol, string interval, float price, float highDay, string decision, System.Collections.Generic.List<UiButton> buttons)
    {
        // HEADER BAR Background
        using var barPaint = new SKPaint { Color = new SKColor(20, 22, 28), Style = SKPaintStyle.Fill };
        canvas.DrawRect(0, 0, canvas.DeviceClipBounds.Width, 50, barPaint);
        
        // Draw UI Buttons
        using var btnFill = new SKPaint { Color = new SKColor(40, 44, 52), Style = SKPaintStyle.Fill };
        using var btnHover = new SKPaint { Color = new SKColor(60, 64, 72), Style = SKPaintStyle.Fill };
        using var textPaint = new SKPaint { Color = SKColors.White, TextSize = 14, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Segoe UI") };

        if (buttons != null)
        {
            foreach(var btn in buttons)
            {
                canvas.DrawRoundRect(btn.Rect, 5, 5, btn.IsHovered ? btnHover : btnFill);
                
                // Center text
                float textWidth = textPaint.MeasureText(btn.Text);
                float tx = btn.Rect.Left + (btn.Rect.Width - textWidth) / 2;
                float ty = btn.Rect.Top + (btn.Rect.Height + 10) / 2; 
                canvas.DrawText(btn.Text, tx, ty, textPaint);
            }
        }
        
        // PRICE
        SKColor priceColor = SKColors.White;
        using var pricePaint = new SKPaint { Color = priceColor, TextSize = 16, IsAntialias = true, Typeface = SKTypeface.FromFamilyName("Consolas", SKFontStyle.Bold) };
        canvas.DrawText(price.ToString("F2") + " " + symbol, canvas.DeviceClipBounds.Width - 300, 30, pricePaint);
    }
    
    // NEW: "The Brain" Layer
    private float MapPriceToY(float price, float min, float max, float height)
    {
        // Invert Y because 0 is top
        float range = max - min;
        if (range == 0) return height / 2;
        float normalized = (price - min) / range;
        return height - (normalized * height); 
    }
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
