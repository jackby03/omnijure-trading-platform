using SkiaSharp;

namespace Omnijure.Visual.Rendering;

public class SidebarRenderer
{
    public void Render(SKCanvas canvas, float width, float height)
    {
        // Background
        using var bgPaint = new SKPaint { Color = new SKColor(13, 17, 23), Style = SKPaintStyle.Fill }; // Github Dim Theme
        canvas.DrawRect(0, 0, width, height, bgPaint);
        
        // Header "Watchlist"
        using var textPaint = new SKPaint 
        { 
            Color = SKColors.White, 
            IsAntialias = true, 
            TextSize = 14,
            Typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold)
        };
        
        canvas.DrawText("WATCHLIST", 10, 25, textPaint);
        
        // Separator
        using var linePaint = new SKPaint { Color = new SKColor(48, 54, 61), StrokeWidth = 1 };
        canvas.DrawLine(0, 35, width, 35, linePaint);
        
        // Placeholder items
        float y = 60;
        string[] symbols = { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT", "ADAUSDT", "DOGEUSDT" };
        
        using var itemPaint = new SKPaint { Color = SKColors.LightGray, IsAntialias = true, TextSize = 13 };
        using var pricePaint = new SKPaint { Color = SKColors.Green, IsAntialias = true, TextSize = 13 };

        foreach(var s in symbols)
        {
            canvas.DrawText(s, 10, y, itemPaint);
            canvas.DrawText("0.00", width - 50, y, pricePaint);
            y += 30;
        }
        
        // Bottom: Order Book Placeholder
        float midY = height / 2;
        canvas.DrawLine(0, midY, width, midY, linePaint);
        canvas.DrawText("ORDER BOOK", 10, midY + 25, textPaint);
        
        // DEPTH VISUAL (Fake)
        y = midY + 50;
        using var askPaint = new SKPaint { Color = SKColors.Red.WithAlpha(100), Style = SKPaintStyle.Fill };
        using var bidPaint = new SKPaint { Color = SKColors.Green.WithAlpha(100), Style = SKPaintStyle.Fill };
        
        // Asks
        for(int i=0; i<5; i++)
        {
             canvas.DrawRect(width/2, y, width/2 * 0.8f, 18, askPaint);
             y += 20;
        }
        y += 10;
        // Bids
        for(int i=0; i<5; i++)
        {
             canvas.DrawRect(width/2, y, width/2 * 0.9f, 18, bidPaint);
             y += 20;
        }
    }
}
