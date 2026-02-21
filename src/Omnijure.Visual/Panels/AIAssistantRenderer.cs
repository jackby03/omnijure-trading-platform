using System;
using System.Collections.Generic;
using SkiaSharp;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Panels;

public class AIAssistantRenderer : IPanelRenderer
{
    public string PanelId => PanelDefinitions.AI_ASSISTANT;
    private float _aiChatContentHeight = 600;

    public void Render(SKCanvas canvas, SKRect rect, float scrollY)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            using var fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 13);
            using var fontNormal = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            using var fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);
            using var fontCode = new SKFont(SKTypeface.FromFamilyName("Cascadia Code", SKFontStyle.Normal), 10);

            paint.IsAntialias = true;
            float pad = 10;
            float headerH = 48;
            float inputH = 52;
            float width = rect.Width;
            float height = rect.Height;

            float scrollZoneTop = headerH;
            float scrollZoneBottom = height - inputH;
            float scrollZoneH = scrollZoneBottom - scrollZoneTop;

            // ========================================
            // STEP 1: Chat bubbles (clipped to scroll zone, translated by -scrollY)
            // ========================================
            canvas.Save();
            canvas.ClipRect(new SKRect(0, scrollZoneTop, width, scrollZoneBottom));
            canvas.Translate(0, -scrollY);

            paint.Style = SKPaintStyle.Fill;
            float y = headerH + 8;

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "What pattern is forming on BTCUSDT 1h?", pad, "2h ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "I see a bullish flag forming on BTC/USDT 1h chart. The price consolidated between $68,800-$69,100 after a strong impulse move.\n\n> Entry: Break above $69,100\n> Target: $69,800 (+1.0%)\n> Stop: $68,600 (-0.7%)\n> R:R ratio: 1.43",
                pad, "2h ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Run RSI divergence scan on top 10 pairs", pad, "1h 45m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Scanning 10 pairs for RSI divergence...\n\n[+] ETHUSDT - Bullish divergence (RSI 34)\n[+] SOLUSDT - Bullish divergence (RSI 29)\n[!] BNBUSDT - Neutral (RSI 48)\n[-] XRPUSDT - Bearish divergence (RSI 72)",
                pad, "1h 44m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Set up a grid bot for ETH between $3,200-$3,600", pad, "1h 20m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Grid bot configured:\n\n> Pair: ETHUSDT\n> Range: $3,200 - $3,600\n> Grid levels: 20\n> Investment: $500 USDT\n> Grid spacing: $20 (0.56%)\n\nEstimated profit per grid: $1.12\nAnnualized return: ~18-24% in range\n\nReady to deploy. Type 'confirm' to start.",
                pad, "1h 19m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Show me the top gainers in the last 4h", pad, "48m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Top gainers (4h):\n\n[+] PEPEUSDT  +8.42%  Vol: $124M\n[+] SUIUSDT   +5.17%  Vol: $89M\n[+] FETUSDT   +4.83%  Vol: $67M\n[+] INJUSDT   +3.91%  Vol: $52M\n[+] ARBUSDT   +3.24%  Vol: $41M\n\nPEPE shows strong momentum with volume breakout above 20-day average.",
                pad, "47m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "What's the current market sentiment?", pad, "12m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Market Sentiment Analysis:\n\nFear & Greed Index: 72 (Greed)\nBTC Dominance: 54.2% (+0.3%)\nTotal Market Cap: $2.48T\n\n[+] Funding rates positive but moderate\n[+] Open interest rising steadily\n[!] Whale accumulation detected on BTC\n[-] Some altcoins showing exhaustion\n\nOverall: Cautiously bullish. Consider trailing stops on long positions.",
                pad, "11m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, true, "Backtest a simple EMA crossover on SOL", pad, "3m ago");

            y = DrawChatBubble(canvas, paint, fontNormal, fontSmall, width,
                y, false,
                "Backtest: EMA 9/21 Crossover on SOLUSDT\nPeriod: Last 30 days (1h candles)\n\nResults:\n> Total trades: 14\n> Win rate: 64.3%\n> Profit factor: 1.82\n> Max drawdown: -3.4%\n> Net return: +8.7%\n> Sharpe ratio: 1.45\n\nThe strategy performs well in trending markets but suffers during consolidation. Consider adding a volatility filter.",
                pad, "2m ago");

            // Total height of all bubbles content (from headerH to final y)
            float bubblesContentH = y + 8 - headerH;
            _aiChatContentHeight = bubblesContentH;

            canvas.Restore(); // restore scroll clip

            // ========================================
            // STEP 2: Fixed header (drawn OVER bubbles, no scroll)
            // ========================================
            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, 0, width, headerH, paint);

            paint.Color = new SKColor(70, 140, 255);
            canvas.DrawText("Omnijure AI", 12, 18, fontBold, paint);

            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Star,
                fontBold.MeasureText("Omnijure AI") + 16, 6, 12, new SKColor(70, 140, 255));

            paint.Color = new SKColor(70, 75, 85);
            canvas.DrawText("GPT-4o  |  Connected", 12, 34, fontSmall, paint);

            paint.Color = new SKColor(46, 204, 113);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawCircle(width - 16, 26, 4, paint);

            // Separator
            paint.Color = new SKColor(35, 40, 50);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(8, headerH - 6, width - 8, headerH - 6, paint);

            // ========================================
            // STEP 3: Fixed input box (drawn OVER bubbles, no scroll)
            // ========================================
            float inputBoxH = 34;
            float inputPad = 8;
            float inputAreaTop = height - inputH;

            paint.Color = new SKColor(18, 20, 24);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, inputAreaTop, width, inputH, paint);

            paint.Color = new SKColor(28, 32, 40);
            var inputRect = new SKRect(inputPad, inputAreaTop + 8, width - inputPad, inputAreaTop + 8 + inputBoxH);
            canvas.DrawRoundRect(inputRect, 8, 8, paint);

            paint.Color = new SKColor(40, 45, 55);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawRoundRect(inputRect, 8, 8, paint);

            paint.Style = SKPaintStyle.Fill;
            paint.Color = new SKColor(80, 85, 95);
            canvas.DrawText("Ask about patterns, strategies, or scan markets...", 16, inputAreaTop + 8 + inputBoxH / 2 + 4, fontSmall, paint);

            // Send button
            float btnH = inputBoxH - 8;
            float btnW = 28;
            float btnX = width - inputPad - btnW - 4;
            float btnY = inputAreaTop + 8 + 4;
            paint.Color = new SKColor(56, 139, 253);
            canvas.DrawRoundRect(new SKRect(btnX, btnY, btnX + btnW, btnY + btnH), 4, 4, paint);
            paint.Color = SKColors.White;
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 2;
            paint.StrokeCap = SKStrokeCap.Round;
            float sendCy = btnY + btnH / 2;
            canvas.DrawLine(btnX + 7, sendCy, btnX + btnW - 7, sendCy, paint);
            canvas.DrawLine(btnX + btnW - 12, sendCy - 5, btnX + btnW - 7, sendCy, paint);
            canvas.DrawLine(btnX + btnW - 12, sendCy + 5, btnX + btnW - 7, sendCy, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    public float GetContentHeight()
    {
        return _aiChatContentHeight;
    }

    private float DrawChatBubble(SKCanvas canvas, SKPaint paint, SKFont font, SKFont timeFont,
        float panelW, float y, bool isUser, string text, float pad, string timestamp = "just now")
    {
        float bubbleMaxW = panelW - pad * 2 - 20;
        float bubbleX = isUser ? pad + 20 : pad;
        float lineH = 15;

        var lines = new List<string>();
        foreach (var segment in text.Split('\n'))
        {
            if (string.IsNullOrEmpty(segment)) { lines.Add(""); continue; }
            string remaining = segment;
            while (remaining.Length > 0)
            {
                float w = font.MeasureText(remaining);
                if (w <= bubbleMaxW - 20) { lines.Add(remaining); break; }
                int cut = (int)(remaining.Length * (bubbleMaxW - 20) / w);
                if (cut >= remaining.Length) cut = remaining.Length - 1;
                int space = remaining.LastIndexOf(' ', cut);
                if (space > 0) cut = space;
                lines.Add(remaining[..cut]);
                remaining = remaining[cut..].TrimStart();
            }
        }

        float bubbleH = lines.Count * lineH + 16;
        float bubbleW = bubbleMaxW;
        if (isUser)
        {
            float maxLineW = 0;
            foreach (var l in lines) { float lw = font.MeasureText(l); if (lw > maxLineW) maxLineW = lw; }
            bubbleW = Math.Min(maxLineW + 24, bubbleMaxW);
            bubbleX = panelW - pad - bubbleW;
        }

        var bubbleRect = new SKRect(bubbleX, y, bubbleX + bubbleW, y + bubbleH);

        paint.Style = SKPaintStyle.Fill;
        paint.Color = isUser ? new SKColor(45, 75, 140) : new SKColor(28, 32, 40);
        canvas.DrawRoundRect(bubbleRect, 8, 8, paint);

        paint.Color = isUser ? new SKColor(220, 230, 245) : new SKColor(195, 200, 210);
        float textY = y + 13;
        foreach (var line in lines)
        {
            if (line.Length > 0)
            {
                if (!isUser && (line.StartsWith("[+]") || line.StartsWith(">")))
                    paint.Color = new SKColor(170, 210, 180);
                else if (!isUser && line.StartsWith("[!]"))
                    paint.Color = new SKColor(220, 200, 130);
                else if (!isUser && line.StartsWith("[-]"))
                    paint.Color = new SKColor(220, 150, 150);
                else
                    paint.Color = isUser ? new SKColor(220, 230, 245) : new SKColor(195, 200, 210);

                canvas.DrawText(line, bubbleX + 10, textY, font, paint);
            }
            textY += lineH;
        }

        paint.Color = new SKColor(70, 75, 85);
        string time = isUser ? $"You \u2022 {timestamp}" : $"AI \u2022 {timestamp}";
        float tw = timeFont.MeasureText(time);
        float timeX = isUser ? bubbleX + bubbleW - tw - 4 : bubbleX + 4;
        canvas.DrawText(time, timeX, y + bubbleH + 12, timeFont, paint);

        return y + bubbleH + 24;
    }
}
