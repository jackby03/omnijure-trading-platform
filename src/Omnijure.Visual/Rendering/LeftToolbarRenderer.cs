using SkiaSharp;
using Omnijure.Visual.Drawing;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Renders the left toolbar with drawing tools (TradingView-style)
/// </summary>
public class LeftToolbarRenderer
{
    public const float ToolbarWidth = 36;
    private const float IconSize = 16;
    private const float ButtonSize = 30;
    private const float ButtonSpacing = 2;

    private readonly SKPaint _bgPaint;
    private readonly SKPaint _btnDefault;
    private readonly SKPaint _btnHover;
    private readonly SKPaint _btnActive;
    private readonly SKPaint _separatorPaint;

    // Tool configuration: (DrawingTool, Icon, Tooltip)
    private readonly (DrawingTool Tool, SvgIconRenderer.Icon Icon, string Tooltip)[] _tools;

    public LeftToolbarRenderer()
    {
        _bgPaint = new SKPaint { Color = ThemeManager.Surface, Style = SKPaintStyle.Fill };
        _btnDefault = new SKPaint { Color = ThemeManager.ButtonDefault, Style = SKPaintStyle.Fill };
        _btnHover = new SKPaint { Color = ThemeManager.ButtonHover, Style = SKPaintStyle.Fill };
        _btnActive = new SKPaint { Color = ThemeManager.ButtonActive, Style = SKPaintStyle.Fill };
        _separatorPaint = new SKPaint { Color = ThemeManager.Divider, StrokeWidth = 1 };

        // Define available tools in order
        _tools = new[]
        {
            (DrawingTool.None, SvgIconRenderer.Icon.Cursor, "Cursor (Esc)"),
            (DrawingTool.TrendLine, SvgIconRenderer.Icon.TrendLine, "Trend Line (T)"),
            (DrawingTool.HorizontalLine, SvgIconRenderer.Icon.HorizontalLine, "Horizontal Line (H)"),
            (DrawingTool.VerticalLine, SvgIconRenderer.Icon.VerticalLine, "Vertical Line (V)"),
            (DrawingTool.Rectangle, SvgIconRenderer.Icon.Rectangle, "Rectangle (R)"),
            (DrawingTool.Fibonacci, SvgIconRenderer.Icon.Fibonacci, "Fibonacci Retracement (F)"),
        };
    }

    /// <summary>
    /// Renders the left toolbar
    /// </summary>
    /// <param name="canvas">Canvas to draw on</param>
    /// <param name="height">Height of the toolbar (full chart height)</param>
    /// <param name="activeTool">Currently active drawing tool</param>
    /// <param name="mouseX">Mouse X position (for hover detection)</param>
    /// <param name="mouseY">Mouse Y position (for hover detection)</param>
    public void Render(SKCanvas canvas, float height, DrawingTool activeTool, float mouseX, float mouseY)
    {
        // Background
        canvas.DrawRect(0, 0, ToolbarWidth, height, _bgPaint);

        // Separator line on right edge
        canvas.DrawLine(ToolbarWidth - 1, 0, ToolbarWidth - 1, height, _separatorPaint);

        // Draw tool buttons
        float y = 4;

        for (int i = 0; i < _tools.Length; i++)
        {
            var (tool, icon, tooltip) = _tools[i];

            bool isActive = activeTool == tool;
            bool isHovered = IsButtonHovered(mouseX, mouseY, y);

            // Button background
            SKRect btnRect = new SKRect(3, y, ToolbarWidth - 3, y + ButtonSize);

            SKPaint btnPaint = isActive ? _btnActive : (isHovered ? _btnHover : _btnDefault);
            canvas.DrawRoundRect(btnRect, ThemeManager.BorderRadius, ThemeManager.BorderRadius, btnPaint);

            // Icon (SVG-based for crisp rendering)
            SKColor iconColor = isActive ? ThemeManager.TextWhite : ThemeManager.TextSecondary;
            SvgIconRenderer.DrawIconCentered(canvas, icon, btnRect, IconSize, iconColor);

            y += ButtonSize + ButtonSpacing;

            // Add separator after cursor tool
            if (i == 0)
            {
                y += 4;
                canvas.DrawLine(6, y, ToolbarWidth - 6, y, _separatorPaint);
                y += 8;
            }
        }
    }

    /// <summary>
    /// Gets the tool button bounds for a given index
    /// </summary>
    public SKRect GetButtonRect(int index)
    {
        float y = 4;

        for (int i = 0; i < index; i++)
        {
            y += ButtonSize + ButtonSpacing;
            if (i == 0) y += 16; // Separator space
        }

        return new SKRect(3, y, ToolbarWidth - 3, y + ButtonSize);
    }

    /// <summary>
    /// Checks if a point is hovering over a button at the given y position
    /// </summary>
    private bool IsButtonHovered(float mouseX, float mouseY, float buttonY)
    {
        return mouseX >= 3 && mouseX <= ToolbarWidth - 3 &&
               mouseY >= buttonY && mouseY <= buttonY + ButtonSize;
    }

    /// <summary>
    /// Gets the tool at a given mouse position, or null if none
    /// </summary>
    public DrawingTool? GetToolAtPosition(float mouseX, float mouseY)
    {
        if (mouseX < 0 || mouseX > ToolbarWidth) return null;

        float y = 8;

        for (int i = 0; i < _tools.Length; i++)
        {
            if (IsButtonHovered(mouseX, mouseY, y))
            {
                return _tools[i].Tool;
            }

            y += ButtonSize + ButtonSpacing;
            if (i == 0) y += 16; // Separator space
        }

        return null;
    }

    /// <summary>
    /// Checks if a point is within the toolbar bounds
    /// </summary>
    public bool Contains(float x, float y)
    {
        return x >= 0 && x <= ToolbarWidth;
    }
}
