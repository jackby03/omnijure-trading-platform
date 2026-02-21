using System;
using System.Collections.Generic;
using Silk.NET.Input;
using Silk.NET.Maths;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual.Shared.UI.Input;

/// <summary>
/// Mediates raw input events from Silk.NET and dispatches them to the appropriate UI components.
/// </summary>
public class GlobalInputManager
{
    public Action<float, float> HandleToolbarClick { get; set; } = (_, _) => { };
    public Action<float, float, float> HandlePanelScroll { get; set; } = (_, _, _) => { };
    public Action<string, string> SwitchContext { get; set; } = (_, _) => { };
    public Action<string> HandleSecondaryToolbarAction { get; set; } = _ => { };
    public Action HandleDrawingToolClick { get; set; } = () => { };
    public Func<Vector2D<int>> GetWindowSize { get; set; } = () => default;
    public Func<Vector2D<int>> GetWindowPosition { get; set; } = () => default;

    public bool IsDragging { get; set; }
    public bool IsResizingPrice { get; set; }
    
    // Extracted state for mouse interactions
    public Vector2D<float> MousePos { get; private set; }
    public Vector2D<float> LastMousePos { get; private set; }

    // Dependencies
    private readonly LayoutManager _layout;
    private readonly ChartTabManager _chartTabs;
    private readonly ToolbarRenderer _toolbar;
    private readonly UiSettingsModal _settingsModal;
    private readonly SettingsModalRenderer _settingsModalRenderer;
    private readonly UiSearchModal _searchModal;
    private readonly UiSearchBox _searchBox;
    private readonly UiDropdown _assetDropdown;
    private readonly List<UiDropdown> _uiDropdowns;
    private readonly List<UiButton> _uiButtons;

    public GlobalInputManager(
        LayoutManager layout,
        ChartTabManager chartTabs,
        ToolbarRenderer toolbar,
        UiSettingsModal settingsModal,
        SettingsModalRenderer settingsModalRenderer,
        UiSearchModal searchModal,
        UiSearchBox searchBox,
        UiDropdown assetDropdown,
        List<UiDropdown> uiDropdowns,
        List<UiButton> uiButtons)
    {
        _layout = layout;
        _chartTabs = chartTabs;
        _toolbar = toolbar;
        _settingsModal = settingsModal;
        _settingsModalRenderer = settingsModalRenderer;
        _searchModal = searchModal;
        _searchBox = searchBox;
        _assetDropdown = assetDropdown;
        _uiDropdowns = uiDropdowns;
        _uiButtons = uiButtons;
    }

