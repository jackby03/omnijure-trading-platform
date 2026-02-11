


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

    // Menu items (VS-style)
    private static readonly string[] MenuItems = ["File", "Edit", "View", "Trading", "Tools", "Help"];
    
    // Menu dropdown state
    private string? _openMenu;
    private Action<string>? _onTogglePanel;
    private Func<string, bool>? _isPanelVisible;
    private readonly List<(string label, SKRect rect)> _menuItemRects = new();
    private readonly List<(string id, string label, SKRect rect)> _submenuItemRects = new();
    private SKRect _submenuBounds;
    
    public void SetPanelCallbacks(Action<string> onTogglePanel, Func<string, bool> isPanelVisible)
    {
        _onTogglePanel = onTogglePanel;
        _isPanelVisible = isPanelVisible;
    }
    
    // Win32 P/Invoke
    [DllImport("user32.dll")] private static extern bool GetCursorPos(out POINT point);
    [DllImport("dwmapi.dll")] private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int value, int size);
    [DllImport("user32.dll")] private static extern bool ReleaseCapture();
    [DllImport("user32.dll")] private static extern IntPtr SendMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern IntPtr LoadCursor(IntPtr hInstance, int lpCursorName);
    [DllImport("user32.dll")] private static extern IntPtr SetCursor(IntPtr hCursor);
    [DllImport("user32.dll")] private static extern IntPtr SetWindowLongPtr(IntPtr hWnd, int nIndex, IntPtr dwNewLong);
    [DllImport("user32.dll")] private static extern IntPtr GetWindowLongPtr(IntPtr hWnd, int nIndex);
    [DllImport("user32.dll")] private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    [DllImport("user32.dll")] private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);
    [DllImport("user32.dll")] private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

    private const int GWL_WNDPROC = -4;
    private const int GWL_STYLE   = -16;
    private const uint WM_GETMINMAXINFO = 0x0024;
    private const uint WM_NCCALCSIZE    = 0x0083;
    private const uint WM_NCHITTEST     = 0x0084;

    // Window styles needed for native resize
    private const int WS_THICKFRAME  = 0x00040000;
    private const int WS_MAXIMIZEBOX = 0x00010000;

    // SetWindowPos flags
    private const uint SWP_NOMOVE     = 0x0002;
    private const uint SWP_NOSIZE     = 0x0001;
    private const uint SWP_NOZORDER   = 0x0004;
    private const uint SWP_FRAMECHANGED = 0x0020;

    [StructLayout(LayoutKind.Sequential)]
    private struct MINMAXINFO
    {
        public POINT ptReserved;
        public POINT ptMaxSize;
        public POINT ptMaxPosition;
        public POINT ptMinTrackSize;
        public POINT ptMaxTrackSize;
    }

    // Min window size constants
    public const int MinWindowWidth  = 960;
    public const int MinWindowHeight = 540;

    private static IntPtr _prevWndProc;
    // Must keep a reference to the delegate to prevent GC collection
    private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
    private static WndProcDelegate? _wndProcDelegate;

    // Standard cursor IDs
    private const int IDC_ARROW    = 32512;
    private const int IDC_SIZEWE   = 32644; // horizontal resize (left-right)
    private const int IDC_SIZENS   = 32645; // vertical resize (up-down)
    private const int IDC_SIZENWSE = 32642; // diagonal resize (top-left / bottom-right)
    private const int IDC_SIZENESW = 32643; // diagonal resize (top-right / bottom-left)

    [StructLayout(LayoutKind.Sequential)]
    private struct POINT { public int X; public int Y; }

    private const int DWMWA_WINDOW_CORNER_PREFERENCE = 33;
    private const int DWMWCP_ROUND = 2;

    // WM_NCLBUTTONDOWN lets Windows handle native resize/move
    private const uint WM_NCLBUTTONDOWN = 0x00A1;
    // Hit-test values for resize edges/corners
    private const int HTLEFT        = 10;
    private const int HTRIGHT       = 11;
    private const int HTTOP         = 12;
    private const int HTTOPLEFT     = 13;
    private const int HTTOPRIGHT    = 14;
    private const int HTBOTTOM      = 15;
    private const int HTBOTTOMLEFT  = 16;
    private const int HTBOTTOMRIGHT = 17;

    /// <summary>
    /// Width of the invisible resize grab zone at each edge of the window.
    /// </summary>
    private const float ResizeGripSize = 6f;

    // Window control state
    private Action? _onClose;
    private Action? _onMinimize;
    private Action? _onMaximize;
    private Action<int, int>? _onWindowMove;
    private IntPtr _hwnd;
    private SKRect _closeButtonRect;
    private SKRect _maximizeButtonRect;
    private SKRect _minimizeButtonRect;
    private SKRect _dragAreaRect;
    private bool _isDraggingWindow;
    private POINT _dragStartCursor;
    private (int X, int Y) _dragStartWindowPos;

    // Resize cursor state
    private int _currentResizeCursor; // 0 = none, HTLEFT/HTRIGHT/etc.

    // Store last known size for WM_NCHITTEST calculation
    private static int _lastScreenWidth;
    private static int _lastScreenHeight;

    public void SetWindowActions(Action onClose, Action onMinimize, Action onMaximize,
        Action<int, int> onWindowMove, IntPtr windowHandle)
    {
        _onClose = onClose;
        _onMinimize = onMinimize;
        _onMaximize = onMaximize;
        _onWindowMove = onWindowMove;
        _hwnd = windowHandle;

        // Windows 11 rounded corners
        int preference = DWMWCP_ROUND;
        DwmSetWindowAttribute(windowHandle, DWMWA_WINDOW_CORNER_PREFERENCE, ref preference, sizeof(int));

        // Inject WS_THICKFRAME so Windows allows native resize on a borderless window.
        // WS_MAXIMIZEBOX enables Win+Arrow snap behaviour.
        IntPtr style = GetWindowLongPtr(windowHandle, GWL_STYLE);
        style = (IntPtr)((long)style | WS_THICKFRAME | WS_MAXIMIZEBOX);
        SetWindowLongPtr(windowHandle, GWL_STYLE, style);

        // Force Windows to re-read the style change
        SetWindowPos(windowHandle, IntPtr.Zero, 0, 0, 0, 0,
            SWP_NOMOVE | SWP_NOSIZE | SWP_NOZORDER | SWP_FRAMECHANGED);

        // Subclass the window procedure to:
        //  - WM_NCCALCSIZE: return 0 to keep full client area (no visible border)
        //  - WM_NCHITTEST:  report resize edges so Windows handles drag-resize
        //  - WM_GETMINMAXINFO: enforce minimum window size
        _wndProcDelegate = WndProc;
        _prevWndProc = GetWindowLongPtr(windowHandle, GWL_WNDPROC);
        SetWindowLongPtr(windowHandle, GWL_WNDPROC, Marshal.GetFunctionPointerForDelegate(_wndProcDelegate));
    }

    /// <summary>
    /// Must be called every frame so the WndProc knows the current window dimensions.
    /// </summary>
    public void UpdateWindowSize(int w, int h)
    {
        _lastScreenWidth = w;
        _lastScreenHeight = h;
    }

    private static IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
    {
        switch (msg)
        {
            // Remove the non-client area so the window stays fully borderless
            case WM_NCCALCSIZE:
                if (wParam != IntPtr.Zero)
                    return IntPtr.Zero;        // returning 0 = entire window is client area
                break;

            // Tell Windows where the resize edges are
            case WM_NCHITTEST:
            {
                // lParam: low word = screen X, high word = screen Y
                // We need client-relative coords → use ScreenToClient, but simpler:
                // just fall through to DefWindowProc and fix up if needed.
                IntPtr defHit = DefWindowProc(hWnd, msg, wParam, lParam);
                int defVal = (int)defHit;

                // If DefWindowProc already detected an edge (it knows from WS_THICKFRAME), use it
                if (defVal >= HTLEFT && defVal <= HTBOTTOMRIGHT)
                    return defHit;

                // Otherwise check ourselves with pixel coords (DefWindowProc might say HTCLIENT)
                break;
            }

            // Enforce minimum window size
            case WM_GETMINMAXINFO:
                if (lParam != IntPtr.Zero)
                {
                    var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                    mmi.ptMinTrackSize = new POINT { X = MinWindowWidth, Y = MinWindowHeight };
                    Marshal.StructureToPtr(mmi, lParam, false);
                    return IntPtr.Zero;
                }
                break;
        }
        return CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
    }

    /// <summary>
    /// Returns the NCHITTEST value for the given position, or 0 if not on a resize edge.
    /// </summary>
    public int HitTestResize(float x, float y, int screenWidth, int screenHeight)
    {
        bool left   = x <= ResizeGripSize;
        bool right  = x >= screenWidth - ResizeGripSize;
        bool top    = y <= ResizeGripSize;
        bool bottom = y >= screenHeight - ResizeGripSize;

        if (top && left)     return HTTOPLEFT;
        if (top && right)    return HTTOPRIGHT;
        if (bottom && left)  return HTBOTTOMLEFT;
        if (bottom && right) return HTBOTTOMRIGHT;
        if (left)            return HTLEFT;
        if (right)           return HTRIGHT;
        if (top)             return HTTOP;
        if (bottom)          return HTBOTTOM;
        return 0;
    }

    /// <summary>
    /// If the mouse is on a resize edge, initiates native Win32 resize and returns true.
    /// </summary>
    public bool TryStartResize(float x, float y, int screenWidth, int screenHeight)
    {
        int ht = HitTestResize(x, y, screenWidth, screenHeight);
        if (ht == 0 || _hwnd == IntPtr.Zero) return false;

        ReleaseCapture();
        SendMessage(_hwnd, WM_NCLBUTTONDOWN, (IntPtr)ht, IntPtr.Zero);
        return true;
    }

    /// <summary>
    /// Returns the current resize cursor hit-test value for cursor rendering.
    /// Updated every mouse move. 0 = no resize edge.
    /// </summary>
    public int CurrentResizeCursor => _currentResizeCursor;

    /// <summary>
    /// Updates the system cursor to match the current resize edge (or restores arrow).
    /// </summary>
    private void ApplyResizeCursor()
    {
        int cursorId = _currentResizeCursor switch
        {
            HTLEFT or HTRIGHT                  => IDC_SIZEWE,
            HTTOP or HTBOTTOM                  => IDC_SIZENS,
            HTTOPLEFT or HTBOTTOMRIGHT         => IDC_SIZENWSE,
            HTTOPRIGHT or HTBOTTOMLEFT         => IDC_SIZENESW,
            _                                  => IDC_ARROW
        };
        SetCursor(LoadCursor(IntPtr.Zero, cursorId));
    }

    public bool HandleMouseDown(float x, float y, int windowX, int windowY)
    {
        if (_closeButtonRect.Contains(x, y)) { _onClose?.Invoke(); return true; }
        if (_maximizeButtonRect.Contains(x, y)) { _onMaximize?.Invoke(); return true; }
        if (_minimizeButtonRect.Contains(x, y)) { _onMinimize?.Invoke(); return true; }
        
        // Submenu item click
        if (_openMenu != null)
        {
            foreach (var (id, label, rect) in _submenuItemRects)
            {
                if (rect.Contains(x, y))
                {
                    if (id.StartsWith("toggle:")) _onTogglePanel?.Invoke(id[7..]);
                    _openMenu = null;
                    return true;
                }
            }
        }
        
        // Menu bar click � toggle menu
        foreach (var (label, rect) in _menuItemRects)
        {
            if (rect.Contains(x, y))
            {
                _openMenu = _openMenu == label ? null : label;
                return true;
            }
        }
        
        // Click outside closes menu
        if (_openMenu != null) { _openMenu = null; return true; }
        
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
        
        // Logo
        float logoSize = rect.Height - 8;
        SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Logo, 
            x + 2, rect.Top + 4, logoSize, new SKColor(56, 139, 253));
        x += logoSize + 8;
        
        // 0. Menu Items
        _menuItemRects.Clear();
        foreach (var menuItem in MenuItems)
        {
            float tw = _fontSmall.MeasureText(menuItem);
            float btnW = tw + 14;
            var menuRect = new SKRect(x, rect.Top, x + btnW, rect.Bottom);
            _menuItemRects.Add((menuItem, menuRect));
            bool hover = menuRect.Contains(_lastMouseX, _lastMouseY) || _openMenu == menuItem;
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
        
        // 7. Open Menu Submenus
        if (_openMenu != null) RenderSubmenu(canvas, rect);
    }
    
    private void RenderSubmenu(SKCanvas canvas, SKRect toolbarRect)
    {
        // Find anchor rect for the open menu
        SKRect anchorRect = default;
        foreach (var (label, rect) in _menuItemRects)
        {
            if (label == _openMenu) { anchorRect = rect; break; }
        }
        if (anchorRect.Width <= 0) return;
        
        // Define menu items per menu
        var items = GetMenuItems(_openMenu!);
        if (items.Count == 0) return;
        
        float itemH = 26;
        float menuW = 220;
        float menuX = anchorRect.Left;
        float menuY = anchorRect.Bottom + 2;
        float totalH = items.Count * itemH + 4;
        
        _submenuBounds = new SKRect(menuX, menuY, menuX + menuW, menuY + totalH);
        _submenuItemRects.Clear();
        
        // Shadow + background
        canvas.DrawRect(_submenuBounds, _shadowPaint);
        using var bgPaint = new SKPaint { Color = new SKColor(28, 31, 38), Style = SKPaintStyle.Fill };
        canvas.DrawRoundRect(_submenuBounds, 4, 4, bgPaint);
        using var borderPaint = new SKPaint { Color = new SKColor(50, 55, 65), Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        canvas.DrawRoundRect(_submenuBounds, 4, 4, borderPaint);
        
        float iy = menuY + 2;
        foreach (var (id, label, shortcut, isSep, isChecked) in items)
        {
            if (isSep)
            {
                using var sepP = new SKPaint { Color = new SKColor(50, 55, 65), StrokeWidth = 1 };
                canvas.DrawLine(menuX + 8, iy + itemH / 2, menuX + menuW - 8, iy + itemH / 2, sepP);
                iy += itemH;
                continue;
            }
            
            var itemRect = new SKRect(menuX + 2, iy, menuX + menuW - 2, iy + itemH);
            _submenuItemRects.Add((id, label, itemRect));
            
            bool hovered = itemRect.Contains(_lastMouseX, _lastMouseY);
            if (hovered)
            {
                using var hoverPaint = new SKPaint { Color = new SKColor(45, 50, 65), Style = SKPaintStyle.Fill };
                canvas.DrawRoundRect(itemRect, 3, 3, hoverPaint);
            }
            
            // Checkmark for toggleable items
            if (isChecked)
            {
                using var checkPaint = new SKPaint { Color = new SKColor(56, 139, 253), StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeCap = SKStrokeCap.Round };
                float cx = menuX + 16, cy = iy + itemH / 2;
                canvas.DrawLine(cx - 4, cy, cx - 1, cy + 3, checkPaint);
                canvas.DrawLine(cx - 1, cy + 3, cx + 4, cy - 3, checkPaint);
            }
            
            // Label
            using var labelPaint = new SKPaint { Color = hovered ? ThemeManager.TextWhite : ThemeManager.TextPrimary, IsAntialias = true };
            canvas.DrawText(label, menuX + 30, iy + itemH / 2 + 4, _fontSmall, labelPaint);
            
            // Shortcut
            if (!string.IsNullOrEmpty(shortcut))
            {
                float shortcutW = _fontSmall.MeasureText(shortcut);
                using var shortcutPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
                canvas.DrawText(shortcut, menuX + menuW - shortcutW - 12, iy + itemH / 2 + 4, _fontSmall, shortcutPaint);
            }
            
            iy += itemH;
        }
    }
    
    private List<(string id, string label, string? shortcut, bool isSep, bool isChecked)> GetMenuItems(string menu) => menu switch
    {
        "File" => [
            ("", "New Workspace", "Ctrl+N", false, false),
            ("", "Open Workspace...", "Ctrl+O", false, false),
            ("", "Save Layout", "Ctrl+S", false, false),
            ("", "", null, true, false),
            ("", "Export Data...", null, false, false),
            ("", "Import Strategy...", null, false, false),
            ("", "", null, true, false),
            ("", "Settings", "Ctrl+,", false, false),
            ("", "", null, true, false),
            ("", "Exit", "Alt+F4", false, false),
        ],
        "Edit" => [
            ("", "Undo", "Ctrl+Z", false, false),
            ("", "Redo", "Ctrl+Y", false, false),
            ("", "", null, true, false),
            ("", "Copy Chart", "Ctrl+C", false, false),
            ("", "Screenshot", "Ctrl+Shift+S", false, false),
            ("", "", null, true, false),
            ("", "Clear Drawings", null, false, false),
            ("", "Reset Layout", null, false, false),
        ],
        "View" => [
            ($"toggle:{PanelDefinitions.AI_ASSISTANT}", "AI Assistant", "Ctrl+1", false, _isPanelVisible?.Invoke(PanelDefinitions.AI_ASSISTANT) ?? false),
            ($"toggle:{PanelDefinitions.PORTFOLIO}", "Portfolio", "Ctrl+2", false, _isPanelVisible?.Invoke(PanelDefinitions.PORTFOLIO) ?? false),
            ($"toggle:{PanelDefinitions.ORDERBOOK}", "Order Book", "Ctrl+3", false, _isPanelVisible?.Invoke(PanelDefinitions.ORDERBOOK) ?? false),
            ($"toggle:{PanelDefinitions.TRADES}", "Trades", "Ctrl+4", false, _isPanelVisible?.Invoke(PanelDefinitions.TRADES) ?? false),
            ($"toggle:{PanelDefinitions.POSITIONS}", "Positions", "Ctrl+5", false, _isPanelVisible?.Invoke(PanelDefinitions.POSITIONS) ?? false),
            ("", "", null, true, false),
            ($"toggle:{PanelDefinitions.SCRIPT_EDITOR}", "Script Editor", "Ctrl+E", false, _isPanelVisible?.Invoke(PanelDefinitions.SCRIPT_EDITOR) ?? false),
            ($"toggle:{PanelDefinitions.ALERTS}", "Alerts", "Ctrl+A", false, _isPanelVisible?.Invoke(PanelDefinitions.ALERTS) ?? false),
            ($"toggle:{PanelDefinitions.LOGS}", "Console", "Ctrl+`", false, _isPanelVisible?.Invoke(PanelDefinitions.LOGS) ?? false),
            ("", "", null, true, false),
            ("", "Zoom In", "Ctrl++", false, false),
            ("", "Zoom Out", "Ctrl+-", false, false),
            ("", "Reset Zoom", "Ctrl+0", false, false),
        ],
        "Trading" => [
            ("", "New Order", "F2", false, false),
            ("", "Quick Buy", "Ctrl+B", false, false),
            ("", "Quick Sell", "Ctrl+Shift+B", false, false),
            ("", "", null, true, false),
            ("", "Cancel All Orders", null, false, false),
            ("", "Close All Positions", null, false, false),
            ("", "", null, true, false),
            ("", "Paper Trading", null, false, false),
            ("", "Risk Manager...", null, false, false),
        ],
        "Tools" => [
            ("", "Strategy Builder", null, false, false),
            ("", "Backtester", null, false, false),
            ("", "Indicator Manager", null, false, false),
            ("", "", null, true, false),
            ("", "API Keys...", null, false, false),
            ("", "Notifications...", null, false, false),
            ("", "", null, true, false),
            ("", "Theme", null, false, false),
            ("", "Keyboard Shortcuts", "Ctrl+K", false, false),
        ],
        "Help" => [
            ("", "Documentation", "F1", false, false),
            ("", "Keyboard Shortcuts", null, false, false),
            ("", "API Reference", null, false, false),
            ("", "", null, true, false),
            ("", "Report Issue", null, false, false),
            ("", "Release Notes", null, false, false),
            ("", "", null, true, false),
            ("", "About Omnijure TDS", null, false, false),
        ],
        _ => []
    };
    
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
    
    public void UpdateMousePos(float x, float y, int screenWidth = 0, int screenHeight = 0)
    {
        _lastMouseX = x;
        _lastMouseY = y;

        // Track resize cursor state and update system cursor
        if (screenWidth > 0 && screenHeight > 0)
            _currentResizeCursor = HitTestResize(x, y, screenWidth, screenHeight);
        else
            _currentResizeCursor = 0;
        ApplyResizeCursor();

        // Hover-to-switch menus (when one is open, hovering another opens it)
        if (_openMenu != null)
        {
            foreach (var (label, rect) in _menuItemRects)
            {
                if (rect.Contains(x, y) && label != _openMenu)
                {
                    _openMenu = label;
                    break;
                }
            }
        }
    }
}
