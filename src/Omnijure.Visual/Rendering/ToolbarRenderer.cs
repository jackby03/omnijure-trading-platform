


using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;

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

    // Menu items for Omnijure TDS
    private static readonly string[] MenuItems = ["Trading", "Bots", "AI", "Markets", "View", "Scripting"];
    
    // Win32 P/Invoke
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    
    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }
    
    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    // Window control state
    private Action? _onClose;
    private Action? _onMinimize;
    private Action? _onMaximize;
    private Action<int, int>? _onWindowMove;
    private SKRect _closeButtonRect;
    private SKRect _maximizeButtonRect;
    private SKRect _minimizeButtonRect;
    private SKRect _dragAreaRect;
    private bool _isDraggingWindow;
    private POINT _dragStartCursor;
    private (int X, int Y) _dragStartWindowPos;

    public void SetWindowActions(Action onClose, Action onMinimize, Action onMaximize, 
        Action<int, int> onWindowMove, IntPtr windowHandle)
    {
        _onClose = onClose;
        _onMinimize = onMinimize;
        _onMaximize = onMaximize;
        _onWindowMove = onWindowMove;
        
        // Windows 11 rounded corners
        int preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(windowHandle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));
    }

    public bool HandleMouseDown(float x, float y, int windowX, int windowY)
    {
        if (_closeButtonRect.Contains(x, y)) { _onClose?.Invoke(); return true; }
        if (_maximizeButtonRect.Contains(x, y)) { _onMaximize?.Invoke(); return true; }
        if (_minimizeButtonRect.Contains(x, y)) { _onMinimize?.Invoke(); return true; }
        
        if (_dragAreaRect.Contains(x, y))
        {
            _isDraggingWindow = true;
            GetCursorPos(out _dragStartCursor);
            _dragStartWindowPos = (windowX, windowY);
            return true;
        }
        return false;
    }

    public bool HandleMouseMove()
    {
        if (!_isDraggingWindow) return false;
        
        GetCursorPos(out var cursor);
        int dx = cursor.X - _dragStartCursor.X;
        int dy = cursor.Y - _dragStartCursor.Y;
        _onWindowMove?.Invoke(_dragStartWindowPos.X + dx, _dragStartWindowPos.Y + dy);
        return true;
    }

    public void HandleMouseUp()
    {
        _isDraggingWindow = false;
    }

    public bool IsDraggingWindow => _isDraggingWindow;

    public void Render(SKCanvas canvas, SKRect rect, UiSearchBox searchBox, UiDropdown assetInfo, List<UiDropdown> dropdowns, List<UiButton> buttons)
    {
        // Background
        canvas.DrawRect(rect, _gradientPaint);
        using var borderPaint = new SKPaint { Color = new SKColor(40, 43, 50), StrokeWidth = 1 };
        canvas.DrawLine(rect.Left, rect.Bottom, rect.Right, rect.Bottom, borderPaint);
        
        float x = 6;
        float midY = rect.MidY;
        using var sepPaint = new SKPaint { Color = ThemeManager.Border, StrokeWidth = 1 };
        
        // 0. Menu Items (compactos)
        foreach (var menuItem in MenuItems)
        {
            float tw = _fontSmall.MeasureText(menuItem);
            float btnW = tw + 14;
            var menuRect = new SKRect(x, rect.Top, x + btnW, rect.Bottom);
            bool hover = menuRect.Contains(_lastMouseX, _lastMouseY);
            if (hover) canvas.DrawRect(menuRect, _btnHover);
            
            using var menuPaint = new SKPaint { Color = hover ? ThemeManager.TextWhite : ThemeManager.TextSecondary, IsAntialias = true };
            canvas.DrawText(menuItem, x + 7, midY + 4, _fontSmall, menuPaint);
            x += btnW;
        }
        
        canvas.DrawLine(x + 4, rect.Top + 5, x + 4, rect.Bottom - 5, sepPaint);
        x += 12;
        
        // 1. Search Box
        if (searchBox != null)
        {
            float searchW = 160;
            var searchRect = new SKRect(x, rect.Top + 3, x + searchW, rect.Bottom - 3);
            searchBox.Rect = searchRect;
            canvas.DrawRoundRect(searchRect, 4, 4, searchBox.IsFocused ? _searchBoxFocused : _searchBoxBg);
            SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Search, searchRect.Left + 4, searchRect.Top + 1, searchRect.Height - 2, ThemeManager.TextMuted);
            
            string displayText = string.IsNullOrEmpty(searchBox.Text) ? searchBox.Placeholder : searchBox.Text;
            using var sPaint = string.IsNullOrEmpty(searchBox.Text) ? new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true } : _textPaint;
            canvas.DrawText(displayText, searchRect.Left + searchRect.Height + 2, midY + 4, _fontSmall, sPaint);
            x += searchW + 8;
        }
        
        canvas.DrawLine(x, rect.Top + 5, x, rect.Bottom - 5, sepPaint);
        x += 8;
        
        // 2. Asset info (read-only, no clickeable)
        if (assetInfo != null)
        {
            float iconSz = rect.Height - 8;
            CryptoIconProvider.DrawCryptoIcon(canvas, assetInfo.SelectedItem, x, rect.Top + 4, (int)iconSz);
            x += iconSz + 4;
            canvas.DrawText(assetInfo.SelectedItem, x, midY + 4, _font, _textPaintLarge);
            x += _font.MeasureText(assetInfo.SelectedItem) + 6;
            
            if (assetInfo.CurrentPrice > 0)
            {
                string priceText = $"${assetInfo.CurrentPrice:F2}";
                canvas.DrawText(priceText, x, midY + 4, _fontSmall, _textPaint);
                x += _fontSmall.MeasureText(priceText) + 5;
                
                SKColor cc = assetInfo.PercentChange >= 0 ? ThemeManager.Success : ThemeManager.Error;
                string ct = $"{(assetInfo.PercentChange >= 0 ? "+" : "")}{assetInfo.PercentChange:F2}%";
                using var cp = new SKPaint { Color = cc, IsAntialias = true };
                canvas.DrawText(ct, x, midY + 4, _fontSmall, cp);
                x += _fontSmall.MeasureText(ct) + 8;
            }
            canvas.DrawLine(x, rect.Top + 5, x, rect.Bottom - 5, sepPaint);
            x += 8;
        }
        
        // 3. Interval Dropdown
        if (dropdowns != null)
        {
            var intervalDd = dropdowns.FirstOrDefault(d => d.Label == "Interval");
            if (intervalDd != null)
            {
                float ddW = 70;
                intervalDd.Rect = new SKRect(x, rect.Top + 2, x + ddW, rect.Bottom - 2);
                canvas.DrawRoundRect(intervalDd.Rect, 3, 3, intervalDd.IsHovered ? _btnHover : _btnFill);
                canvas.DrawText(intervalDd.SelectedItem, x + 6, midY + 4, _fontSmall, _textPaint);
                float arrowX = intervalDd.Rect.Right - 10;
                using var ap = new SKPaint { Color = ThemeManager.TextMuted, StrokeWidth = 1.2f, IsAntialias = true, StrokeCap = SKStrokeCap.Round };
                canvas.DrawLine(arrowX - 2, midY - 1, arrowX, midY + 1, ap);
                canvas.DrawLine(arrowX, midY + 1, arrowX + 2, midY - 1, ap);
                x += ddW + 4;
            }
        }

        // 5. Window Controls (derecha)
        float wcW = 46;
        float re = rect.Right;

        _closeButtonRect = new SKRect(re - wcW, rect.Top, re, rect.Bottom);
        bool ch = _closeButtonRect.Contains(_lastMouseX, _lastMouseY);
        if (ch) { using var cbg = new SKPaint { Color = new SKColor(232, 17, 35), Style = SKPaintStyle.Fill }; canvas.DrawRect(_closeButtonRect, cbg); }
        { float cx = _closeButtonRect.MidX, cy = midY; using var xp = new SKPaint { Color = ch ? SKColors.White : ThemeManager.TextSecondary, StrokeWidth = 1.2f, IsAntialias = true, StrokeCap = SKStrokeCap.Round }; canvas.DrawLine(cx-4,cy-4,cx+4,cy+4,xp); canvas.DrawLine(cx+4,cy-4,cx-4,cy+4,xp); }

        _maximizeButtonRect = new SKRect(re - wcW*2, rect.Top, re - wcW, rect.Bottom);
        bool mxh = _maximizeButtonRect.Contains(_lastMouseX, _lastMouseY);
        if (mxh) canvas.DrawRect(_maximizeButtonRect, _btnHover);
        { float cx = _maximizeButtonRect.MidX, cy = midY; using var bp = new SKPaint { Color = ThemeManager.TextSecondary, StrokeWidth = 1.2f, IsAntialias = true, Style = SKPaintStyle.Stroke }; canvas.DrawRect(cx-4,cy-3,8,7,bp); }

        _minimizeButtonRect = new SKRect(re - wcW*3, rect.Top, re - wcW*2, rect.Bottom);
        bool mnh = _minimizeButtonRect.Contains(_lastMouseX, _lastMouseY);
        if (mnh) canvas.DrawRect(_minimizeButtonRect, _btnHover);
        { float cx = _minimizeButtonRect.MidX, cy = midY; using var lp = new SKPaint { Color = ThemeManager.TextSecondary, StrokeWidth = 1.2f, IsAntialias = true }; canvas.DrawLine(cx-4,cy,cx+4,cy,lp); }

        _dragAreaRect = new SKRect(x, rect.Top, re - wcW*3, rect.Bottom);
        
        // 6. Dropdown Lists
        if (dropdowns != null) { foreach (var dd in dropdowns) { if (dd.IsOpen) RenderDropdownList(canvas, dd); } }
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
