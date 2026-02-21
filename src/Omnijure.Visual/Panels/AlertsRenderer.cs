using System;
using SkiaSharp;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Panels;

public class AlertsRenderer : IPanelRenderer
{
    public string PanelId => PanelDefinitions.ALERTS;
    private const float AlertsFixedHeaderH = 58;
    private const int AlertsRowCount = 12;
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

            paint.IsAntialias = true;
            paint.Style = SKPaintStyle.Fill;
            
            float width = rect.Width;
            float height = rect.Height;
            float px = 8;
            float rx = width - px;

            // SCROLLABLE ROWS
            float rowsTop = AlertsFixedHeaderH;
            canvas.Save();
            canvas.ClipRect(new SKRect(0, rowsTop, width, height));
            canvas.Translate(0, -scrollY);

            float y = rowsTop + 4;

            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "BTCUSDT", "Price above", "$69,500.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "ETHUSDT", "Price below", "$3,200.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "SOLUSDT", "RSI > 70", "$158.40", "Triggered", new SKColor(255, 180, 50));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "BTCUSDT", "EMA Cross 9/21", "$68,900.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "BNBUSDT", "Price above", "$620.00", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "XRPUSDT", "Vol spike >200%", "$0.6240", "Triggered", new SKColor(255, 180, 50));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "ARBUSDT", "Price below", "$1.12", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "DOGEUSDT", "MACD Cross", "$0.1580", "Expired", new SKColor(120, 125, 135));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "PEPEUSDT", "Price above", "$0.00001200", "Triggered", new SKColor(255, 180, 50));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "LINKUSDT", "Price below", "$14.80", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "AVAXUSDT", "RSI < 30", "$35.20", "Active", new SKColor(46, 204, 113));
            DrawAlertRow(canvas, paint, fontNormal, fontSmall, px, rx, width, ref y,
                "DOTUSDT", "BB squeeze", "$7.45", "Expired", new SKColor(120, 125, 135));

            _contentHeight = y - rowsTop;
            
            canvas.Restore();

            // FIXED HEADER
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, 0, width, AlertsFixedHeaderH, paint);

            float hy = 4;
            float cardGap = 6;
            float cardW = (width - px * 2 - cardGap * 2) / 3;
            DrawAlertSummaryCard(canvas, paint, fontSmall, fontBold, px, hy, cardW, "Active", "7", new SKColor(46, 204, 113));
            DrawAlertSummaryCard(canvas, paint, fontSmall, fontBold, px + cardW + cardGap, hy, cardW, "Triggered", "3", new SKColor(255, 180, 50));
            DrawAlertSummaryCard(canvas, paint, fontSmall, fontBold, px + (cardW + cardGap) * 2, hy, cardW, "Expired", "2", new SKColor(120, 125, 135));
            hy += 40;

            paint.Color = new SKColor(65, 70, 80);
            canvas.DrawText("PAIR", px + 4, hy, fontHeader, paint);
            canvas.DrawText("CONDITION", px + width * 0.25f, hy, fontHeader, paint);
            canvas.DrawText("PRICE", px + width * 0.55f, hy, fontHeader, paint);
            string statusHdr = "STATUS";
            float sW = fontHeader.MeasureText(statusHdr);
            canvas.DrawText(statusHdr, rx - sW - 4, hy, fontHeader, paint);
            hy += 8;

            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(px, hy, rx, hy, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public float GetContentHeight()
    {
        return _contentHeight > 0 ? _contentHeight : 288; // 12 * 24
    }

    private static void DrawAlertSummaryCard(SKCanvas canvas, SKPaint paint, SKFont labelFont, SKFont valueFont,
        float x, float y, float w, string label, string value, SKColor valueColor)
    {
        float h = 32;
        paint.Color = new SKColor(20, 24, 32);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(new SKRect(x, y, x + w, y + h), 6, 6, paint);
        paint.Color = new SKColor(75, 80, 92);
        canvas.DrawText(label, x + 8, y + 12, labelFont, paint);
        paint.Color = valueColor;
        canvas.DrawText(value, x + 8, y + 26, valueFont, paint);
    }

    private static void DrawAlertRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, float width, ref float y,
        string pair, string condition, string price, string status, SKColor statusColor)
    {
        float rowH = 22;
        var rowRect = new SKRect(left, y, right, y + rowH);

        paint.Color = new SKColor(18, 22, 30);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(rowRect, 3, 3, paint);

        paint.Color = new SKColor(210, 215, 225);
        canvas.DrawText(pair, left + 4, y + 14, font, paint);

        paint.Color = new SKColor(150, 155, 165);
        canvas.DrawText(condition, left + width * 0.25f, y + 14, smallFont, paint);

        paint.Color = new SKColor(180, 185, 195);
        canvas.DrawText(price, left + width * 0.55f, y + 14, smallFont, paint);

        float badgeW = smallFont.MeasureText(status) + 10;
        float badgeX = right - badgeW - 4;
        float badgeY = y + 4;
        float badgeH = 14;
        paint.Color = new SKColor(statusColor.Red, statusColor.Green, statusColor.Blue, 30);
        canvas.DrawRoundRect(new SKRect(badgeX, badgeY, badgeX + badgeW, badgeY + badgeH), 3, 3, paint);
        paint.Color = statusColor;
        float statusTextW = smallFont.MeasureText(status);
        canvas.DrawText(status, badgeX + (badgeW - statusTextW) / 2, badgeY + 11, smallFont, paint);

        y += rowH + 2;
    }
}
