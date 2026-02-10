

using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Rendering;

public class ToolbarRenderer
{
    private readonly SKPaint _gradientPaint;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _textPaintLarge;
    private readonly SKPaint _btnFill;
    private readonly SKPaint _btnHover;
    private readonly SKPaint _dropdownBg;
    private readonly SKPaint _searchBoxBg;
    private readonly SKPaint _searchBoxFocused;
    private readonly SKPaint _shadowPaint;
    private readonly SKFont _font;
    private readonly SKFont _fontLarge;
    private readonly SKFont _fontSmall;

    public ToolbarRenderer()
    {
        // Modern gradient background
        _gradientPaint = new SKPaint
        {
            Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(0, 50),
                new[] { ThemeManager.Surface, ThemeManager.Background },
                SKShaderTileMode.Clamp
            ),
            Style = SKPaintStyle.Fill
        };

        _textPaint = new SKPaint { Color = ThemeManager.TextPrimary, IsAntialias = true };
        _textPaintLarge = new SKPaint { Color = ThemeManager.TextWhite, IsAntialias = true };
        _btnFill = new SKPaint { Color = ThemeManager.ButtonDefault, Style = SKPaintStyle.Fill };
        _btnHover = new SKPaint { Color = ThemeManager.ButtonHover, Style = SKPaintStyle.Fill };
        _dropdownBg = new SKPaint { Color = ThemeManager.SurfaceElevated, Style = SKPaintStyle.Fill };
        _searchBoxBg = new SKPaint { Color = ThemeManager.Surface, Style = SKPaintStyle.Fill };
        _searchBoxFocused = new SKPaint { Color = ThemeManager.ButtonDefault, Style = SKPaintStyle.Fill };
        _shadowPaint = new SKPaint
        {
            Color = ThemeManager.ShadowMedium,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 4)
        };
        
        _font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 13);
        _fontLarge = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 16);
        _fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
    }

    // Menu items for trading platform
    private static readonly string[] MenuItems = ["Markets", "Charts", "Strategy", "Tools", "View"];
    
    // Window control state
    private Action? _onClose;
    private Action? _onMinimize;
    private Action? _onMaximize;
    private SKRect _closeButtonRect;
    private SKRect _maximizeButtonRect;
    private SKRect _minimizeButtonRect;
    private SKRect _dragAreaRect;
    private bool _isDraggingWindow;
    private SKPoint _dragStart;

    public void SetWindowActions(Action onClose, Action onMinimize, Action onMaximize)
    {
        _onClose = onClose;
        _onMinimize = onMinimize;
        _onMaximize = onMaximize;
    }

    public bool HandleMouseDown(float x, float y)
    {
        if (_closeButtonRect.Contains(x, y)) { _onClose?.Invoke(); return true; }
        if (_maximizeButtonRect.Contains(x, y)) { _onMaximize?.Invoke(); return true; }
        if (_minimizeButtonRect.Contains(x, y)) { _onMinimize?.Invoke(); return true; }
        return false;
    }

    public void Render(SKCanvas canvas, SKRect rect, UiSearchBox searchBox, List<UiDropdown> dropdowns, List<UiButton> buttons)
    {
        // Background with gradient
        canvas.DrawRect(rect, _gradientPaint);
        
        // Bottom shadow for depth
        var shadowRect = new SKRect(rect.Left, rect.Bottom - 2, rect.Right, rect.Bottom + 2);
        canvas.DrawRect(shadowRect, _shadowPaint);
        
        float x = 8;
        float y = rect.Top + 8;
        float itemH = 34;
        
        // 0. Menu Items (izquierda, como IDE)
        using var menuFont = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 12);
        foreach (var menuItem in MenuItems)
        {
            float menuTextW = menuFont.MeasureText(menuItem);
            float menuBtnW = menuTextW + 16;
            var menuRect = new SKRect(x, y + 2, x + menuBtnW, y + itemH - 2);
            
            bool isHovered = menuRect.Contains(_lastMouseX, _lastMouseY);
            if (isHovered)
            {
                canvas.DrawRoundRect(menuRect, 4, 4, _btnHover);
            }
            
            using var menuPaint = new SKPaint 
            { 
                Color = isHovered ? ThemeManager.TextWhite : ThemeManager.TextSecondary, 
                IsAntialias = true 
            };
            canvas.DrawText(menuItem, x + 8, menuRect.MidY + 4, menuFont, menuPaint);
            x += menuBtnW + 2;
        }
        
        // Separator after menu
        canvas.DrawLine(x + 4, y + 5, x + 4, y + itemH - 5, new SKPaint { Color = ThemeManager.Border, StrokeWidth = 1 });
        x += 16;
        
        // 1. Search Box
        if (searchBox != null)
        {
            var searchRect = new SKRect(x, y, x + 250, y + itemH);
            searchBox.Rect = searchRect;
            
            // Draw search box background
            canvas.DrawRoundRect(searchRect, 6, 6, searchBox.IsFocused ? _searchBoxFocused : _searchBoxBg);
            
            // Search icon (SVG for crisp rendering)
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Search, searchRect.Left + 10, searchRect.Top + 7, 20, ThemeManager.TextSecondary);
            
            // Text or placeholder
            string displayText = string.IsNullOrEmpty(searchBox.Text) ? searchBox.Placeholder : searchBox.Text;
            using var textPaint = string.IsNullOrEmpty(searchBox.Text) 
                ? new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true }
                : _textPaint;
            
            canvas.DrawText(displayText, searchRect.Left + 35, searchRect.MidY + 5, _font, textPaint);
            
            // Cursor if focused
            if (searchBox.IsFocused && !string.IsNullOrEmpty(searchBox.Text))
            {
                float cursorX = searchRect.Left + 35 + _font.MeasureText(searchBox.Text.Substring(0, searchBox.CursorPosition));
                canvas.DrawLine(cursorX, searchRect.Top + 8, cursorX, searchRect.Bottom - 8, _textPaintLarge);
            }
            
            // Clear button if text exists
            if (!string.IsNullOrEmpty(searchBox.Text))
            {
                var clearRect = new SKRect(searchRect.Right - 30, searchRect.Top + 7, searchRect.Right - 7, searchRect.Bottom - 7);
                using var circlePaint = new SKPaint { Color = ThemeManager.Border, Style = SKPaintStyle.Fill };
                canvas.DrawCircle(clearRect.MidX, clearRect.MidY, 10, circlePaint);
                // Draw X
                using var xPaint = new SKPaint { Color = ThemeManager.TextPrimary, StrokeWidth = 2, IsAntialias = true };
                canvas.DrawLine(clearRect.MidX - 4, clearRect.MidY - 4, clearRect.MidX + 4, clearRect.MidY + 4, xPaint);
                canvas.DrawLine(clearRect.MidX + 4, clearRect.MidY - 4, clearRect.MidX - 4, clearRect.MidY + 4, xPaint);
            }
            
            x += 265;
        }
        
        // 2. Current Asset Display (Prominent)
        if (dropdowns != null && dropdowns.Count > 0)
        {
            var assetDd = dropdowns.FirstOrDefault(d => d.Label == "Asset");
            if (assetDd != null)
            {
                // Crypto icon before asset name
                CryptoIconProvider.DrawCryptoIcon(canvas, assetDd.SelectedItem, x, y + 2, 24);
                x += 30;

                // Asset name (large and bold)
                canvas.DrawText(assetDd.SelectedItem, x, y + 12, _fontLarge, _textPaintLarge);
                float assetW = _fontLarge.MeasureText(assetDd.SelectedItem);
                x += assetW + 15;
                
                // Price and change
                if (assetDd.CurrentPrice > 0)
                {
                    string priceText = $"${assetDd.CurrentPrice:F2}";
                    canvas.DrawText(priceText, x, y + 11, _font, _textPaint);
                    float priceW = _font.MeasureText(priceText);
                    x += priceW + 10;
                    
                    // Change % with vector arrow
                    SKColor changeColor = assetDd.PercentChange >= 0 ? ThemeManager.Success : ThemeManager.Error;

                    // Draw arrow triangle
                    using var arrowPath2 = new SKPath();
                    float ax = x;
                    float ay = y + 7;
                    if (assetDd.PercentChange >= 0)
                    {
                        arrowPath2.MoveTo(ax, ay + 6);
                        arrowPath2.LineTo(ax + 4, ay);
                        arrowPath2.LineTo(ax + 8, ay + 6);
                    }
                    else
                    {
                        arrowPath2.MoveTo(ax, ay);
                        arrowPath2.LineTo(ax + 4, ay + 6);
                        arrowPath2.LineTo(ax + 8, ay);
                    }
                    arrowPath2.Close();
                    using var arrowFill = new SKPaint { Color = changeColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawPath(arrowPath2, arrowFill);
                    x += 12;

                    string changeText = $"{Math.Abs(assetDd.PercentChange):F2}%";
                    using var changePaint = new SKPaint { Color = changeColor, IsAntialias = true };
                    canvas.DrawText(changeText, x, y + 11, _font, changePaint);
                    x += _font.MeasureText(changeText) + 20;
                }
                
                // Separator
                canvas.DrawLine(x, y + 5, x, y + itemH - 5, new SKPaint { Color = ThemeManager.Border, StrokeWidth = 1 });
                x += 20;
            }
        }
        
        // 3. Interval Dropdown
        if (dropdowns != null && dropdowns.Count > 1)
        {
            var intervalDd = dropdowns.FirstOrDefault(d => d.Label == "Interval");
            if (intervalDd != null)
            {
                intervalDd.Rect = new SKRect(x, y, x + 100, y + itemH);
                canvas.DrawRoundRect(intervalDd.Rect, 6, 6, intervalDd.IsHovered ? _btnHover : _btnFill);
                
                string text = $"{intervalDd.Label}: {intervalDd.SelectedItem}";
                canvas.DrawText(text, intervalDd.Rect.Left + 10, intervalDd.Rect.MidY + 5, _font, _textPaint);

                // Down arrow icon
                using var arrowPath = new SKPath();
                float arrowX = intervalDd.Rect.Right - 15;
                float arrowY = intervalDd.Rect.MidY;
                arrowPath.MoveTo(arrowX - 4, arrowY - 2);
                arrowPath.LineTo(arrowX, arrowY + 2);
                arrowPath.LineTo(arrowX + 4, arrowY - 2);
                using var arrowPaint = new SKPaint { Color = ThemeManager.TextSecondary, Style = SKPaintStyle.Stroke, StrokeWidth = 2, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                canvas.DrawPath(arrowPath, arrowPaint);
                
                x += 115;
            }
        }
        
        // 4. Chart Type Buttons
        if (buttons != null)
        {
            foreach (var btn in buttons)
            {
                btn.Rect = new SKRect(x, y, x + 70, y + itemH);
                canvas.DrawRoundRect(btn.Rect, 6, 6, btn.IsHovered ? _btnHover : _btnFill);
                
                float tw = _font.MeasureText(btn.Text);
                canvas.DrawText(btn.Text, btn.Rect.MidX - (tw / 2), btn.Rect.MidY + 5, _font, _textPaint);
                x += 80;
            }
        }
        
        // 5. Window Controls (derecha, como IDE)
        float btnW = 46;
        float btnH = rect.Height;
        float rightEdge = rect.Right;

        // Close button (rojo al hover)
        _closeButtonRect = new SKRect(rightEdge - btnW, rect.Top, rightEdge, rect.Top + btnH);
        bool closeHover = _closeButtonRect.Contains(_lastMouseX, _lastMouseY);
        if (closeHover)
        {
            using var closeBg = new SKPaint { Color = new SKColor(232, 17, 35), Style = SKPaintStyle.Fill };
            canvas.DrawRect(_closeButtonRect, closeBg);
        }
        // X icon
        {
            float cx = _closeButtonRect.MidX, cy = _closeButtonRect.MidY;
            using var xPaint = new SKPaint 
            { 
                Color = closeHover ? SKColors.White : ThemeManager.TextSecondary, 
                StrokeWidth = 1.5f, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round 
            };
            canvas.DrawLine(cx - 5, cy - 5, cx + 5, cy + 5, xPaint);
            canvas.DrawLine(cx + 5, cy - 5, cx - 5, cy + 5, xPaint);
        }

        // Maximize button
        _maximizeButtonRect = new SKRect(rightEdge - btnW * 2, rect.Top, rightEdge - btnW, rect.Top + btnH);
        bool maxHover = _maximizeButtonRect.Contains(_lastMouseX, _lastMouseY);
        if (maxHover)
        {
            canvas.DrawRect(_maximizeButtonRect, _btnHover);
        }
        {
            float cx = _maximizeButtonRect.MidX, cy = _maximizeButtonRect.MidY;
            using var boxPaint = new SKPaint 
            { 
                Color = ThemeManager.TextSecondary, StrokeWidth = 1.5f, 
                IsAntialias = true, Style = SKPaintStyle.Stroke 
            };
            canvas.DrawRect(cx - 5, cy - 4, 10, 9, boxPaint);
        }

        // Minimize button
        _minimizeButtonRect = new SKRect(rightEdge - btnW * 3, rect.Top, rightEdge - btnW * 2, rect.Top + btnH);
        bool minHover = _minimizeButtonRect.Contains(_lastMouseX, _lastMouseY);
        if (minHover)
        {
            canvas.DrawRect(_minimizeButtonRect, _btnHover);
        }
        {
            float cx = _minimizeButtonRect.MidX, cy = _minimizeButtonRect.MidY;
            using var linePaint = new SKPaint 
            { 
                Color = ThemeManager.TextSecondary, StrokeWidth = 1.5f, IsAntialias = true 
            };
            canvas.DrawLine(cx - 5, cy, cx + 5, cy, linePaint);
        }

        // Drag area (entre contenido y botones de ventana)
        _dragAreaRect = new SKRect(x, rect.Top, rightEdge - btnW * 3, rect.Bottom);
        
        // 6. Render Dropdown Lists (if open)
        if (dropdowns != null)
        {
            foreach (var dd in dropdowns)
            {
                if (dd.IsOpen)
                {
                    RenderDropdownList(canvas, dd);
                }
            }
        }
    }
    
    private void RenderDropdownList(SKCanvas canvas, UiDropdown dd)
    {
        canvas.Save();
        float itemH = 32;
        var filtered = dd.GetFilteredItems();
        
        int displayCount = Math.Min(filtered.Count, dd.MaxVisibleItems);
        float fullH = displayCount * itemH;
        var listRect = new SKRect(dd.Rect.Left, dd.Rect.Bottom + 5, dd.Rect.Right, dd.Rect.Bottom + 5 + fullH);
        
        // Shadow
        canvas.DrawRect(listRect, _shadowPaint);
        canvas.DrawRoundRect(listRect, 6, 6, _dropdownBg);
        
        int startIdx = (int)dd.ScrollOffset;
        for (int i = 0; i < displayCount; i++)
        {
            int actualIdx = startIdx + i;
            if (actualIdx >= filtered.Count) break;

            var itemRect = new SKRect(dd.Rect.Left, dd.Rect.Bottom + 5 + (i * itemH), dd.Rect.Right, dd.Rect.Bottom + 5 + ((i + 1) * itemH));
            
            // Hover effect
            if (itemRect.Contains(_lastMouseX, _lastMouseY))
            {
                canvas.DrawRect(itemRect, _btnHover);
            }
            
            canvas.DrawText(filtered[actualIdx], itemRect.Left + 12, itemRect.MidY + 5, _font, _textPaint);
        }
        
        // Scrollbar
        if (filtered.Count > dd.MaxVisibleItems)
        {
            float scrollTrackH = displayCount * itemH;
            float scrollThumbH = (dd.MaxVisibleItems / (float)filtered.Count) * scrollTrackH;
            float scrollThumbY = (dd.ScrollOffset / (float)filtered.Count) * scrollTrackH;
            
            var thumbRect = new SKRect(dd.Rect.Right - 8, dd.Rect.Bottom + 5 + scrollThumbY, dd.Rect.Right - 3, dd.Rect.Bottom + 5 + scrollThumbY + scrollThumbH);
            using var scrollPaint = new SKPaint { Color = ThemeManager.TextMuted, Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(thumbRect, 2, 2, scrollPaint);
        }
        
        canvas.Restore();
    }
    
    private float _lastMouseX = 0;
    private float _lastMouseY = 0;
    
    public void UpdateMousePos(float x, float y)
    {
        _lastMouseX = x;
        _lastMouseY = y;
    }
}
