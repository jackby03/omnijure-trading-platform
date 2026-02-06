using SkiaSharp;
using Omnijure.Core.DataStructures;
using System.Linq;

namespace Omnijure.Visual.Rendering;

public class SidebarRenderer
{
    private readonly SKFont _headerFont;
    private readonly SKFont _itemFont;
    private readonly SKPaint _bgPaint;
    private readonly SKPaint _headerTextPaint;
    private readonly SKPaint _linePaint;
    private readonly SKPaint _askPaint;
    private readonly SKPaint _bidPaint;
    private readonly SKPaint _askTextPaint;
    private readonly SKPaint _bidTextPaint;

    public SidebarRenderer()
    {
        var typeface = SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold);
        _headerFont = new SKFont(typeface, 14);
        _itemFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 12);
        
        _bgPaint = new SKPaint { Color = new SKColor(13, 17, 23), Style = SKPaintStyle.Fill };
        _headerTextPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _linePaint = new SKPaint { Color = new SKColor(48, 54, 61), StrokeWidth = 1 };
        
        _askPaint = new SKPaint { Color = SKColors.Red.WithAlpha(40), Style = SKPaintStyle.Fill };
        _bidPaint = new SKPaint { Color = SKColors.Green.WithAlpha(40), Style = SKPaintStyle.Fill };
        
        _askTextPaint = new SKPaint { Color = new SKColor(239, 83, 80), IsAntialias = true };
        _bidTextPaint = new SKPaint { Color = new SKColor(38, 166, 154), IsAntialias = true };
    }

    public void Render(SKCanvas canvas, float width, float height, OrderBook orderBook)
    {
        canvas.DrawRect(0, 0, width, height, _bgPaint);
        
        // Header "Watchlist"
        canvas.DrawText("WATCHLIST", 10, 25, _headerFont, _headerTextPaint);
        canvas.DrawLine(0, 35, width, 35, _linePaint);
        
        float y = 60;
        string[] symbols = { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT" };
        using var itemTextPaint = new SKPaint { Color = SKColors.LightGray, IsAntialias = true };

        foreach(var s in symbols)
        {
            canvas.DrawText(s, 10, y, _itemFont, itemTextPaint);
            canvas.DrawText("---", width - 60, y, _itemFont, itemTextPaint);
            y += 25;
        }
        
        // Order Book
        float midY = height / 2;
        canvas.DrawLine(0, midY, width, midY, _linePaint);
        canvas.DrawText("ORDER BOOK", 10, midY + 25, _headerFont, _headerTextPaint);
        
        if (orderBook == null) return;

        lock(orderBook)
        {
            float rowH = 18;
            float startY = midY + 40;
            
            // Limit to e.g. 15 levels
            var asks = orderBook.Asks.Take(15).ToList();
            var bids = orderBook.Bids.Take(15).ToList();
            
            float maxQty = 0;
            if (asks.Count > 0) maxQty = Math.Max(maxQty, asks.Max(a => a.Quantity));
            if (bids.Count > 0) maxQty = Math.Max(maxQty, bids.Max(b => b.Quantity));
            if (maxQty == 0) maxQty = 1;

            // Draw Asks (Red) - Showing the lowest asks first (at the top or merged)
            // Usually Order book shows Asks on top, Bids on bottom.
            // Asks are sorted by price ascending.
            y = startY;
            foreach(var ask in asks.AsEnumerable().Reverse()) // Reverse so lowest is closer to middle if we want
            {
                float barW = (ask.Quantity / maxQty) * width;
                canvas.DrawRect(width - barW, y - rowH + 4, barW, rowH, _askPaint);
                canvas.DrawText(ask.Price.ToString("F2"), 10, y, _itemFont, _askTextPaint);
                canvas.DrawText(ask.Quantity.ToString("F4"), width / 2, y, _itemFont, _headerTextPaint);
                y += rowH;
            }

            // Middle Price
            y += 5;
            canvas.DrawLine(10, y, width - 10, y, _linePaint);
            y += 15;

            // Draw Bids (Green)
            foreach(var bid in bids)
            {
                float barW = (bid.Quantity / maxQty) * width;
                canvas.DrawRect(width - barW, y - rowH + 4, barW, rowH, _bidPaint);
                canvas.DrawText(bid.Price.ToString("F2"), 10, y, _itemFont, _bidTextPaint);
                canvas.DrawText(bid.Quantity.ToString("F4"), width / 2, y, _itemFont, _headerTextPaint);
                y += rowH;
            }
        }
    }
}
