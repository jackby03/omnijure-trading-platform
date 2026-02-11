using SkiaSharp;
using System;

namespace Omnijure.Visual.Rendering;

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
    private int _latencyMs;
    private string _balance = "---";
    private int _openOrders;
    private int _openPositions;
    private string _spread = "---";
    private string _volume24h = "---";
    private string _marketStatus = "Open";
    private string _currentTime = "";

    public void UpdateFps(int fps) => _fps = fps;
    public void UpdateConnection(string status) => _connectionStatus = status;
    public void UpdateLatency(int ms) => _latencyMs = ms;
    public void UpdateBalance(string balance) => _balance = balance;
    public void UpdateOpenOrders(int count) => _openOrders = count;
    public void UpdateOpenPositions(int count) => _openPositions = count;
    public void UpdateSpread(string spread) => _spread = spread;
    public void UpdateVolume24h(string volume) => _volume24h = volume;
    public void UpdateMarketStatus(string status) => _marketStatus = status;

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        float y = screenHeight - Height;
        _currentTime = DateTime.Now.ToString("HH:mm:ss");

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
            var sepColor = new SKColor(0, 100, 180);

            // Exchange
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Lightning, leftX, iconY, iconSize, white);
            leftX += iconSize + 4;
            paint.Color = white;
            canvas.DrawText(_exchangeName, leftX, textY, font, paint);
            leftX += font.MeasureText(_exchangeName) + 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 8;

            // Connection status
            var statusColor = _connectionStatus == "Connected" 
                ? new SKColor(80, 250, 123) 
                : new SKColor(255, 85, 85);
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Dot, leftX, iconY, iconSize, statusColor);
            leftX += iconSize + 4;
            paint.Color = white;
            canvas.DrawText(_connectionStatus, leftX, textY, font, paint);
            leftX += font.MeasureText(_connectionStatus) + 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 8;

            // Latency
            paint.Color = _latencyMs < 50 ? new SKColor(80, 250, 123) 
                         : _latencyMs < 150 ? new SKColor(255, 200, 50) 
                         : new SKColor(255, 85, 85);
            string latencyText = $"{_latencyMs}ms";
            canvas.DrawText(latencyText, leftX, textY, font, paint);
            leftX += font.MeasureText(latencyText) + 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 8;

            // Balance
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Wallet, leftX, iconY, iconSize, white);
            leftX += iconSize + 4;
            paint.Color = white;
            string balanceText = $"${_balance}";
            canvas.DrawText(balanceText, leftX, textY, font, paint);
            leftX += font.MeasureText(balanceText) + 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 8;

            // Open orders
            paint.Color = _openOrders > 0 ? new SKColor(255, 200, 50) : new SKColor(180, 200, 220);
            string ordersText = $"Orders: {_openOrders}";
            canvas.DrawText(ordersText, leftX, textY, font, paint);
            leftX += font.MeasureText(ordersText) + 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 8;

            // Positions
            paint.Color = _openPositions > 0 ? new SKColor(80, 250, 123) : new SKColor(180, 200, 220);
            string posText = $"Positions: {_openPositions}";
            canvas.DrawText(posText, leftX, textY, font, paint);
            leftX += font.MeasureText(posText) + 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 8;

            // Spread
            paint.Color = new SKColor(180, 200, 220);
            string spreadText = $"Spread: {_spread}";
            canvas.DrawText(spreadText, leftX, textY, font, paint);

            // === RIGHT SIDE ===
            float rightX = screenWidth - 10;

            // Time
            float timeW = font.MeasureText(_currentTime);
            rightX -= timeW;
            paint.Color = white;
            canvas.DrawText(_currentTime, rightX, textY, font, paint);
            rightX -= 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(rightX, y + 4, rightX, y + Height - 4, paint);
            rightX -= 8;

            // FPS
            string fpsText = $"{_fps} FPS";
            float fpsW = font.MeasureText(fpsText);
            rightX -= fpsW;
            paint.Color = _fps >= 55 ? white : new SKColor(255, 200, 50);
            canvas.DrawText(fpsText, rightX, textY, font, paint);
            rightX -= 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(rightX, y + 4, rightX, y + Height - 4, paint);
            rightX -= 8;

            // 24h Volume
            string volText = $"Vol: {_volume24h}";
            float volW = font.MeasureText(volText);
            rightX -= volW;
            paint.Color = new SKColor(180, 200, 220);
            canvas.DrawText(volText, rightX, textY, font, paint);
            rightX -= 12;

            // Separator
            paint.Color = sepColor;
            canvas.DrawLine(rightX, y + 4, rightX, y + Height - 4, paint);
            rightX -= 8;

            // Platform
            string platformText = ".NET 9";
            float platformW = font.MeasureText(platformText);
            rightX -= platformW;
            paint.Color = new SKColor(180, 200, 220);
            canvas.DrawText(platformText, rightX, textY, font, paint);
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }
}
