using SkiaSharp;
using Omnijure.Visual.Rendering;
using System;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Context-sensitive secondary toolbar rendered below the main header.
/// Shows Chart tools when Chart is active, Script tools when Script Editor is active.
/// </summary>
public class SecondaryToolbarRenderer
{
    public const float ToolbarHeight = 28f;
    private const float HeaderHeight = 28f; // Main header above us
    private const float IconSize = 14f;
    private const float ButtonSize = 24f;
    private const float ButtonSpacing = 2f;
    private const float SectionPadding = 8f;
    private const float SeparatorGap = 4f;
    private const float TextButtonPadding = 12f;

    // Paints (created once)
    private readonly SKPaint _bgPaint;
    private readonly SKPaint _btnDefault;
    private readonly SKPaint _btnHover;
    private readonly SKPaint _btnActive;
    private readonly SKPaint _separatorPaint;
    private readonly SKPaint _borderPaint;

    // Fonts
    private readonly SKFont _labelFont;
    private readonly SKFont _smallFont;

    // Button definitions
    private readonly List<ToolbarButton> _chartButtons;
    private readonly List<ToolbarButton> _scriptButtons;

    // Runtime state
    private readonly List<(string id, SKRect rect)> _buttonRects = new();
    private string? _hoveredButtonId;

    public SecondaryToolbarRenderer()
    {
        _bgPaint = new SKPaint { Color = new SKColor(18, 22, 28), Style = SKPaintStyle.Fill };
        _btnDefault = new SKPaint { Color = SKColors.Transparent, Style = SKPaintStyle.Fill };
        _btnHover = new SKPaint { Color = ThemeManager.ButtonHover, Style = SKPaintStyle.Fill };
        _btnActive = new SKPaint { Color = ThemeManager.ButtonActive, Style = SKPaintStyle.Fill };
        _separatorPaint = new SKPaint { Color = new SKColor(40, 46, 56), StrokeWidth = 1 };
        _borderPaint = new SKPaint { Color = ThemeManager.Divider, StrokeWidth = 1 };

        _labelFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
        _smallFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 10);

