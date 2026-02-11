using SkiaSharp;

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

    public void UpdateFps(int fps) => _fps = fps;
    public void UpdateConnection(string status) => _connectionStatus = status;
    public void UpdateLatency(int ms) => _latencyMs = ms;
    public void UpdateBalance(string balance) => _balance = balance;

    public void Render(SKCanvas canvas, float screenWidth, float screenHeight)
    {
        float y = screenHeight - Height;

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

            // Exchange icon + name
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Lightning, 
                leftX, iconY, iconSize, white);
            leftX += iconSize + 4;

            paint.Color = white;
            canvas.DrawText(_exchangeName, leftX, textY, font, paint);
            leftX += font.MeasureText(_exchangeName) + 16;

            // Separator
            paint.Color = new SKColor(0, 100, 180);
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 10;

            // Connection status dot + text
            var statusColor = _connectionStatus == "Connected" 
                ? new SKColor(80, 250, 123) 
                : new SKColor(255, 85, 85);
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Dot, 
                leftX, iconY, iconSize, statusColor);
            leftX += iconSize + 4;

            paint.Color = white;
            canvas.DrawText(_connectionStatus, leftX, textY, font, paint);
            leftX += font.MeasureText(_connectionStatus) + 12;

            // Separator
            paint.Color = new SKColor(0, 100, 180);
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 10;

            // Latency
            string latencyText = $"{_latencyMs}ms";
            paint.Color = _latencyMs < 100 ? new SKColor(80, 250, 123) : new SKColor(255, 200, 50);
            canvas.DrawText(latencyText, leftX, textY, font, paint);
            leftX += font.MeasureText(latencyText) + 12;

            // Separator
            paint.Color = new SKColor(0, 100, 180);
            canvas.DrawLine(leftX, y + 4, leftX, y + Height - 4, paint);
            leftX += 10;

            // Balance
            string balanceText = $"Balance: ${_balance}";
            paint.Color = white;
            canvas.DrawText(balanceText, leftX, textY, font, paint);

            // Right side
            float rightX = screenWidth - 10;

            // FPS
            string fpsText = $"FPS: {_fps}";
            float fpsW = font.MeasureText(fpsText);
            rightX -= fpsW;
            paint.Color = white;
            canvas.DrawText(fpsText, rightX, textY, font, paint);
            rightX -= 16;

            // Separator
            paint.Color = new SKColor(0, 100, 180);
            canvas.DrawLine(rightX, y + 4, rightX, y + Height - 4, paint);
            rightX -= 10;

            // Platform info
            string platformText = ".NET 9";
            float platformW = font.MeasureText(platformText);
            rightX -= platformW;
            paint.Color = new SKColor(200, 210, 220);
            canvas.DrawText(platformText, rightX, textY, font, paint);
            rightX -= 4;

            // Info icon
            rightX -= iconSize;
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Info, 
                rightX, iconY, iconSize, new SKColor(200, 210, 220));
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }
}
