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

        _bgPaint = new SKPaint { Color = ThemeManager.Background, Style = SKPaintStyle.Fill };
        _headerTextPaint = new SKPaint { Color = ThemeManager.TextWhite, IsAntialias = true };
        _linePaint = new SKPaint { Color = ThemeManager.Border, StrokeWidth = 1 };

        _askPaint = new SKPaint { Color = ThemeManager.WithAlpha(ThemeManager.BearishRed, 40), Style = SKPaintStyle.Fill };
        _bidPaint = new SKPaint { Color = ThemeManager.WithAlpha(ThemeManager.BullishGreen, 40), Style = SKPaintStyle.Fill };

        _askTextPaint = new SKPaint { Color = ThemeManager.BearishRed, IsAntialias = true };
        _bidTextPaint = new SKPaint { Color = ThemeManager.BullishGreen, IsAntialias = true };
    }

    public void RenderOrderBook(SKCanvas canvas, float width, float height, OrderBook orderBook)
    {
        // Ya no dibujamos fondo ni header - el panel lo maneja
        
        if (orderBook == null) 
        {
            RenderEmptyState(canvas, width, height, "No order book data");
            return;
        }

        lock(orderBook)
        {
            float rowH = 18;
            float startY = 10;
            
            var asks = orderBook.Asks.Take(20).ToList();
            var bids = orderBook.Bids.Take(20).ToList();
            
            float maxQty = 0;
            if (asks.Count > 0) maxQty = Math.Max(maxQty, asks.Max(a => a.Quantity));
            if (bids.Count > 0) maxQty = Math.Max(maxQty, bids.Max(b => b.Quantity));
            if (maxQty == 0) maxQty = 1;

            float y = startY;
            
            // Asks (Reverse so highest price is top)
            foreach(var ask in asks.AsEnumerable().Reverse())
            {
                float barW = (ask.Quantity / maxQty) * width;
                canvas.DrawRect(width - barW, y - rowH + 4, barW, rowH, _askPaint);
                canvas.DrawText(ask.Price.ToString("F2"), 10, y, _itemFont, _askTextPaint);
                canvas.DrawText(ask.Quantity.ToString("F4"), width / 2, y, _itemFont, _headerTextPaint);
                y += rowH;
            }

            y += 5;
            canvas.DrawLine(10, y, width - 10, y, _linePaint);
            y += 15;

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

    public void RenderTrades(SKCanvas canvas, float width, float height, RingBuffer<MarketTrade> trades)
    {
        if (trades == null || trades.Count == 0)
        {
            RenderEmptyState(canvas, width, height, "No recent trades");
            return;
        }

        float rowH = 20;
        float y = 10;
        
        // Column headers
        using var headerPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
        using var headerFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 10);
        canvas.DrawText("PRICE", 10, y, headerFont, headerPaint);
        canvas.DrawText("AMOUNT", width / 2 - 10, y, headerFont, headerPaint);
        canvas.DrawText("TIME", width - 60, y, headerFont, headerPaint);
        
        y += 18;
        canvas.DrawLine(5, y, width - 5, y, _linePaint);
        y += 8;
        
        // Show last trades
        int count = Math.Min(trades.Count, (int)((height - y) / rowH));
        
        using var timePaint = new SKPaint { Color = ThemeManager.TextSecondary, IsAntialias = true };
        
        for (int i = 0; i < count; i++)
        {
            var t = trades[i];
            var paint = t.IsBuyerMaker ? _askTextPaint : _bidTextPaint;
            
            canvas.DrawText(t.Price.ToString("F2"), 10, y, _itemFont, paint);
            canvas.DrawText(t.Quantity.ToString("F4"), width / 2 - 10, y, _itemFont, _headerTextPaint);
            
            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(t.Timestamp).LocalDateTime;
            canvas.DrawText(dt.ToString("HH:mm:ss"), width - 60, y, _itemFont, timePaint);
            
            y += rowH;
        }
    }

    public void RenderPositions(SKCanvas canvas, float width, float height)
    {
        RenderEmptyState(canvas, width, height, "No open positions");
    }

    private void RenderEmptyState(SKCanvas canvas, float width, float height, string message)
    {
        using var emptyPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
        using var emptyFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 12);
        
        float textWidth = TextMeasureCache.Instance.MeasureText(message, emptyFont);
        canvas.DrawText(message, (width - textWidth) / 2, height / 2, emptyFont, emptyPaint);
    }
}
