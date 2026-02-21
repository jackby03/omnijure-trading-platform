using System;
using SkiaSharp;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Widgets.Panels;

public class PortfolioRenderer : IPanelRenderer
{
    public string PanelId => PanelDefinitions.PORTFOLIO;
    private float _contentHeight = 0;

    public void Render(SKCanvas canvas, SKRect rect, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontSection = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 9);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 9);
            using var fontBalance = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 20);

            paint.IsAntialias = true;
            
            float width = rect.Width;
            float y = 4;
            float px = 8;
            float rx = width - px;

            // Total Balance
            paint.Color = new SKColor(85, 90, 100);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawText("TOTAL BALANCE", px, y + 10, fontSmall, paint);
            y += 18;

            paint.Color = new SKColor(235, 240, 250);
            canvas.DrawText("$12,847.53", px, y + 16, fontBalance, paint);
            y += 22;

            paint.Color = new SKColor(46, 204, 113);
            canvas.DrawText("+$342.18 (2.73%)", px, y + 10, fontSmall, paint);
            y += 18;

            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Accounts
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "ACCOUNTS");

            DrawAccountRow(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Binance Spot", "$8,240.12", true);
            DrawAccountRow(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Binance Futures", "$4,607.41", true);

            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Active Bots
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "ACTIVE BOTS");

            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Grid Bot #1", "BTCUSDT", "+3.42%", true);
            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "DCA Bot #2", "ETHUSDT", "+1.87%", true);
            DrawBotCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "Scalper #3", "SOLUSDT", "-0.24%", false);

            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Holdings
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "HOLDINGS");

            paint.Color = new SKColor(70, 75, 85);
            canvas.DrawText("ASSET", px, y, fontSmall, paint);
            string hdrVal = "VALUE";
            float hdrValW = fontSmall.MeasureText(hdrVal);
            canvas.DrawText(hdrVal, rx - hdrValW, y, fontSmall, paint);
            y += 10;

            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "BTC", "0.0842 BTC", "$5,804.21", "+2.1%", true);
            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "ETH", "1.245 ETH", "$4,318.90", "+3.4%", true);
            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "SOL", "12.50 SOL", "$1,912.50", "-0.8%", false);
            DrawHoldingCard(canvas, paint, fontNormal, fontSmall, px, rx, ref y,
                "USDT", "811.92 USDT", "$811.92", "0.0%", true);

            y += 4;
            DrawSectionDivider(canvas, paint, px, rx, ref y);

            // Datasets
            DrawSectionHeader(canvas, paint, fontSection, px, ref y, "DATASETS");

            DrawDatasetRow(canvas, paint, fontNormal, fontSmall, px, ref y,
                "BTCUSDT 1m", "30 days \u2022 43,200 candles");
            DrawDatasetRow(canvas, paint, fontNormal, fontSmall, px, ref y,
                "ETHUSDT 5m", "90 days \u2022 25,920 candles");

            _contentHeight = y;
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public float GetContentHeight()
    {
        return _contentHeight > 0 ? _contentHeight : 665;
    }

    private static void DrawSectionDivider(SKCanvas canvas, SKPaint paint, float left, float right, ref float y)
    {
        paint.Color = new SKColor(35, 40, 50);
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        canvas.DrawLine(left, y, right, y, paint);
        paint.Style = SKPaintStyle.Fill;
        y += 10;
    }

    private static void DrawSectionHeader(SKCanvas canvas, SKPaint paint, SKFont font, float x, ref float y, string label)
    {
        paint.Color = new SKColor(90, 96, 108);
        canvas.DrawText(label, x, y + 2, font, paint);
        y += 16;
    }

    private static void DrawAccountRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, ref float y, string name, string balance, bool connected)
    {
        paint.Color = connected ? new SKColor(46, 204, 113) : new SKColor(120, 125, 135);
        canvas.DrawCircle(left + 5, y + 1, 3.5f, paint);

        paint.Color = new SKColor(195, 200, 210);
        canvas.DrawText(name, left + 14, y + 5, font, paint);

        paint.Color = new SKColor(140, 145, 155);
        float bw = smallFont.MeasureText(balance);
        canvas.DrawText(balance, right - bw, y + 5, smallFont, paint);

        y += 20;
    }

    private static void DrawBotCard(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, ref float y, string name, string pair, string pnl, bool isPositive)
    {
        float cardH = 32;
        var cardRect = new SKRect(left, y, right, y + cardH);

        paint.Color = new SKColor(22, 26, 34);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(cardRect, 6, 6, paint);

        paint.Color = new SKColor(46, 204, 113);
        canvas.DrawCircle(left + 10, y + cardH / 2, 3, paint);

        paint.Color = new SKColor(200, 205, 215);
        canvas.DrawText(name, left + 20, y + 13, font, paint);

        paint.Color = new SKColor(85, 90, 100);
        canvas.DrawText(pair, left + 20, y + 26, smallFont, paint);

        paint.Color = isPositive ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
        float pw = font.MeasureText(pnl);
        canvas.DrawText(pnl, right - pw - 8, y + 20, font, paint);

        y += cardH + 4;
    }

    private static void DrawHoldingCard(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, float right, ref float y, string coin, string amount, string value, string change, bool isPositive)
    {
        float rowH = 28;
        var rowRect = new SKRect(left, y, right, y + rowH);

        paint.Color = new SKColor(20, 24, 32);
        paint.Style = SKPaintStyle.Fill;
        canvas.DrawRoundRect(rowRect, 4, 4, paint);

        paint.Color = new SKColor(210, 215, 225);
        canvas.DrawText(coin, left + 8, y + 12, font, paint);

        paint.Color = new SKColor(80, 85, 95);
        canvas.DrawText(amount, left + 8, y + 24, smallFont, paint);

        paint.Color = new SKColor(190, 195, 205);
        float vw = smallFont.MeasureText(value);
        canvas.DrawText(value, right - vw - 6, y + 12, smallFont, paint);

        paint.Color = isPositive ? new SKColor(46, 204, 113) : new SKColor(239, 83, 80);
        float cw = smallFont.MeasureText(change);
        canvas.DrawText(change, right - cw - 6, y + 24, smallFont, paint);

        y += rowH + 3;
    }

    private static void DrawDatasetRow(SKCanvas canvas, SKPaint paint, SKFont font, SKFont smallFont,
        float left, ref float y, string name, string details)
    {
        paint.Color = new SKColor(56, 139, 253);
        canvas.DrawCircle(left + 4, y + 2, 3, paint);

        paint.Color = new SKColor(185, 190, 200);
        canvas.DrawText(name, left + 14, y + 6, font, paint);
        y += 16;

        paint.Color = new SKColor(75, 80, 90);
        canvas.DrawText(details, left + 14, y, smallFont, paint);
        y += 16;
    }
}
