using SkiaSharp;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Icon rendering system using Unicode symbols for TradingView-style UI.
/// Provides consistent icon rendering across the application.
/// </summary>
public static class IconRenderer
{
    /// <summary>
    /// Available icons for the UI
    /// </summary>
    public enum Icon
    {
        // Navigation & Cursor
        Cursor,         // ⬆ Arrow/cursor
        Hand,           // ✋ Hand tool

        // Drawing Tools
        TrendLine,      // ╱ Trend line
        HorizontalLine, // ─ Horizontal line
        VerticalLine,   // │ Vertical line
        Rectangle,      // ▭ Rectangle
        Circle,         // ○ Circle
        Triangle,       // △ Triangle

        // Chart Tools
        ChartCandles,   // ▉ Candles
        ChartLine,      // ╱ Line chart
        ChartArea,      // ▨ Area chart
        ChartBars,      // ▌ Bar chart

        // Indicators
        Indicators,     // 📊 Indicators
        Oscillator,     // 〰 Oscillator/wave
        MovingAverage, // ⚌ Moving average lines

        // UI Controls
        Settings,       // ⚙ Settings gear
        Search,         // 🔍 Search magnifier
        ZoomIn,         // 🔍+ Zoom in
        ZoomOut,        // 🔍- Zoom out
        Screenshot,     // 📷 Screenshot/camera
        Fullscreen,     // ⛶ Fullscreen

        // Time
        Timeframe,      // 🕐 Clock
        Calendar,       // 📅 Calendar

        // Actions
        Play,           // ▶ Play
        Pause,          // ⏸ Pause
        Stop,           // ⏹ Stop
        Delete,         // 🗑 Delete/trash
        Save,           // 💾 Save/floppy
        Load,           // 📂 Load/folder

        // Status
        Check,          // ✓ Checkmark
        Cross,          // ✗ X/cross
        Warning,        // ⚠ Warning triangle
        Info,           // ℹ Information

        // Arrows
        ArrowUp,        // ▲ Up arrow
        ArrowDown,      // ▼ Down arrow
        ArrowLeft,      // ◀ Left arrow
        ArrowRight,     // ▶ Right arrow

        // Chart Actions
        Fibonacci,      // Φ Fibonacci
        Measure,        // 📏 Ruler/measure
        Text,           // T Text annotation
        Note,           // 📝 Note/comment
    }

    /// <summary>
    /// Maps icons to Unicode symbols
    /// </summary>
    private static readonly Dictionary<Icon, string> IconMap = new()
    {
        // Navigation & Cursor
        { Icon.Cursor, "➜" },
        { Icon.Hand, "✋" },

        // Drawing Tools (using geometric shapes)
        { Icon.TrendLine, "╱" },
        { Icon.HorizontalLine, "━" },
        { Icon.VerticalLine, "┃" },
        { Icon.Rectangle, "▭" },
        { Icon.Circle, "○" },
        { Icon.Triangle, "△" },

        // Chart Tools
        { Icon.ChartCandles, "▉" },
        { Icon.ChartLine, "╱" },
        { Icon.ChartArea, "▨" },
        { Icon.ChartBars, "▌" },

        // Indicators
        { Icon.Indicators, "📊" },
        { Icon.Oscillator, "〰" },
        { Icon.MovingAverage, "⚌" },

        // UI Controls
        { Icon.Settings, "⚙" },
        { Icon.Search, "🔍" },
        { Icon.ZoomIn, "🔍" },  // Can add + in text
        { Icon.ZoomOut, "🔍" }, // Can add - in text
        { Icon.Screenshot, "📷" },
        { Icon.Fullscreen, "⛶" },

        // Time
        { Icon.Timeframe, "🕐" },
        { Icon.Calendar, "📅" },

        // Actions
        { Icon.Play, "▶" },
        { Icon.Pause, "⏸" },
        { Icon.Stop, "⏹" },
        { Icon.Delete, "🗑" },
        { Icon.Save, "💾" },
        { Icon.Load, "📂" },

        // Status
        { Icon.Check, "✓" },
        { Icon.Cross, "✗" },
        { Icon.Warning, "⚠" },
        { Icon.Info, "ℹ" },

        // Arrows
        { Icon.ArrowUp, "▲" },
        { Icon.ArrowDown, "▼" },
        { Icon.ArrowLeft, "◀" },
        { Icon.ArrowRight, "▶" },

        // Chart Actions
        { Icon.Fibonacci, "Φ" },
        { Icon.Measure, "📏" },
        { Icon.Text, "T" },
        { Icon.Note, "📝" },
    };

