using SkiaSharp;
using Omnijure.Core.DataStructures;
using Silk.NET.Maths;

namespace Omnijure.Visual.Rendering;

public class ChartRenderer
{
    private readonly SKPaint _checkeredPaint;
    private readonly SKPaint _bullishPaint;
    private readonly SKPaint _bearishPaint;
    private readonly SKPaint _gridPaint;

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

        // Subtle grid like TradingView - low opacity for better readability
        _gridPaint = new SKPaint { Color = new SKColor(48, 54, 61, 40), IsAntialias = false, StrokeWidth = 1 };
    }

    // Render method...

    public void Render(SKCanvas canvas, int width, int height, RingBuffer<Candle> buffer, string decision, int scrollOffset, float zoom, string symbol, string interval, ChartType chartType, System.Collections.Generic.List<UiButton> buttons, float minPrice, float maxPrice, Vector2D<float> mousePos)
    {
        // 1. Layout Margins (TradingView style with volume panel)
        const int RightAxisWidth = 60;
        const int BottomAxisHeight = 30;
        const int VolumeHeight = 80;  // Volume panel height

        int chartW = width - RightAxisWidth;
        int mainChartH = height - BottomAxisHeight - VolumeHeight;
        int volumeChartH = VolumeHeight;
        int totalChartH = height - BottomAxisHeight;
        
        // Clear
        using var bgPaint = new SKPaint { Color = new SKColor(13, 17, 23) };
        canvas.DrawRect(0, 0, width, height, bgPaint);

        if (buffer.Count == 0 && scrollOffset >= 0) {
             DrawHeader(canvas, 0, symbol, interval, 0, 0, decision, buttons);
             return;
        }

        // 2. Metrics
        // Candle width is now the primary driver of zoom, rather than a fixed count per screen.
        float baseCandleWidth = 8.0f;
        float candleWidth = baseCandleWidth * zoom;
        if (candleWidth < 1.0f) candleWidth = 1.0f; // Minimum width
        
        int visibleCandles = (int)Math.Ceiling(chartW / candleWidth);
        if (visibleCandles < 2) visibleCandles = 2; 
        
        // 3. Main Chart - Grid & Candles (Clipped Content)
        canvas.Save();
        canvas.ClipRect(new SKRect(0, 0, chartW, mainChartH));

        DrawGrid(canvas, chartW, mainChartH, minPrice, maxPrice, visibleCandles, candleWidth, interval, buffer, scrollOffset);

        switch(chartType)
        {
            case ChartType.Candles: DrawCandles(canvas, buffer, visibleCandles, scrollOffset, candleWidth, mainChartH, minPrice, maxPrice); break;
            case ChartType.Line: DrawLineChart(canvas, buffer, visibleCandles, scrollOffset, candleWidth, mainChartH, minPrice, maxPrice); break;
            case ChartType.Area: DrawAreaChart(canvas, buffer, visibleCandles, scrollOffset, candleWidth, mainChartH, minPrice, maxPrice); break;
        }

        // Draw indicators (SMA lines like TradingView)
        DrawIndicators(canvas, buffer, visibleCandles, scrollOffset, candleWidth, mainChartH, minPrice, maxPrice);

        // CROSSHAIR LINES (Inside Main Chart Clip)
        bool isHoverChart = mousePos.X >= 0 && mousePos.X <= chartW && mousePos.Y >= 0 && mousePos.Y <= mainChartH;
        if (isHoverChart)
        {
             DrawCrosshairLines(canvas, mousePos.X, mousePos.Y, chartW, mainChartH);
        }

        canvas.Restore(); // End Main Chart Clip

        // 4. Volume Panel (TradingView style)
        canvas.Save();
        canvas.ClipRect(new SKRect(0, mainChartH, chartW, totalChartH));
        DrawVolumePanel(canvas, buffer, visibleCandles, scrollOffset, candleWidth, mainChartH, volumeChartH, chartW);
        canvas.Restore(); // End Volume Clip

        // 5. Draw Axes (Outside Clip)
        DrawPriceAxis(canvas, chartW, mainChartH, width, height, minPrice, maxPrice);
        DrawTimeAxis(canvas, chartW, totalChartH, buffer, scrollOffset, visibleCandles, candleWidth, interval);

        // Draw current price indicator (TradingView style)
        if (buffer.Count > 0)
        {
            float currentPrice = buffer[0].Close;
            float prevPrice = buffer.Count > 1 ? buffer[1].Close : currentPrice;
            bool isGreen = currentPrice >= prevPrice;
            DrawCurrentPriceIndicator(canvas, chartW, mainChartH, currentPrice, minPrice, maxPrice, isGreen);
        }

        // CROSSHAIR LABELS (Over Axes)
        if (isHoverChart)
        {
            DrawCrosshairLabels(canvas, mousePos.X, mousePos.Y, chartW, mainChartH, minPrice, maxPrice, buffer, scrollOffset, visibleCandles, candleWidth, interval);
        }
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

    private void DrawCurrentPriceIndicator(SKCanvas canvas, int chartW, int chartH, float currentPrice, float minPrice, float maxPrice, bool isGreen)
    {
        float y = MapPriceToY(currentPrice, minPrice, maxPrice, chartH);

        // Draw horizontal line across chart (like TradingView)
        using var linePaint = new SKPaint
        {
            Color = isGreen ? new SKColor(38, 166, 154) : new SKColor(239, 83, 80),
            StrokeWidth = 1,
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0)
        };
        canvas.DrawLine(0, y, chartW, y, linePaint);

        // Draw price label box on right axis (TradingView style)
        using var font = new SKFont(SKTypeface.Default, 11);
        string priceLabel = currentPrice < 10 ? currentPrice.ToString("F4") : currentPrice.ToString("F2");
        float labelWidth = font.MeasureText(priceLabel);

        using var boxPaint = new SKPaint
        {
            Color = isGreen ? new SKColor(38, 166, 154) : new SKColor(239, 83, 80),
            Style = SKPaintStyle.Fill
        };
        using var textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };

        float boxHeight = 18;
        SKRect priceBox = new SKRect(chartW, y - boxHeight / 2, chartW + labelWidth + 16, y + boxHeight / 2);
        canvas.DrawRect(priceBox, boxPaint);
        canvas.DrawText(priceLabel, chartW + 8, y + 4, font, textPaint);
    }

    private void DrawTimeAxis(SKCanvas canvas, int chartW, int chartH, RingBuffer<Candle> buffer, int scrollOffset, int visibleCandles, float candleWidth, string interval)
    {
        using var linePaint = new SKPaint { Color = new SKColor(48, 54, 61), StrokeWidth = 1 };
        using var font = new SKFont(SKTypeface.Default, 11);
        using var textPaint = new SKPaint { Color = SKColors.Gray, IsAntialias = true };

        if (buffer.Count == 0 && scrollOffset >= 0) return;

        int skip = (int)(100 / candleWidth);
        if (skip < 1) skip = 1;

        // Calculate phase to synchronize with grid (same as DrawGrid)
        int phase = (skip - (scrollOffset % skip)) % skip;

        var latestTs = (buffer.Count > 0) ? buffer[0].Timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DateTime latestTime = DateTimeOffset.FromUnixTimeMilliseconds(latestTs).LocalDateTime;
        TimeSpan intervalSpan = ParseInterval(interval);

        for (int i = phase; i < visibleCandles; i += skip)
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

    private void DrawIndicators(SKCanvas canvas, RingBuffer<Candle> buffer, int visible, int offset, float candleWidth, int height, float min, float max)
    {
        if (buffer.Count < 50) return; // Need enough data for indicators

        // SMA 20 (Yellow line)
        DrawSMA(canvas, buffer, visible, offset, candleWidth, height, min, max, 20, new SKColor(255, 200, 50));

        // SMA 50 (Cyan line)
        DrawSMA(canvas, buffer, visible, offset, candleWidth, height, min, max, 50, new SKColor(100, 200, 255));
    }

    private void DrawSMA(SKCanvas canvas, RingBuffer<Candle> buffer, int visible, int offset, float candleWidth, int height, float min, float max, int period, SKColor color)
    {
        using var smaPaint = new SKPaint { Color = color, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        using var path = new SKPath();

        bool started = false;

        for (int i = 0; i < visible; i++)
        {
            int idx = i + offset;
            if (idx < 0 || idx >= buffer.Count) continue;

            // Calculate SMA
            if (idx + period > buffer.Count) continue; // Not enough data

            float sum = 0;
            for (int j = 0; j < period; j++)
            {
                sum += buffer[idx + j].Close;
            }
            float sma = sum / period;

            float x = (visible - 1 - i) * candleWidth + (candleWidth / 2);
            float y = MapPriceToY(sma, min, max, height);

            if (!started)
            {
                path.MoveTo(x, y);
                started = true;
            }
            else
            {
                path.LineTo(x, y);
            }
        }

        canvas.DrawPath(path, smaPaint);
    }

    private void DrawVolumePanel(SKCanvas canvas, RingBuffer<Candle> buffer, int visible, int offset, float candleWidth, float yOffset, int volumeHeight, int chartW)
    {
        if (buffer.Count == 0) return;

        // Calculate max volume for scaling
        float maxVolume = 0;
        for (int i = 0; i < visible; i++)
        {
            int idx = i + offset;
            if (idx < 0 || idx >= buffer.Count) continue;
            ref var c = ref buffer[idx];
            if (c.Volume > maxVolume) maxVolume = c.Volume;
        }

        if (maxVolume == 0) return;

        // Draw separator line
        using var separatorPaint = new SKPaint { Color = new SKColor(48, 54, 61), StrokeWidth = 1 };
        canvas.DrawLine(0, yOffset, chartW, yOffset, separatorPaint);

        // Draw volume bars
        using var greenVolPaint = new SKPaint { Color = new SKColor(38, 166, 154, 100), Style = SKPaintStyle.Fill };
        using var redVolPaint = new SKPaint { Color = new SKColor(239, 83, 80, 100), Style = SKPaintStyle.Fill };

        float barWidth = candleWidth * 0.8f;

        for (int i = 0; i < visible; i++)
        {
            int idx = i + offset;
            if (idx < 0 || idx >= buffer.Count) continue;

            ref var c = ref buffer[idx];

            float x = (visible - 1 - i) * candleWidth + (candleWidth / 2);

            // Calculate bar height (scale to volume panel height)
            float barHeight = (c.Volume / maxVolume) * (volumeHeight - 10);
            float y = yOffset + volumeHeight - barHeight;

            // Green if close >= open, red otherwise
            bool isGreen = c.Close >= c.Open;
            var paint = isGreen ? greenVolPaint : redVolPaint;

            canvas.DrawRect(x - barWidth / 2, y, barWidth, barHeight, paint);
        }

        // Draw volume label
        using var font = new SKFont(SKTypeface.Default, 10);
        using var textPaint = new SKPaint { Color = new SKColor(128, 128, 128), IsAntialias = true };
        canvas.DrawText("Volume", 5, yOffset + 15, font, textPaint);
    }

    private void DrawGrid(SKCanvas canvas, int chartW, int chartH, float minPrice, float maxPrice, int visibleCandles, float candleWidth, string interval, RingBuffer<Candle> buffer, int scrollOffset)
    {
        // Horizontal Lines (Price grid) - Aligned with price axis
        float range = maxPrice - minPrice;
        if (range > 0)
        {
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
                canvas.DrawLine(0, y, chartW, y, _gridPaint);
            }
        }

        // Vertical Lines (Time grid) - Synchronized with time axis labels
        // Calculate skip interval for grid density (matches DrawTimeAxis)
        int skip = (int)(100 / candleWidth);
        if (skip < 1) skip = 1;

        // Calculate phase offset to synchronize grid with time axis
        // Phase must move in the correct direction with scroll
        int phase = (skip - (scrollOffset % skip)) % skip;

        // Draw grid lines at same positions as time axis labels
        for (int i = phase; i < visibleCandles; i += skip)
        {
            // Draw grid line at this position
            float x = (visibleCandles - 1 - i) * candleWidth + (candleWidth / 2);

            // Ensure the line is within the chart bounds
            if (x >= 0 && x <= chartW)
            {
                canvas.DrawLine(x, 0, x, chartH, _gridPaint);
            }
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
                
            }
        }
    }
    private void DrawCrosshairLines(SKCanvas canvas, float x, float y, int w, int h)
    {
        using var paint = new SKPaint 
        { 
            Color = SKColors.Gray, 
            Style = SKPaintStyle.Stroke, 
            StrokeWidth = 1, 
            PathEffect = SKPathEffect.CreateDash(new float[] { 4, 4 }, 0) 
        };
        
        // Horizontal
        canvas.DrawLine(0, y, w, y, paint);
        // Vertical
        canvas.DrawLine(x, 0, x, h, paint);
    }
    
    private void DrawCrosshairLabels(SKCanvas canvas, float x, float y, int chartW, int chartH, float min, float max, RingBuffer<Candle> buffer, int scrollOffset, int visible, float candleWidth, string interval)
    {
        using var bgPaint = new SKPaint { Color = new SKColor(50, 50, 50), Style = SKPaintStyle.Fill };
        using var textPaint = new SKPaint { Color = SKColors.White, TextSize = 11, IsAntialias = true };
        
        // 1. Price Label (Right Axis)
        // Map Y back to Price
        // Y = Height - (Norm * Height)
        // Norm * Height = Height - Y
        // Norm = (Height - Y) / Height
        // Price = Min + Norm * Range
        float range = max - min;
        float norm = (chartH - y) / chartH;
        float price = min + (norm * range);
        
        string priceLabel = price.ToString("F2");
        float pw = textPaint.MeasureText(priceLabel);
        float ph = 14; 
        
        SKRect priceRect = new SKRect(chartW, y - ph/2, chartW + pw + 10, y + ph/2);
        canvas.DrawRect(priceRect, bgPaint);
        canvas.DrawText(priceLabel, chartW + 5, y + 4, textPaint);
        
        // 2. Time Label (Bottom Axis)
        // Map X back to Index
        // x = (visible - 1 - i) * cw + cw/2
        // i approx = (visible - 1) - (x / cw)
        float iFloat = (visible - 1) - ((x - candleWidth/2) / candleWidth);
        int i = (int)Math.Round(iFloat);
        int idx = i + scrollOffset;
        
        DateTime time = DateTime.UtcNow; // Fallback
        
        var latestTs = (buffer.Count > 0) ? buffer[0].Timestamp : DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        DateTime latestTime = DateTimeOffset.FromUnixTimeMilliseconds(latestTs).LocalDateTime;
        TimeSpan intervalSpan = ParseInterval(interval);

        if (idx < 0)
        {
             time = latestTime.Add(intervalSpan * (-idx));
        }
        else if (idx < buffer.Count && idx >= 0)
        {
            time = DateTimeOffset.FromUnixTimeMilliseconds(buffer[idx].Timestamp).LocalDateTime;
        }
        // If idx too large, use oldest time? Or hide?
        
        string timeLabel = time.ToString("dd MMM HH:mm");
        float tw = textPaint.MeasureText(timeLabel);
        
        SKRect timeRect = new SKRect(x - tw/2 - 5, chartH, x + tw/2 + 5, chartH + 18);
        canvas.DrawRect(timeRect, bgPaint);
        canvas.DrawText(timeLabel, x - tw/2, chartH + 14, textPaint);
    }
}
