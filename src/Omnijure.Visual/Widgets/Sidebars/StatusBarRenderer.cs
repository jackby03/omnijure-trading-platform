using SkiaSharp;
using System;

namespace Omnijure.Visual.Widgets.Sidebars;

/// <summary>
/// Barra de estado inferior (como VS Code / Visual Studio).
/// Muestra información de estado, conexión, FPS, etc.
/// </summary>
public class StatusBarRenderer
{
    public const float Height = 24f;

    private int _fps;
    private string _connectionStatus = "Connected";
    private string _exchangeName = "Binance";
    private int _latencyMs = 23;
    private string _balance = "12,847.53";
    private int _openOrders = 3;
    private int _openPositions = 4;
    private string _spread = "0.01%";
    private string _volume24h = "$1.84B";
    private string _marketStatus = "Open";
    private string _currentTime = "";

    // Mock data adicional
    private int _activeBots = 3;
    private string _pnlToday = "+$342.18";
    private bool _pnlPositive = true;
    private string _cpuUsage = "12%";
    private string _memUsage = "248 MB";
    private int _wsMessages = 1_247;
    private string _btcDominance = "54.2%";
    private string _fundingRate = "+0.012%";
    private int _alertsActive = 7;
    private string _scriptStatus = "core.clj";
    private bool _scriptRunning = true;

    public void UpdateFps(int fps) => _fps = fps;
    public void UpdateConnection(string status) => _connectionStatus = status;
    public void UpdateLatency(int ms) => _latencyMs = ms;
    public void UpdateBalance(string balance) => _balance = balance;
    public void UpdateOpenOrders(int count) => _openOrders = count;
    public void UpdateOpenPositions(int count) => _openPositions = count;
    public void UpdateSpread(string spread) => _spread = spread;
    public void UpdateVolume24h(string volume) => _volume24h = volume;
    public void UpdateMarketStatus(string status) => _marketStatus = status;
    public void UpdateActiveBots(int count) => _activeBots = count;
    public void UpdatePnlToday(string pnl, bool positive) { _pnlToday = pnl; _pnlPositive = positive; }
    public void UpdateCpuUsage(string usage) => _cpuUsage = usage;
    public void UpdateMemUsage(string usage) => _memUsage = usage;
    public void UpdateWsMessages(int count) => _wsMessages = count;
    public void UpdateBtcDominance(string dom) => _btcDominance = dom;
    public void UpdateFundingRate(string rate) => _fundingRate = rate;
    public void UpdateAlertsActive(int count) => _alertsActive = count;
    public void UpdateScriptStatus(string name, bool running) { _scriptStatus = name; _scriptRunning = running; }

    // Simulated fluctuation for mock data realism
    private int _frameCounter;
    private readonly Random _rng = new();

    private void TickMockData()
    {
        _frameCounter++;
        if (_frameCounter % 60 == 0) // ~once per second at 60 FPS
        {
            _latencyMs = Math.Clamp(_latencyMs + _rng.Next(-5, 6), 8, 85);
            _wsMessages += _rng.Next(10, 40);
        }
    }

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        float y = screenHeight - Height;
        _currentTime = DateTime.UtcNow.ToString("HH:mm:ss") + " UTC";
        TickMockData();