    /// <summary>
    /// Draws an icon at the specified position
    /// </summary>
    /// <param name="canvas">Canvas to draw on</param>
    /// <param name="icon">Icon to draw</param>
    /// <param name="x">X position (left edge of icon)</param>
    /// <param name="y">Y position (baseline of icon)</param>
    /// <param name="size">Font size for the icon</param>
    /// <param name="color">Color of the icon</param>
    public static void DrawIcon(SKCanvas canvas, Icon icon, float x, float y, float size, SKColor color)
    {
        if (!IconMap.TryGetValue(icon, out string symbol))
        {
            symbol = "?"; // Fallback for unknown icons
        }

        using var font = new SKFont(SKTypeface.Default, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(symbol, x, y, font, paint);
    }

    /// <summary>
    /// Draws an icon centered in a rectangle
    /// </summary>
    /// <param name="canvas">Canvas to draw on</param>
    /// <param name="icon">Icon to draw</param>
    /// <param name="rect">Rectangle to center the icon in</param>
    /// <param name="size">Font size for the icon</param>
    /// <param name="color">Color of the icon</param>
    public static void DrawIconCentered(SKCanvas canvas, Icon icon, SKRect rect, float size, SKColor color)
    {
        if (!IconMap.TryGetValue(icon, out string symbol))
        {
            symbol = "?";
        }

        using var font = new SKFont(SKTypeface.Default, size);
        using var paint = new SKPaint { Color = color, IsAntialias = true };

        // Measure text to center it
        float textWidth = font.MeasureText(symbol);
        float x = rect.Left + (rect.Width - textWidth) / 2;
        float y = rect.Top + (rect.Height + size / 2) / 2;

        canvas.DrawText(symbol, x, y, font, paint);
    }

    /// <summary>
    /// Draws an icon with a label next to it
    /// </summary>
    /// <param name="canvas">Canvas to draw on</param>
    /// <param name="icon">Icon to draw</param>
    /// <param name="label">Text label to draw</param>
    /// <param name="x">X position (left edge)</param>
    /// <param name="y">Y position (baseline)</param>
    /// <param name="iconSize">Font size for the icon</param>
    /// <param name="labelSize">Font size for the label</param>
    /// <param name="color">Color for both icon and label</param>
    /// <param name="spacing">Space between icon and label</param>
    /// <returns>Total width of icon + label</returns>
    public static float DrawIconWithLabel(SKCanvas canvas, Icon icon, string label,
        float x, float y, float iconSize, float labelSize, SKColor color, float spacing = 8)
    {
        // Draw icon
        if (IconMap.TryGetValue(icon, out string symbol))
        {
            using var iconFont = new SKFont(SKTypeface.Default, iconSize);
            using var iconPaint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawText(symbol, x, y, iconFont, iconPaint);

            // Measure icon width
            float iconWidth = iconFont.MeasureText(symbol);

            // Draw label
            using var labelFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), labelSize);
            using var labelPaint = new SKPaint { Color = color, IsAntialias = true };
            canvas.DrawText(label, x + iconWidth + spacing, y, labelFont, labelPaint);

            // Return total width
            float labelWidth = labelFont.MeasureText(label);
            return iconWidth + spacing + labelWidth;
        }

        // Fallback: just draw label
        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), labelSize);
        using var paint = new SKPaint { Color = color, IsAntialias = true };
        canvas.DrawText(label, x, y, font, paint);
        return font.MeasureText(label);
    }

    /// <summary>
    /// Gets the symbol for an icon without rendering it
    /// </summary>
    /// <param name="icon">Icon to get symbol for</param>
    /// <returns>Unicode symbol string</returns>
    public static string GetSymbol(Icon icon)
    {
        return IconMap.TryGetValue(icon, out string symbol) ? symbol : "?";
    }

    /// <summary>
    /// Measures the width of an icon at a given size
    /// </summary>
    /// <param name="icon">Icon to measure</param>
    /// <param name="size">Font size</param>
    /// <returns>Width in pixels</returns>
    public static float MeasureIcon(Icon icon, float size)
    {
        if (!IconMap.TryGetValue(icon, out string symbol))
        {
            symbol = "?";
        }

        using var font = new SKFont(SKTypeface.Default, size);
        return font.MeasureText(symbol);
    }
}