        _chartButtons = BuildChartButtons();
        _scriptButtons = BuildScriptButtons();
    }

    // ═══════════════════════════════════════════════════════════
    // BUTTON DEFINITIONS
    // ═══════════════════════════════════════════════════════════

    private enum BtnType { Icon, Text, Separator, RightSection }

    private record ToolbarButton(
        string Id,
        BtnType Type,
        SvgIconRenderer.Icon? Icon,
        string Text,
        string Tooltip
    );

    private static List<ToolbarButton> BuildChartButtons() => new()
    {
        // Left: Chart types
        new("chart_type_candles", BtnType.Icon, SvgIconRenderer.Icon.Candles, "", "Candlestick (Alt+1)"),
        new("chart_type_line", BtnType.Icon, SvgIconRenderer.Icon.LineChart, "", "Line Chart (Alt+2)"),
        new("chart_type_area", BtnType.Icon, SvgIconRenderer.Icon.AreaChart, "", "Area Chart (Alt+3)"),
        new("chart_type_bars", BtnType.Icon, SvgIconRenderer.Icon.Bars, "", "Bar Chart (Alt+4)"),
        new("sep_1", BtnType.Separator, null, "", ""),

        // Indicator
        new("chart_indicator", BtnType.Icon, SvgIconRenderer.Icon.Indicators, "", "Indicators (Ctrl+I)"),
        new("sep_2", BtnType.Separator, null, "", ""),

        // Timeframes
        new("tf_1m", BtnType.Text, null, "1m", "1 Minute"),
        new("tf_5m", BtnType.Text, null, "5m", "5 Minutes"),
        new("tf_15m", BtnType.Text, null, "15m", "15 Minutes"),
        new("tf_1h", BtnType.Text, null, "1h", "1 Hour"),
        new("tf_4h", BtnType.Text, null, "4h", "4 Hours"),
        new("tf_1d", BtnType.Text, null, "1d", "1 Day"),

        // Right-aligned section
        new("right", BtnType.RightSection, null, "", ""),
        new("chart_screenshot", BtnType.Icon, SvgIconRenderer.Icon.Screenshot, "", "Screenshot (Ctrl+Shift+S)"),
        new("chart_fullscreen", BtnType.Icon, SvgIconRenderer.Icon.Fullscreen, "", "Fullscreen (F11)"),
        new("sep_3", BtnType.Separator, null, "", ""),
        new("chart_zoomin", BtnType.Icon, SvgIconRenderer.Icon.ZoomIn, "", "Zoom In (Ctrl++)"),
        new("chart_zoomout", BtnType.Icon, SvgIconRenderer.Icon.ZoomOut, "", "Zoom Out (Ctrl+-)"),
        new("chart_zoomreset", BtnType.Text, null, "Reset", "Reset View (Space)"),
    };

    private static List<ToolbarButton> BuildScriptButtons() => new()
    {
        // Left: File operations
        new("script_new", BtnType.Icon, SvgIconRenderer.Icon.Plus, "", "New Script (Ctrl+N)"),
        new("script_open", BtnType.Icon, SvgIconRenderer.Icon.FolderOpen, "", "Open Script (Ctrl+O)"),
        new("script_save", BtnType.Icon, SvgIconRenderer.Icon.Save, "", "Save Script (Ctrl+S)"),
        new("sep_1", BtnType.Separator, null, "", ""),

        // Center: Execution
        new("script_run", BtnType.Icon, SvgIconRenderer.Icon.Play, "", "Run Script (F5)"),
        new("script_stop", BtnType.Icon, SvgIconRenderer.Icon.Stop, "", "Stop (Shift+F5)"),
        new("sep_2", BtnType.Separator, null, "", ""),
        new("script_toggle", BtnType.Icon, SvgIconRenderer.Icon.Lightning, "", "Toggle ON/OFF (Ctrl+T)"),

        // Right-aligned section
        new("right", BtnType.RightSection, null, "", ""),
        new("script_undo", BtnType.Icon, SvgIconRenderer.Icon.Undo, "", "Undo (Ctrl+Z)"),
        new("script_redo", BtnType.Icon, SvgIconRenderer.Icon.Redo, "", "Redo (Ctrl+Y)"),
        new("sep_3", BtnType.Separator, null, "", ""),
        new("script_find", BtnType.Icon, SvgIconRenderer.Icon.Search, "", "Find (Ctrl+F)"),
        new("sep_4", BtnType.Separator, null, "", ""),
        new("script_fontup", BtnType.Icon, SvgIconRenderer.Icon.ZoomIn, "", "Increase Font (Ctrl++)"),
        new("script_fontdown", BtnType.Icon, SvgIconRenderer.Icon.ZoomOut, "", "Decrease Font (Ctrl+-)"),
        new("sep_5", BtnType.Separator, null, "", ""),
        new("script_settings", BtnType.Icon, SvgIconRenderer.Icon.Settings, "", "Script Settings"),
    };

    // ═══════════════════════════════════════════════════════════
    // RENDERING
    // ═══════════════════════════════════════════════════════════

    public void Render(SKCanvas canvas, float screenWidth, string activeCenterTabId,
        ChartType chartType, string timeframe,
        string? assetSymbol = null, float assetPrice = 0, float assetChange = 0)
    {
        float barTop = HeaderHeight;
        float barBottom = HeaderHeight + ToolbarHeight;
        var barRect = new SKRect(0, barTop, screenWidth, barBottom);

        // Background
        canvas.DrawRect(barRect, _bgPaint);

        // Bottom border
        canvas.DrawLine(0, barBottom, screenWidth, barBottom, _borderPaint);

        // Asset info (chart mode only, left side)
        float assetInfoWidth = 0;
        if (activeCenterTabId != PanelDefinitions.SCRIPT_EDITOR && !string.IsNullOrEmpty(assetSymbol))
        {
            assetInfoWidth = RenderAssetInfo(canvas, barRect, assetSymbol, assetPrice, assetChange);
        }

        // Select button set
        var buttons = activeCenterTabId == PanelDefinitions.SCRIPT_EDITOR
            ? _scriptButtons : _chartButtons;

        _buttonRects.Clear();
        LayoutAndRenderButtons(canvas, barRect, buttons, chartType, timeframe, activeCenterTabId, screenWidth, assetInfoWidth);
    }

    private float RenderAssetInfo(SKCanvas canvas, SKRect barRect, string symbol, float price, float change)
    {
        float x = SectionPadding;
        float midY = (barRect.Top + barRect.Bottom) / 2f;
        var paint = PaintPool.Instance.Rent();
        try
        {
            paint.IsAntialias = true;

            // Crypto icon
            float iconSz = ToolbarHeight - 10;
            CryptoIconProvider.DrawCryptoIcon(canvas, symbol, x, barRect.Top + 5, (int)iconSz);
            x += iconSz + 4;

            // Symbol name
            paint.Color = ThemeManager.TextWhite;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawText(symbol, x, midY + 4, _labelFont, paint);
            x += _labelFont.MeasureText(symbol) + 6;

            // Price
            if (price > 0)
            {
                string priceText = $"${price:F2}";
                paint.Color = ThemeManager.TextPrimary;
                canvas.DrawText(priceText, x, midY + 4, _smallFont, paint);
                x += _smallFont.MeasureText(priceText) + 5;

                // Change %
                SKColor cc = change >= 0 ? ThemeManager.Success : ThemeManager.Error;
                string ct = $"{(change >= 0 ? "+" : "")}{change:F2}%";
                paint.Color = cc;
                canvas.DrawText(ct, x, midY + 4, _smallFont, paint);
                x += _smallFont.MeasureText(ct) + 4;
            }

            // Separator after asset info
            x += SeparatorGap;
            canvas.DrawLine(x, barRect.Top + 6, x, barRect.Bottom - 6, _separatorPaint);
            x += SeparatorGap;

            return x;
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private void LayoutAndRenderButtons(SKCanvas canvas, SKRect barRect,
        List<ToolbarButton> buttons, ChartType chartType, string timeframe,
        string activeCenterId, float screenWidth, float leftOffset = 0)
    {
        var paint = PaintPool.Instance.Rent();
        try
        {
            float barTop = barRect.Top;
            float barBottom = barRect.Bottom;
            float barMidY = (barTop + barBottom) / 2f;

            // First pass: find right-section marker index
            int rightSectionStart = buttons.Count;
            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].Type == BtnType.RightSection) { rightSectionStart = i + 1; break; }
            }

            // Layout right section first (right-to-left) to know how much space it takes
            float rightX = screenWidth - SectionPadding;
            var rightBtnLayouts = new List<(int idx, SKRect rect)>();

            for (int i = buttons.Count - 1; i >= rightSectionStart; i--)
            {
                var btn = buttons[i];
                if (btn.Type == BtnType.Separator)
                {
                    rightX -= SeparatorGap;
                    // We'll draw separator later
                    rightBtnLayouts.Insert(0, (i, new SKRect(rightX, barTop + 6, rightX + 1, barBottom - 6)));
                    rightX -= SeparatorGap;
                    continue;
                }

                float w = btn.Type == BtnType.Text ? MeasureTextButton(btn.Text) : ButtonSize;
                float btnLeft = rightX - w;
                float btnTop = barMidY - ButtonSize / 2f;
                var rect = new SKRect(btnLeft, btnTop, btnLeft + w, btnTop + ButtonSize);
                rightBtnLayouts.Insert(0, (i, rect));
                rightX -= w + ButtonSpacing;
            }

            // Layout left section (left-to-right, after asset info if present)
            float leftX = leftOffset > 0 ? leftOffset : SectionPadding;

            for (int i = 0; i < buttons.Count; i++)
            {
                if (buttons[i].Type == BtnType.RightSection) break;

                var btn = buttons[i];
                if (btn.Type == BtnType.Separator)
                {
                    leftX += SeparatorGap;
                    DrawSeparator(canvas, leftX, barTop, barBottom);
                    leftX += SeparatorGap + 1;
                    continue;
                }

                float w = btn.Type == BtnType.Text ? MeasureTextButton(btn.Text) : ButtonSize;
                float btnTop = barMidY - ButtonSize / 2f;
                var rect = new SKRect(leftX, btnTop, leftX + w, btnTop + ButtonSize);

                bool isActive = GetIsActive(btn, chartType, timeframe, activeCenterId);
                bool isHovered = _hoveredButtonId == btn.Id;

                RenderButton(canvas, paint, rect, btn, isActive, isHovered);
                _buttonRects.Add((btn.Id, rect));

                leftX += w + ButtonSpacing;
            }

            // Render right section buttons
            foreach (var (idx, rect) in rightBtnLayouts)
            {
                var btn = buttons[idx];
                if (btn.Type == BtnType.Separator)
                {
                    DrawSeparator(canvas, rect.Left, barTop, barBottom);
                    continue;
                }

                bool isActive = GetIsActive(btn, chartType, timeframe, activeCenterId);
                bool isHovered = _hoveredButtonId == btn.Id;

                RenderButton(canvas, paint, rect, btn, isActive, isHovered);
                _buttonRects.Add((btn.Id, rect));
            }
        }
        finally
        {
            PaintPool.Instance.Return(paint);
        }
    }

    private bool GetIsActive(ToolbarButton btn, ChartType chartType, string timeframe, string activeCenterId)
    {
        return btn.Id switch
        {
            "chart_type_candles" => chartType == ChartType.Candles,
            "chart_type_line" => chartType == ChartType.Line,
            "chart_type_area" => chartType == ChartType.Area,
            "chart_type_bars" => chartType == ChartType.Bars,
            "tf_1m" => timeframe == "1m",
            "tf_5m" => timeframe == "5m",
            "tf_15m" => timeframe == "15m",
            "tf_1h" => timeframe == "1h",
            "tf_4h" => timeframe == "4h",
            "tf_1d" => timeframe == "1d",
            _ => false
        };
    }

    private void RenderButton(SKCanvas canvas, SKPaint paint, SKRect rect,
        ToolbarButton btn, bool isActive, bool isHovered)
    {
        // Background
        if (isActive)
        {
            paint.Color = ThemeManager.ButtonActive;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(rect, ThemeManager.BorderRadiusSmall, ThemeManager.BorderRadiusSmall, paint);
        }
        else if (isHovered)
        {
            paint.Color = ThemeManager.ButtonHover;
            paint.Style = SKPaintStyle.Fill;
            canvas.DrawRoundRect(rect, ThemeManager.BorderRadiusSmall, ThemeManager.BorderRadiusSmall, paint);
        }

        if (btn.Type == BtnType.Icon && btn.Icon.HasValue)
        {
            // Special coloring for play/stop
            SKColor iconColor;
            if (btn.Id == "script_run")
                iconColor = isHovered ? new SKColor(80, 220, 120) : new SKColor(46, 204, 113);
            else if (btn.Id == "script_stop")
                iconColor = isHovered ? new SKColor(255, 100, 100) : new SKColor(239, 83, 80);
            else
                iconColor = isActive ? ThemeManager.TextWhite : (isHovered ? ThemeManager.TextPrimary : ThemeManager.TextSecondary);

            SvgIconRenderer.DrawIconCentered(canvas, btn.Icon.Value, rect, IconSize, iconColor);
        }
        else if (btn.Type == BtnType.Text)
        {
            paint.Color = isActive ? ThemeManager.TextWhite : (isHovered ? ThemeManager.TextPrimary : ThemeManager.TextSecondary);
            paint.Style = SKPaintStyle.Fill;
            paint.IsAntialias = true;

            float textW = _smallFont.MeasureText(btn.Text);
            float textX = rect.MidX - textW / 2f;
            float textY = rect.MidY + 4f;
            canvas.DrawText(btn.Text, textX, textY, _smallFont, paint);
        }
    }

    private void DrawSeparator(SKCanvas canvas, float x, float barTop, float barBottom)
    {
        canvas.DrawLine(x, barTop + 6, x, barBottom - 6, _separatorPaint);
    }

    private float MeasureTextButton(string text)
    {
        float w = _smallFont.MeasureText(text);
        return w + TextButtonPadding;
    }

    // ═══════════════════════════════════════════════════════════
    // MOUSE INTERACTION
    // ═══════════════════════════════════════════════════════════

    public void UpdateMousePos(float x, float y)
    {
        _hoveredButtonId = null;
        if (y < HeaderHeight || y > HeaderHeight + ToolbarHeight) return;

        foreach (var (id, rect) in _buttonRects)
        {
            if (rect.Contains(x, y)) { _hoveredButtonId = id; break; }
        }
    }

    public string? GetButtonAtPosition(float x, float y)
    {
        if (y < HeaderHeight || y > HeaderHeight + ToolbarHeight) return null;

        foreach (var (id, rect) in _buttonRects)
        {
            if (rect.Contains(x, y)) return id;
        }
        return null;
    }

    public bool Contains(float x, float y)
        => y >= HeaderHeight && y <= HeaderHeight + ToolbarHeight;
}