    public void OnMouseDown(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            if (_settingsModal != null && _settingsModal.IsVisible)
            {
                var size = GetWindowSize();
                _settingsModalRenderer.HandleMouseDown(MousePos.X, MousePos.Y, size.X, size.Y, _settingsModal);
                return;
            }

            if (_searchModal != null && _searchModal.IsVisible)
            {
                var size = GetWindowSize();
                float modalWidth = Math.Min(600, size.X - 80);
                float modalHeight = Math.Min(700, size.Y - 100);
                float modalX = (size.X - modalWidth) / 2;
                float modalY = (size.Y - modalHeight) / 2 - 50;
                
                if (MousePos.X >= modalX && MousePos.X <= modalX + modalWidth &&
                    MousePos.Y >= modalY && MousePos.Y <= modalY + modalHeight)
                {
                    float searchBoxY = modalY + 84; 
                    float resultsY = searchBoxY + 40; 
                    
                    if (MousePos.Y > resultsY)
                    {
                        int index = (int)((MousePos.Y - resultsY) / 48); // itemHeight is 48
                        var results = _searchModal.GetVisibleResults();
                        if (index >= 0 && index < results.Count)
                        {
                            var clickedSym = results[index].Symbol;
                            SwitchContext?.Invoke(clickedSym, _chartTabs.ActiveTab.Timeframe);
                            _searchModal.IsVisible = false;
                            _searchModal.Clear();
                        }
                    }
                }
                else
                {
                    _searchModal.IsVisible = false;
                    _searchModal.Clear();
                }
                return;
            }

            // Toolbar interactions (e.g., drag handle, minimize, close)
            var pos = GetWindowPosition();
            if (_toolbar.HandleMouseDown(MousePos.X, MousePos.Y, pos.X, pos.Y))
                return;

            if (_searchBox != null)
            {
                _searchBox.IsFocused = _searchBox.Contains(MousePos.X, MousePos.Y);
                if (_searchBox.IsFocused) return;
            }

            UiDropdown? clickedDd = null;
            foreach (var dd in _uiDropdowns)
            {
                if (dd.Contains(MousePos.X, MousePos.Y))
                    clickedDd = dd;
            }

            // Check if clicked inside dropdown menu item area
            foreach (var dd in _uiDropdowns)
            {
                if (dd.IsOpen)
                {
                    var filtered = dd.GetFilteredItems();
                    for (int i = 0; i < filtered.Count; i++)
                    {
                        if (dd.ContainsItem(MousePos.X, MousePos.Y, i))
                        {
                            dd.SelectedItem = filtered[i];
                            dd.OnSelected?.Invoke(dd.SelectedItem);
                            dd.IsOpen = false;
                            dd.SearchQuery = "";
                            dd.ScrollOffset = 0;
                            return;
                        }
                    }
                }
            }

            if (clickedDd != null)
            {
                clickedDd.IsOpen = !clickedDd.IsOpen;
                if (clickedDd.IsOpen) { clickedDd.SearchQuery = ""; clickedDd.ScrollOffset = 0; }
                foreach (var other in _uiDropdowns) if (other != clickedDd) other.IsOpen = false;
                return;
            }

            // Close all if clicked away
            foreach (var dd in _uiDropdowns)
            {
                if (dd.IsOpen) { dd.IsOpen = false; dd.SearchQuery = ""; dd.ScrollOffset = 0; }
            }

            foreach (var btn in _uiButtons)
            {
                if (btn.Contains(MousePos.X, MousePos.Y))
                {
                    btn.Action?.Invoke();
                    return;
                }
            }

            // Secondary toolbar click
            var secondaryBtnId = _layout.HandleSecondaryToolbarClick(MousePos.X, MousePos.Y);
            if (secondaryBtnId != null)
            {
                HandleSecondaryToolbarAction?.Invoke(secondaryBtnId);
                return;
            }

            // Script editor click
            if (_layout.InputHandler.HandleScriptEditorClick(MousePos.X, MousePos.Y))
                return;

            if (_layout.InputHandler.IsScriptEditorFocused)
                _layout.InputHandler.IsScriptEditorFocused = false;

            // Chart tab bar click
            if (_layout.HandleChartTabClick(MousePos.X, MousePos.Y))
                return;

            // Check for left toolbar (drawing tools) click
            var clickedTool = _layout.HandleToolbarClick(MousePos.X, MousePos.Y);
            if (clickedTool.HasValue)
            {
                _chartTabs.ActiveTab.DrawingState.ActiveTool = clickedTool.Value;
                return;
            }

            if (_layout.GetPriceAxisRect().Contains(MousePos.X, MousePos.Y))
            {
                IsResizingPrice = true;
                _chartTabs.ActiveTab.AutoScaleY = false;
                return;
            }

            if (_layout.ChartRect.Contains(MousePos.X, MousePos.Y) && _chartTabs.ActiveTab.DrawingState.ActiveTool != Omnijure.Visual.Shared.Lib.Drawing.DrawingTool.None)
            {
                HandleDrawingToolClick?.Invoke();
                return;
            }

            if (_layout.ChartRect.Contains(MousePos.X, MousePos.Y))
            {
                IsDragging = true;
                return;
            }

            // Panel system handled logic
            _layout.HandleMouseDown(MousePos.X, MousePos.Y);
        }
        else if (button == MouseButton.Right)
        {
            var activeTab = _chartTabs.ActiveTab;
            if (activeTab.DrawingState.CurrentDrawing != null)
            {
                activeTab.DrawingState.CurrentDrawing = null;
            }
            else
            {
                activeTab.DrawingState.ActiveTool = Omnijure.Visual.Shared.Lib.Drawing.DrawingTool.None;
            }
        }
    }

    public void OnMouseMove(IMouse mouse, System.Numerics.Vector2 position)
    {
        LastMousePos = MousePos;
        MousePos = new Vector2D<float>(position.X, position.Y);

        if (_settingsModal != null && _settingsModal.IsVisible)
        {
            var size = GetWindowSize();
            _settingsModalRenderer.HandleMouseMove(MousePos.X, MousePos.Y, size.X, size.Y, _settingsModal);
            return;
        }

        if (_searchModal != null && _searchModal.IsVisible)
        {
            return;
        }

        _layout.UpdateSecondaryToolbarMouse(MousePos.X, MousePos.Y);

        if (_layout.IsDraggingPanel || _layout.IsResizingPanel)
        {
            var size = GetWindowSize();
            float dx = MousePos.X - LastMousePos.X;
            _layout.HandleMouseMove(MousePos.X, MousePos.Y, dx, size.X, size.Y);
            return;
        }

        if (IsDragging)
        {
            float dx = MousePos.X - LastMousePos.X;
            if (dx != 0)
            {
                _chartTabs.ActiveTab.ScrollOffset += (int)dx;
            }
            return;
        }

        if (IsResizingPrice)
        {
            float dy = MousePos.Y - LastMousePos.Y;
            if (dy != 0)
            {
                var tab = _chartTabs.ActiveTab;
                float height = _layout.ChartRect.Height;
                float priceRange = tab.ViewMaxY - tab.ViewMinY;
                float pricePerPixel = priceRange / height;
                
                float zoomDelta = dy * pricePerPixel * 0.5f;
                tab.ViewMinY -= zoomDelta;
                tab.ViewMaxY += zoomDelta;

                if (tab.ViewMaxY <= tab.ViewMinY) tab.ViewMaxY = tab.ViewMinY + 0.0001f;
            }
            return;
        }
    }

    public void OnMouseUp(IMouse mouse, MouseButton button)
    {
        if (button == MouseButton.Left)
        {
            IsDragging = false;
            IsResizingPrice = false;
            _layout.HandleMouseUp();
        }
    }

    public void OnScroll(IMouse mouse, ScrollWheel arg2)
    {
        if (_settingsModal != null && _settingsModal.IsVisible)
        {
            return;
        }

        if (_searchModal != null && _searchModal.IsVisible)
        {
            _searchModal.ScrollOffset = (int)Math.Max(0, _searchModal.ScrollOffset - arg2.Y * 3f);
            return;
        }

        var openDd = System.Linq.Enumerable.FirstOrDefault(_uiDropdowns, d => d.IsOpen);
        if (openDd != null)
        {
            float delta = -arg2.Y;
            var filtered = openDd.GetFilteredItems();
            openDd.ScrollOffset = Math.Max(0, Math.Min(filtered.Count - openDd.MaxVisibleItems, openDd.ScrollOffset + delta));
            return;
        }

        if (_layout.InputHandler.HandlePanelScroll(MousePos.X, MousePos.Y, arg2.Y))
            return;

        var tab = _chartTabs.ActiveTab;
        if (_layout.GetPriceAxisRect().Contains(MousePos.X, MousePos.Y))
        {
            tab.AutoScaleY = false;
            float factor = arg2.Y > 0 ? 0.9f : 1.1f;
            float mid = (tab.ViewMinY + tab.ViewMaxY) / 2.0f;
            float range = (tab.ViewMaxY - tab.ViewMinY) * factor;
            if (range < 0.00001f) range = 0.00001f;
            tab.ViewMinY = mid - range / 2.0f;
            tab.ViewMaxY = mid + range / 2.0f;
            return;
        }

        if (arg2.Y > 0)
        {
            tab.Zoom *= 1.1f;
            if (tab.Zoom > 10.0f) tab.Zoom = 10.0f;
        }
        else if (arg2.Y < 0)
        {
            tab.Zoom *= 0.9f;
            if (tab.Zoom < 0.1f) tab.Zoom = 0.1f;
        }
    }

    public void OnKeyChar(IKeyboard arg1, char arg2)
    {
        if (_settingsModal != null && _settingsModal.IsVisible)
        {
            if (char.IsControl(arg2)) return;
            _settingsModal.FocusedInput?.AddChar(arg2);
            _settingsModal.HasUnsavedChanges = true;
            return;
        }

        if (_searchModal != null && _searchModal.IsVisible)
        {
            if (char.IsControl(arg2)) return;
            _searchModal.AddChar(arg2);
            return;
        }
        
        if (_searchBox != null && _searchBox.IsFocused)
        {
            if (char.IsLetterOrDigit(arg2) || char.IsWhiteSpace(arg2))
            {
                _searchBox.AddChar(arg2);
                if (_assetDropdown != null)
                    _assetDropdown.SearchQuery = _searchBox.Text;
            }
            return;
        }
        
        if (_layout.InputHandler.IsScriptEditorFocused)
        {
            if (!char.IsControl(arg2))
            {
                _layout.InputHandler.ScriptEditorInsertChar(arg2);
            }
            return;
        }

        var openDd = System.Linq.Enumerable.FirstOrDefault(_uiDropdowns, d => d.IsOpen);
        if (openDd != null)
            openDd.SearchQuery += arg2;
    }

    public void OnKeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        // Ctrl+, -> settings modal
        if (arg1.IsKeyPressed(Key.ControlLeft) && arg2 == Key.Comma)
        {
            if (_settingsModal != null)
            {
                // Requires the original settings instance to load. Will handle later or pass settings model.
            }
            return;
        }

        if (_settingsModal != null && _settingsModal.IsVisible)
        {
            switch (arg2)
            {
                case Key.Escape: _settingsModal.Close(); break;
                case Key.Backspace: _settingsModal.FocusedInput?.Backspace(); break;
                case Key.Delete: _settingsModal.FocusedInput?.Delete(); break;
                case Key.Tab: _settingsModalRenderer.FocusNextInput(_settingsModal); break;
            }
            return;
        }

        if (arg1.IsKeyPressed(Key.ControlLeft) && arg2 == Key.K)
        {
            if (_searchModal != null)
            {
                _searchModal.IsVisible = true;
                _searchModal.Clear();
            }
            return;
        }
        
        if (_searchModal != null && _searchModal.IsVisible)
        {
            switch (arg2)
            {
                case Key.Escape:
                    _searchModal.IsVisible = false;
                    _searchModal.Clear();
                    break;
                case Key.Backspace: _searchModal.Backspace(); break;
                case Key.Up: _searchModal.MoveSelectionUp(); break;
                case Key.Down: _searchModal.MoveSelectionDown(); break;
                case Key.Enter:
                    var selected = _searchModal.GetSelectedSymbol();
                    if (!string.IsNullOrEmpty(selected))
                    {
                        SwitchContext?.Invoke(selected, _chartTabs.ActiveTab.Timeframe);
                        _searchModal.IsVisible = false;
                        _searchModal.Clear();
                    }
                    break;
            }
            return;
        }
        
        if (_searchBox != null && _searchBox.IsFocused)
        {
            if (arg2 == Key.Backspace)
            {
                _searchBox.Backspace();
                if (_assetDropdown != null) _assetDropdown.SearchQuery = _searchBox.Text;
            }
            else if (arg2 == Key.Escape)
            {
                _searchBox.IsFocused = false;
                _searchBox.Clear();
                if (_assetDropdown != null) _assetDropdown.SearchQuery = "";
            }
            else if (arg2 == Key.Enter && _assetDropdown != null)
            {
                var filtered = _assetDropdown.GetFilteredItems();
                if (filtered.Count > 0)
                {
                    SwitchContext?.Invoke(filtered[0], _chartTabs.ActiveTab.Timeframe);
                    _searchBox.Clear();
                    _searchBox.IsFocused = false;
                    _assetDropdown.SearchQuery = "";
                }
            }
            return;
        }
        
        var openDd = System.Linq.Enumerable.FirstOrDefault(_uiDropdowns, d => d.IsOpen);
        if (openDd != null)
        {
            if (arg2 == Key.Escape) openDd.IsOpen = false;
            else if (arg2 == Key.Backspace && openDd.SearchQuery.Length > 0)
                openDd.SearchQuery = openDd.SearchQuery[..^1];
            return;
        }

        if (_layout.InputHandler.IsScriptEditorFocused)
        {
            bool ctrl = arg1.IsKeyPressed(Key.ControlLeft) || arg1.IsKeyPressed(Key.ControlRight);
            if (ctrl)
            {
                if (arg2 == Key.S || arg2 == Key.N || arg2 == Key.T)
                    goto GlobalShortcuts;
            }

            switch (arg2)
            {
                case Key.Escape: _layout.InputHandler.IsScriptEditorFocused = false; return;
                case Key.Backspace: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Backspace); return;
                case Key.Delete: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Delete); return;
                case Key.Enter: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Enter); return;
                case Key.Left: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Left); return;
                case Key.Right: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Right); return;
                case Key.Up: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Up); return;
                case Key.Down: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Down); return;
                case Key.Home: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Home); return;
                case Key.End: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.End); return;
                case Key.Tab: _layout.InputHandler.ScriptEditorHandleKey(PanelContentRenderer.EditorKey.Tab); return;
            }
            if (arg2 == Key.F5) { HandleSecondaryToolbarAction?.Invoke("script_run"); return; }
            return;
        }

        GlobalShortcuts:
        var activeTab = _chartTabs.ActiveTab;
        if (arg2 == Key.Space) { activeTab.ScrollOffset = 0; activeTab.Zoom = 1.0f; }
        if (arg2 == Key.Delete && activeTab.DrawingState.Objects.Count > 0)
            activeTab.DrawingState.Objects.RemoveAt(activeTab.DrawingState.Objects.Count - 1);

        if (arg2 == Key.Number1) SwitchContext?.Invoke(activeTab.Symbol, "1m");
        if (arg2 == Key.Number2) SwitchContext?.Invoke(activeTab.Symbol, "5m");
        if (arg2 == Key.Number3) SwitchContext?.Invoke(activeTab.Symbol, "15m");
        if (arg2 == Key.Number4) SwitchContext?.Invoke(activeTab.Symbol, "1h");
        if (arg2 == Key.Number5) SwitchContext?.Invoke(activeTab.Symbol, "4h");
        if (arg2 == Key.Number6) SwitchContext?.Invoke(activeTab.Symbol, "1d");

        if (arg2 == Key.F5) HandleSecondaryToolbarAction?.Invoke("script_run");
        if (arg2 == Key.F11) HandleSecondaryToolbarAction?.Invoke("chart_fullscreen");

        if (arg1.IsKeyPressed(Key.ControlLeft) || arg1.IsKeyPressed(Key.ControlRight))
        {
            if (arg2 == Key.N) HandleSecondaryToolbarAction?.Invoke("script_new");
            if (arg2 == Key.S) HandleSecondaryToolbarAction?.Invoke("script_save");
            if (arg2 == Key.T) HandleSecondaryToolbarAction?.Invoke("script_toggle");
        }

        if (arg2 == Key.F1) SwitchContext?.Invoke("BTCUSDT", activeTab.Timeframe);
        if (arg2 == Key.F2) SwitchContext?.Invoke("ETHUSDT", activeTab.Timeframe);
        if (arg2 == Key.F3) SwitchContext?.Invoke("SOLUSDT", activeTab.Timeframe);
        if (arg2 == Key.F4) SwitchContext?.Invoke("XRPUSDT", activeTab.Timeframe);
    }
}
