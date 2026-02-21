using System;
using SkiaSharp;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Panels;

public class PositionsRenderer : IPanelRenderer
{
    public string PanelId => PanelDefinitions.POSITIONS;
    private float _contentHeight = 0;

    public void Render(SKCanvas canvas, SKRect rect, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontHeader = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 9);
            using var fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 10);
            using var fontValue = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 12);

            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;

            float width = rect.Width;
            float height = rect.Height;
            float px = 8;
            float rx = width - px;
            float rowH = 48;

            // FIXED HEADER (no scroll)
            float sumY = 4;
            float cardGap = 6;
            float cardW = (width - px * 2 - cardGap * 3) / 4;

            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px, sumY, cardW, "Margin", "$2,140.00", new SKColor(200, 205, 215));
            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px + cardW + cardGap, sumY, cardW, "Unrealized PnL", "+$187.42", new SKColor(46, 204, 113));
            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px + (cardW + cardGap) * 2, sumY, cardW, "ROE", "+8.76%", new SKColor(46, 204, 113));
            DrawSummaryCard(canvas, paint, fontSmall, fontValue,
                px + (cardW + cardGap) * 3, sumY, cardW, "Positions", "4 open", new SKColor(200, 205, 215));

            float headerY = sumY + 48;
            float usableW = width - px * 2;
            float[] cols = [
                px + 4,
                px + usableW * 0.22f,
                px + usableW * 0.37f,
                px + usableW * 0.52f,
                px + usableW * 0.68f,
                rx - 56
            ];
            string[] headers = ["Symbol", "Entry", "Mark Price", "PnL (ROE)", "Liq. Price", ""];

            paint.Color = new SKColor(65, 70, 80);
            for (int i = 0; i < headers.Length && i < cols.Length; i++)
            {
                if (cols[i] < width && headers[i].Length > 0)
                    canvas.DrawText(headers[i], cols[i], headerY, fontHeader, paint);
            }

            headerY += 6;
            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(px, headerY, rx, headerY, paint);
            paint.Style = SKPaintStyle.Fill;

            // SCROLLABLE ROWS
            float rowsTop = headerY + 2;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, rowsTop, width, height));
            canvas.Translate(0, -scrollY);

            float y = rowsTop + 4;

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "BTCUSDT", "Long", "0.015 BTC",
                "$67,240.00", "$69,061.00",
                "+$27.32", "+2.71%", true, "10x", "$62,100");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "ETHUSDT", "Long", "0.85 ETH",
                "$3,420.00", "$3,512.40",
                "+$78.54", "+2.70%", true, "5x", "$2,980");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "SOLUSDT", "Short", "12.0 SOL",
                "$158.20", "$153.80",
                "+$52.80", "+2.78%", true, "10x", "$174.50");

            DrawPositionRow(canvas, paint, fontNormal, fontSmall, fontBold,
                cols, width, ref y, rowH,
                "BNBUSDT", "Long", "1.5 BNB",
                "$612.40", "$608.50",
                "-$5.85", "-0.64%", false, "3x", "$420.80");

            canvas.Restore();
            
            _contentHeight = y - rowsTop;
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public float GetContentHeight()
    {
        return _contentHeight > 0 ? _contentHeight : 202; // Initial rowsTotalH approximation (rowH * 4 + 10)
    }

    private static void DrawSummaryCard(SKCanvas canvas, SKPaint paint, SKFont labelFont, SKFont valueFont,
        float x, float y, float w, string label, string value, SKColor valueColor)
    {
        float h = 38;
        paint.Color = new SKColor(20, 24, 32);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), 6, 6, paint);

        paint.Color = new SKColor(75, 80, 92);
        canvas.DrawText(label, x + 10, y + 14, labelFont, paint);

        paint.Color = valueColor;
        canvas.DrawText(value, x + 10, y + 30, valueFont, paint);
    }

    private static void DrawPositionRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont, SKFont boldFont,
        float[] cols, float width, ref float y, float rowH,
        string symbol, string side, string size,
        string entry, string mark,
        string pnl, string roe, bool isProfit, string leverage, string liqPrice)
    {
        float px = cols[0] - 4;

        paint.Color = new SKColor(18, 22, 30);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(px, y, width - 8, y + rowH - 2), 4, 4, paint);

        paint.Color = isProfit ? new SKColor(46, 204, 113, 180) : new SKColor(239, 83, 80, 180);
        canvas.DrawRoundRect(new SKRect(px, y + 4, px + 3, y + rowH - 6), 1.5f, 1.5f, paint);

        float topY = y + 16;
        float botY = y + 30;

        paint.Color = new SKColor(215, 220, 230);
        canvas.DrawText(symbol, cols[0] + 4, topY, boldFont, paint);

        float levX = cols[0] + 4 + boldFont.MeasureText(symbol) + 4;
        float levW = smallFont.MeasureText(leverage) + 8;
        paint.Color = new SKColor(50, 55, 70);
        canvas.DrawRoundRect(new SKRect(levX, topY - 9, levX + levW, topY + 2), 3, 3, paint);
        paint.Color = new SKColor(160, 165, 175);
        canvas.DrawText(leverage, levX + 4, topY - 1, smallFont, paint);

        bool isLong = side == "Long";
        paint.Color = isLong ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
        canvas.DrawText(side, cols[0] + 4, botY, smallFont, paint);
        paint.Color = new SKColor(100, 105, 115);
        canvas.DrawText(" " + size, cols[0] + 4 + smallFont.MeasureText(side), botY, smallFont, paint);

        if (cols[1] < width - 40)
        {
            paint.Color = new SKColor(160, 165, 175);
            canvas.DrawText(entry, cols[1], topY, font, paint);
        }

        if (cols[2] < width - 40)
        {
            paint.Color = new SKColor(220, 225, 235);
            canvas.DrawText(mark, cols[2], topY, boldFont, paint);
        }

        if (cols[3] < width - 40)
        {
            paint.Color = isProfit ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
            canvas.DrawText(pnl, cols[3], topY, boldFont, paint);
            canvas.DrawText(roe, cols[3], botY, smallFont, paint);
        }

        if (cols[4] < width - 60)
        {
            paint.Color = new SKColor(130, 135, 145);
            canvas.DrawText(liqPrice, cols[4], topY, font, paint);
        }

        if (cols.Length > 5 && cols[5] < width)
        {
            float btnX = cols[5];
            float btnY = y + rowH / 2 - 9;
            float btnW = 48;
            float btnH = 18;
            var btnRect = new SKRect(btnX, btnY, btnX + btnW, btnY + btnH);

            paint.Color = new SKColor(60, 30, 30);
            canvas.DrawRoundRect(btnRect, 4, 4, paint);

            paint.Color = new SKColor(239, 83, 80);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawRoundRect(btnRect, 4, 4, paint);

            paint.Style = SKPaintStyle.Fill;
            paint.Color = new SKColor(239, 120, 110);
            string closeLabel = "Close";
            float clW = smallFont.MeasureText(closeLabel);
            canvas.DrawText(closeLabel, btnX + (btnW - clW) / 2, btnY + 13, smallFont, paint);
        }

        y += rowH;
    }
}
