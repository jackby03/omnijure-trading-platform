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

    public void RenderOrderBook(SKCanvas canvas, float width, float height, OrderBook orderBook, PanelPosition position)
    {
        if (orderBook == null)
        {
            RenderEmptyState(canvas, width, height, "No order book data");
            return;
        }

        // Vertical (single column) for Left/Right docks; Horizontal (two columns) for Bottom/others
        bool vertical = position is PanelPosition.Left or PanelPosition.Right;

        if (vertical)
            RenderOrderBookVertical(canvas, width, height, orderBook);
        else
            RenderOrderBookHorizontal(canvas, width, height, orderBook);
    }

    // =========================================================================
    // VERTICAL MODE - single column: Asks (top, reversed) -> Spread -> Bids (bottom)
    // Used when docked Left or Right
    // =========================================================================
    private void RenderOrderBookVertical(SKCanvas canvas, float width, float height, OrderBook orderBook)
    {
        lock (orderBook)
        {
            var asks = orderBook.Asks.Take(20).ToList();
            var bids = orderBook.Bids.Take(20).ToList();

            float maxQty = 0;
            if (asks.Count > 0) maxQty = Math.Max(maxQty, asks.Max(a => a.Quantity));
            if (bids.Count > 0) maxQty = Math.Max(maxQty, bids.Max(b => b.Quantity));
            if (maxQty == 0) maxQty = 1;

            using var hdrFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
            using var hdrPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
            using var spreadFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 10);

            float rowH = 15;
            float headerH = 16;
            float spreadBarH = 22;

            // Header: PRICE | QTY
            canvas.DrawText("PRICE", 6, headerH - 4, hdrFont, hdrPaint);
            float qtyHdrW = hdrFont.MeasureText("QTY");
            canvas.DrawText("QTY", width - 6 - qtyHdrW, headerH - 4, hdrFont, hdrPaint);
            canvas.DrawLine(4, headerH, width - 4, headerH, _linePaint);

            // Calculate how many rows fit in each half (asks top, bids bottom)
            float availH = height - headerH - spreadBarH;
            int halfRows = Math.Max(1, (int)(availH / 2 / rowH));
            int askRows = Math.Min(asks.Count, halfRows);
            int bidRows = Math.Min(bids.Count, halfRows);

            // -- ASKS (top half, reversed so lowest ask is nearest the spread) --
            float askStartY = headerH + 2;
            for (int i = 0; i < askRows; i++)
            {
                int idx = askRows - 1 - i; // reverse: highest ask at top, lowest near spread
                float y = askStartY + i * rowH;
                float textY = y + rowH - 3;

                // Depth bar (grows from right edge)
                float barW = (asks[idx].Quantity / maxQty) * (width - 8);
                canvas.DrawRect(width - 4 - barW, y, barW, rowH - 1, _askPaint);

                // Price
                canvas.DrawText(asks[idx].Price.ToString("F2"), 6, textY, _itemFont, _askTextPaint);

                // Qty (right-aligned)
                string qty = asks[idx].Quantity.ToString("F4");
                float qtyW = _itemFont.MeasureText(qty);
                canvas.DrawText(qty, width - 6 - qtyW, textY, _itemFont, _headerTextPaint);
            }

            // -- SPREAD BAR (center divider) --
            float spreadY = askStartY + askRows * rowH + 2;

            using var spreadBgPaint = new SKPaint { Color = new SKColor(28, 32, 42), Style = SKPaintStyle.Fill };
            canvas.DrawRect(0, spreadY, width, spreadBarH, spreadBgPaint);
            canvas.DrawLine(4, spreadY, width - 4, spreadY, _linePaint);
            canvas.DrawLine(4, spreadY + spreadBarH, width - 4, spreadY + spreadBarH, _linePaint);

            if (bids.Count > 0 && asks.Count > 0)
            {
                float spread = asks[0].Price - bids[0].Price;
                float spreadPct = (spread / asks[0].Price) * 100;
                string spreadText = $"{spread:F2} ({spreadPct:F3}%)";
                float spreadW = spreadFont.MeasureText(spreadText);
                using var spreadValPaint = new SKPaint { Color = new SKColor(255, 200, 50), IsAntialias = true };
                canvas.DrawText(spreadText, (width - spreadW) / 2, spreadY + spreadBarH - 6, spreadFont, spreadValPaint);
            }

            // -- BIDS (bottom half) --
            float bidStartY = spreadY + spreadBarH + 2;
            for (int i = 0; i < bidRows; i++)
            {
                float y = bidStartY + i * rowH;
                float textY = y + rowH - 3;

                // Depth bar (grows from right edge)
                float barW = (bids[i].Quantity / maxQty) * (width - 8);
                canvas.DrawRect(width - 4 - barW, y, barW, rowH - 1, _bidPaint);

                // Price
                canvas.DrawText(bids[i].Price.ToString("F2"), 6, textY, _itemFont, _bidTextPaint);

                // Qty (right-aligned)
                string qty = bids[i].Quantity.ToString("F4");
                float qtyW = _itemFont.MeasureText(qty);
                canvas.DrawText(qty, width - 6 - qtyW, textY, _itemFont, _headerTextPaint);
            }
        }
    }

    // =========================================================================
    // HORIZONTAL MODE - two columns: Bids (left) | Asks (right)
    // Used when docked Bottom
    // =========================================================================
    private void RenderOrderBookHorizontal(SKCanvas canvas, float width, float height, OrderBook orderBook)
    {
        lock (orderBook)
        {
            var asks = orderBook.Asks.Take(25).ToList();
            var bids = orderBook.Bids.Take(25).ToList();

            float maxQty = 0;
            if (asks.Count > 0) maxQty = Math.Max(maxQty, asks.Max(a => a.Quantity));
            if (bids.Count > 0) maxQty = Math.Max(maxQty, bids.Max(b => b.Quantity));
            if (maxQty == 0) maxQty = 1;

            float midX = width / 2;
            float colW = midX - 4;
            float rowH = 16;
            float headerH = 20;

            // Column headers
            using var hdrFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
            using var hdrPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };

            // Bids header (left)
            canvas.DrawText("BID", 8, headerH - 6, hdrFont, hdrPaint);
            canvas.DrawText("QTY", colW - 30, headerH - 6, hdrFont, hdrPaint);

            // Asks header (right)
            canvas.DrawText("QTY", midX + 4, headerH - 6, hdrFont, hdrPaint);
            canvas.DrawText("ASK", width - 40, headerH - 6, hdrFont, hdrPaint);

            // Separator line under headers
            canvas.DrawLine(4, headerH, width - 4, headerH, _linePaint);

            // Center divider
            using var divPaint = new SKPaint { Color = new SKColor(35, 40, 50), StrokeWidth = 1 };
            canvas.DrawLine(midX, headerH, midX, height, divPaint);

            int maxRows = Math.Min(Math.Max(bids.Count, asks.Count), (int)((height - headerH - 4) / rowH));

            // Render rows
            for (int i = 0; i < maxRows; i++)
            {
                float y = headerH + 4 + i * rowH;
                float textY = y + rowH - 3;

                // Bid (left column) - bars grow right-to-left from center
                if (i < bids.Count)
                {
                    float barW = (bids[i].Quantity / maxQty) * colW;
                    canvas.DrawRect(midX - 2 - barW, y, barW, rowH - 1, _bidPaint);
                    canvas.DrawText(bids[i].Price.ToString("F2"), 6, textY, _itemFont, _bidTextPaint);

                    string bidQty = bids[i].Quantity.ToString("F4");
                    float qtyW = _itemFont.MeasureText(bidQty);
                    canvas.DrawText(bidQty, midX - 6 - qtyW, textY, _itemFont, _headerTextPaint);
                }

                // Ask (right column) - bars grow left-to-right from center
                if (i < asks.Count)
                {
                    float barW = (asks[i].Quantity / maxQty) * colW;
                    canvas.DrawRect(midX + 2, y, barW, rowH - 1, _askPaint);

                    canvas.DrawText(asks[i].Quantity.ToString("F4"), midX + 6, textY, _itemFont, _headerTextPaint);

                    string askPrice = asks[i].Price.ToString("F2");
                    float priceW = _itemFont.MeasureText(askPrice);
                    canvas.DrawText(askPrice, width - 6 - priceW, textY, _itemFont, _askTextPaint);
                }
            }

            // Spread indicator at bottom center
            if (bids.Count > 0 && asks.Count > 0)
            {
                float spread = asks[0].Price - bids[0].Price;
                float spreadPct = (spread / asks[0].Price) * 100;
                string spreadText = $"Spread: {spread:F2} ({spreadPct:F3}%)";
                using var spreadFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 9);
                float spreadW = spreadFont.MeasureText(spreadText);
                using var spreadPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
                canvas.DrawText(spreadText, (width - spreadW) / 2, height - 4, spreadFont, spreadPaint);
            }
        }
    }

    public void RenderTrades(SKCanvas canvas, float width, float height, RingBuffer<MarketTrade> trades, PanelPosition position)
    {
        if (trades == null || trades.Count == 0)
        {
            RenderEmptyState(canvas, width, height, "No recent trades");
            return;
        }

        bool vertical = position is PanelPosition.Left or PanelPosition.Right;

        if (vertical)
            RenderTradesVertical(canvas, width, height, trades);
        else
            RenderTradesHorizontal(canvas, width, height, trades);
    }

    // =========================================================================
    // TRADES VERTICAL - single column: Price | Qty | Time per row
    // Used when docked Left or Right
    // =========================================================================
    private void RenderTradesVertical(SKCanvas canvas, float width, float height, RingBuffer<MarketTrade> trades)
    {
        float rowH = 17;
        float y = 12;

        using var headerPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
        using var headerFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
        using var timePaint = new SKPaint { Color = ThemeManager.TextSecondary, IsAntialias = true };

        // Single column header
        canvas.DrawText("PRICE", 6, y, headerFont, headerPaint);
        canvas.DrawText("QTY", width * 0.42f, y, headerFont, headerPaint);
        string timeHdr = "TIME";
        float timeHdrW = headerFont.MeasureText(timeHdr);
        canvas.DrawText(timeHdr, width - 6 - timeHdrW, y, headerFont, headerPaint);

        float divY = y + 6;
        canvas.DrawLine(2, divY, width - 2, divY, _linePaint);

        y = divY + 5 + rowH;

        int maxRows = (int)((height - y) / rowH);
        int count = Math.Min(trades.Count, maxRows);

        for (int i = 0; i < count; i++)
        {
            float ry = y + i * rowH;
            var t = trades[i];
            var paint = t.IsBuyerMaker ? _askTextPaint : _bidTextPaint;

            canvas.DrawText(t.Price.ToString("F2"), 6, ry, _itemFont, paint);
            canvas.DrawText(t.Quantity.ToString("F4"), width * 0.42f, ry, _itemFont, _headerTextPaint);

            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(t.Timestamp).LocalDateTime;
            string timeStr = dt.ToString("HH:mm:ss");
            float timeW = _itemFont.MeasureText(timeStr);
            canvas.DrawText(timeStr, width - 6 - timeW, ry, _itemFont, timePaint);
        }
    }

    // =========================================================================
    // TRADES HORIZONTAL - N columns layout
    // Used when docked Bottom
    // =========================================================================
    private void RenderTradesHorizontal(SKCanvas canvas, float width, float height, RingBuffer<MarketTrade> trades)
    {
        float rowH = 17;
        float y = 12;

        float colW = 220;
        int numCols = Math.Max(1, (int)(width / colW));
        float actualColW = width / numCols;

        using var headerPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
        using var headerFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
        using var timePaint = new SKPaint { Color = ThemeManager.TextSecondary, IsAntialias = true };

        for (int c = 0; c < numCols; c++)
        {
            float cx = c * actualColW;
            canvas.DrawText("PRICE", cx + 6, y, headerFont, headerPaint);
            canvas.DrawText("QTY", cx + actualColW * 0.48f, y, headerFont, headerPaint);
            canvas.DrawText("TIME", cx + actualColW - 46, y, headerFont, headerPaint);
        }

        float divY = y + 6;
        canvas.DrawLine(2, divY, width - 2, divY, _linePaint);

        for (int c = 1; c < numCols; c++)
        {
            float sx = c * actualColW;
            canvas.DrawLine(sx, 0, sx, height, _linePaint);
        }

        y = divY + 5 + rowH;

        int rowsPerCol = (int)((height - y) / rowH);
        int totalVisible = rowsPerCol * numCols;
        int count = Math.Min(trades.Count, totalVisible);

        for (int i = 0; i < count; i++)
        {
            int col = i / rowsPerCol;
            int row = i % rowsPerCol;

            if (row >= rowsPerCol) continue;

            float cx = col * actualColW;
            float ry = y + row * rowH;

            var t = trades[i];
            var paint = t.IsBuyerMaker ? _askTextPaint : _bidTextPaint;

            canvas.DrawText(t.Price.ToString("F2"), cx + 6, ry, _itemFont, paint);
            canvas.DrawText(t.Quantity.ToString("F4"), cx + actualColW * 0.48f, ry, _itemFont, _headerTextPaint);

            DateTime dt = DateTimeOffset.FromUnixTimeMilliseconds(t.Timestamp).LocalDateTime;
            canvas.DrawText(dt.ToString("HH:mm:ss"), cx + actualColW - 46, ry, _itemFont, timePaint);
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