        var paint = PaintPool.Instance.Rent();
        try
        {
            // Background
            paint.Color = new SKColor(0, 122, 204);
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRect(0, y, screenWidth, Height, paint);

            // Top separator
            paint.Color = new SKColor(0, 100, 180);
            paint.Style = SKPaintStyle.Stroke;
            paint.StrokeWidth = 1;
            canvas.DrawLine(0, y, screenWidth, y, paint);

            using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
            paint.Style = SKPaintStyle.Fill;
            paint.IsAntialias = true;

            float textY = y + 16;
            float leftX = 8;
            float iconY = y + 4;
            float iconSize = 14f;
            var white = new SKColor(255, 255, 255);
            var dimWhite = new SKColor(200, 215, 230);
            var sepColor = new SKColor(0, 100, 180);
            var green = new SKColor(80, 250, 123);
            var yellow = new SKColor(255, 200, 50);
            var red = new SKColor(255, 85, 85);

            // ===================== LEFT SIDE =====================

            // Exchange
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Lightning, leftX, iconY, iconSize, white);
            leftX += iconSize + 4;
            paint.Color = white;
            canvas.DrawText(_exchangeName, leftX, textY, font, paint);
            leftX += font.MeasureText(_exchangeName) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Connection status
            var statusColor = _connectionStatus == "Connected" ? green : red;
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Dot, leftX, iconY, iconSize, statusColor);
            leftX += iconSize + 4;
            paint.Color = white;
            canvas.DrawText(_connectionStatus, leftX, textY, font, paint);
            leftX += font.MeasureText(_connectionStatus) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Latency
            paint.Color = _latencyMs < 50 ? green : _latencyMs < 150 ? yellow : red;
            string latencyText = $"{_latencyMs}ms";
            canvas.DrawText(latencyText, leftX, textY, font, paint);
            leftX += font.MeasureText(latencyText) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Balance
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Wallet, leftX, iconY, iconSize, white);
            leftX += iconSize + 4;
            paint.Color = white;
            string balanceText = $"${_balance}";
            canvas.DrawText(balanceText, leftX, textY, font, paint);
            leftX += font.MeasureText(balanceText) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // PnL Today
            paint.Color = _pnlPositive ? green : red;
            string pnlText = $"P&L: {_pnlToday}";
            canvas.DrawText(pnlText, leftX, textY, font, paint);
            leftX += font.MeasureText(pnlText) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Open orders
            paint.Color = _openOrders > 0 ? yellow : dimWhite;
            string ordersText = $"Orders: {_openOrders}";
            canvas.DrawText(ordersText, leftX, textY, font, paint);
            leftX += font.MeasureText(ordersText) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Positions
            paint.Color = _openPositions > 0 ? green : dimWhite;
            string posText = $"Positions: {_openPositions}";
            canvas.DrawText(posText, leftX, textY, font, paint);
            leftX += font.MeasureText(posText) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Active Bots
            paint.Color = _activeBots > 0 ? new SKColor(130, 200, 255) : dimWhite;
            string botsText = $"Bots: {_activeBots}";
            canvas.DrawText(botsText, leftX, textY, font, paint);
            leftX += font.MeasureText(botsText) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Spread
            paint.Color = dimWhite;
            string spreadText = $"Spread: {_spread}";
            canvas.DrawText(spreadText, leftX, textY, font, paint);
            leftX += font.MeasureText(spreadText) + 10;
            DrawSep(canvas, paint, sepColor, leftX, y);
            leftX += 8;

            // Funding Rate
            bool fundingPositive = _fundingRate.StartsWith("+");
            paint.Color = fundingPositive ? green : red;
            string fundText = $"Fund: {_fundingRate}";
            canvas.DrawText(fundText, leftX, textY, font, paint);

            // ===================== RIGHT SIDE =====================
            float rightX = screenWidth - 10;

            // Time (UTC)
            float timeW = font.MeasureText(_currentTime);
            rightX -= timeW;
            paint.Color = white;
            canvas.DrawText(_currentTime, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // FPS
            string fpsText = $"{_fps} FPS";
            float fpsW = font.MeasureText(fpsText);
            rightX -= fpsW;
            paint.Color = _fps >= 55 ? white : yellow;
            canvas.DrawText(fpsText, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // CPU / Memory
            string perfText = $"CPU {_cpuUsage} | {_memUsage}";
            float perfW = font.MeasureText(perfText);
            rightX -= perfW;
            paint.Color = dimWhite;
            canvas.DrawText(perfText, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // WS Messages counter
            string wsText = $"WS: {_wsMessages:N0}";
            float wsW = font.MeasureText(wsText);
            rightX -= wsW;
            paint.Color = new SKColor(130, 200, 255);
            canvas.DrawText(wsText, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // 24h Volume
            string volText = $"Vol 24h: {_volume24h}";
            float volW = font.MeasureText(volText);
            rightX -= volW;
            paint.Color = dimWhite;
            canvas.DrawText(volText, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // BTC Dominance
            string domText = $"BTC.D: {_btcDominance}";
            float domW = font.MeasureText(domText);
            rightX -= domW;
            paint.Color = new SKColor(255, 180, 50);
            canvas.DrawText(domText, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // Alerts active
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Bell, rightX - iconSize, iconY, iconSize,
                _alertsActive > 0 ? yellow : dimWhite);
            rightX -= iconSize + 4;
            string alertText = $"{_alertsActive}";
            float alertW = font.MeasureText(alertText);
            rightX -= alertW;
            paint.Color = _alertsActive > 0 ? yellow : dimWhite;
            canvas.DrawText(alertText, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // Script status
            paint.Color = _scriptRunning ? green : red;
            canvas.DrawCircle(rightX - 2, y + Height / 2, 3, paint);
            rightX -= 10;
            string scriptText = _scriptStatus;
            float scriptW = font.MeasureText(scriptText);
            rightX -= scriptW;
            paint.Color = dimWhite;
            canvas.DrawText(scriptText, rightX, textY, font, paint);
            rightX -= 10;
            DrawSep(canvas, paint, sepColor, rightX, y);
            rightX -= 8;

            // Platform
            string platformText = ".NET 9 | GPU";
            float platformW = font.MeasureText(platformText);
            rightX -= platformW;
            paint.Color = new SKColor(160, 175, 195);
            canvas.DrawText(platformText, rightX, textY, font, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private static void DrawSep(SKCanvas canvas, SKPaint paint, SKColor color, float x, float y)
    {
        paint.Color = color;
        paint.Style = SKPaintStyle.Stroke;
        paint.StrokeWidth = 1;
        canvas.DrawLine(x, y + 4, x, y + Height - 4, paint);
        paint.Style = SKPaintStyle.Fill;
    }
}
