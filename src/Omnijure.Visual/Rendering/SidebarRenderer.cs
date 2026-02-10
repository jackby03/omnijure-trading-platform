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
        canvas.DrawRect(0, 0, width, height, _bgPaint);
        canvas.DrawText("ORDER BOOK", 10, 25, _headerFont, _headerTextPaint);
        canvas.DrawLine(0, 35, width, 35, _linePaint);
        
        if (orderBook == null) return;

        lock(orderBook)
        {
            float rowH = 18;
            float startY = 50;
            
            var asks = orderBook.Asks.Take(20).ToList();
            var bids = orderBook.Bids.Take(20).ToList();
            
            float maxQty = 0;
            if (asks.Count > 0) maxQty = Math.Max(maxQty, asks.Max(a => a.Quantity));
            if (bids.Count > 0) maxQty = Math.Max(maxQty, bids.Max(b => b.Quantity));
            if (maxQty == 0) maxQty = 1;

            float y = startY;
            // Asks (Reverse so highest price is top of the list if we want, or lowest is bottom)
            // TradingView style: Asks on top, Bids on bottom. Asks sorted Ascending (lowest price bottom of asks block).
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

    public void RenderRightSidebar(SKCanvas canvas, float width, float height, RingBuffer<MarketTrade> trades)
    {
        canvas.DrawRect(0, 0, width, height, _bgPaint);
        
        // 1. WATCHLIST (Top Half)
        float watchlistH = height * 0.4f;
        canvas.DrawText("WATCHLIST", 10, 25, _headerFont, _headerTextPaint);
        canvas.DrawLine(0, 35, width, 35, _linePaint);
        
        float y = 60;
        string[] symbols = { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT" };
        using var itemTextPaint = new SKPaint { Color = ThemeManager.TextSecondary, IsAntialias = true };

        foreach(var s in symbols)
        {
            canvas.DrawText(s, 10, y, _itemFont, itemTextPaint);
            canvas.DrawText("---", width - 60, y, _itemFont, itemTextPaint);
            y += 25;
            if (y > watchlistH - 20) break;
        }

        // 2. MARKET TRADES (Bottom Half)
        float tradesStartY = watchlistH;
        canvas.DrawLine(0, tradesStartY, width, tradesStartY, _linePaint);
        canvas.DrawText("MARKET TRADES", 10, tradesStartY + 25, _headerFont, _headerTextPaint);
        canvas.DrawLine(0, tradesStartY + 35, width, tradesStartY + 35, _linePaint);

        if (trades == null || trades.Count == 0) return;

        float rowH = 20;
        y = tradesStartY + 55;
        
        // Show last ~20-30 trades
        int count = Math.Min(trades.Count, (int)((height - y) / rowH));
        
        for (int i = 0; i < count; i++)
        {
            var t = trades[i];
            var paint = t.IsBuyerMaker ? _askTextPaint : _bidTextPaint; // BuyerMaker = Sell (Red)
            
            canvas.DrawText(t.Price.ToString("F2"), 10, y, _itemFont, paint);
            canvas.DrawText(t.Quantity.ToString("F4"), width / 2, y, _itemFont, _headerTextPaint);
            
            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(t.Timestamp).LocalDateTime;
            canvas.DrawText(dt.ToString("HH:mm:ss"), width - 60, y, _itemFont, itemTextPaint);
            
            y += rowH;
        }
    }
}
