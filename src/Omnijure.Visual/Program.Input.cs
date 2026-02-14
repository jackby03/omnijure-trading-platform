using System;
using Silk.NET.Maths;
using Silk.NET.Input;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual;

/// <summary>
/// Input handling: keyboard, mouse, scroll
/// </summary>
public static partial class Program
{
    private static void OnKeyChar(IKeyboard arg1, char arg2)
    {
        // Settings modal text input
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
        
        var openDd = System.Linq.Enumerable.FirstOrDefault(_uiDropdowns, d => d.IsOpen);
        if (openDd != null)
            openDd.SearchQuery += arg2;
    }

    private static void OnKeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        // Ctrl+, -> settings modal
        if (arg1.IsKeyPressed(Key.ControlLeft) && arg2 == Key.Comma)
        {
            if (_settingsModal != null)
            {
                _settingsModal.LoadFromSettings(_settings.Current);
                _settingsModal.Open();
            }
            return;
        }

        // Settings modal keys
        if (_settingsModal != null && _settingsModal.IsVisible)
        {
            switch (arg2)
            {
                case Key.Escape:
                    _settingsModal.Close();
                    break;
                case Key.Backspace:
                    _settingsModal.FocusedInput?.Backspace();
                    break;
                case Key.Delete:
                    _settingsModal.FocusedInput?.Delete();
                    break;
                case Key.Tab:
                    _settingsModalRenderer.FocusNextInput(_settingsModal);
                    break;
            }
            return;
        }

        // Ctrl+K -> search modal
        if (arg1.IsKeyPressed(Key.ControlLeft) && arg2 == Key.K)
        {
            if (_searchModal != null)
            {
                _searchModal.IsVisible = true;
                _searchModal.Clear();
            }
            return;
        }
        
        // Search modal keys
        if (_searchModal != null && _searchModal.IsVisible)
        {
            switch (arg2)
            {
                case Key.Escape:
                    _searchModal.IsVisible = false;
                    _searchModal.Clear();
                    break;
                case Key.Backspace:
                    _searchModal.Backspace();
                    break;
                case Key.Up:
                    _searchModal.MoveSelectionUp();
                    break;
                case Key.Down:
                    _searchModal.MoveSelectionDown();
                    break;
                case Key.Enter:
                    var selected = _searchModal.GetSelectedSymbol();
                    if (!string.IsNullOrEmpty(selected))
                    {
                        SwitchContext(selected, _chartTabs.ActiveTab.Timeframe);
                        _searchModal.IsVisible = false;
                        _searchModal.Clear();
                    }
                    break;
            }
            return;
        }
        
        // Search box keys
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
                    SwitchContext(filtered[0], _chartTabs.ActiveTab.Timeframe);
                    _searchBox.Clear();
                    _searchBox.IsFocused = false;
                    _assetDropdown.SearchQuery = "";
                }
            }
            return;
        }
        
        // Dropdown keys
        var openDd = System.Linq.Enumerable.FirstOrDefault(_uiDropdowns, d => d.IsOpen);
        if (openDd != null)
        {
            if (arg2 == Key.Escape) openDd.IsOpen = false;
            else if (arg2 == Key.Backspace && openDd.SearchQuery.Length > 0)
                openDd.SearchQuery = openDd.SearchQuery[..^1];
            return;
        }

        // Global shortcuts
        var activeTab = _chartTabs.ActiveTab;
        if (arg2 == Key.Space) { activeTab.ScrollOffset = 0; activeTab.Zoom = 1.0f; }
        if (arg2 == Key.Delete && activeTab.DrawingState.Objects.Count > 0)
            activeTab.DrawingState.Objects.RemoveAt(activeTab.DrawingState.Objects.Count - 1);

        // Timeframe shortcuts
        if (arg2 == Key.Number1) SwitchContext(activeTab.Symbol, "1m");
        if (arg2 == Key.Number2) SwitchContext(activeTab.Symbol, "5m");
        if (arg2 == Key.Number3) SwitchContext(activeTab.Symbol, "15m");

        // Asset shortcuts
        if (arg2 == Key.F1) SwitchContext("BTCUSDT", activeTab.Timeframe);
        if (arg2 == Key.F2) SwitchContext("ETHUSDT", activeTab.Timeframe);
        if (arg2 == Key.F3) SwitchContext("SOLUSDT", activeTab.Timeframe);
        if (arg2 == Key.F4) SwitchContext("XRPUSDT", activeTab.Timeframe);
    }
    
    private static void OnResize(Vector2D<int> size)
    {
        // Enforce minimum size – clamp and push back to the window if needed
        // (Also enforced natively via WM_GETMINMAXINFO in ToolbarRenderer)
        int w = Math.Max(size.X, ToolbarRenderer.MinWindowWidth);
        int h = Math.Max(size.Y, ToolbarRenderer.MinWindowHeight);
        if (w != size.X || h != size.Y)
        {
            _window.Size = new Vector2D<int>(w, h);
            return; // The forced resize will re-trigger OnResize with valid size
        }

        _gl?.Viewport(size);
        CreateSurface(size);
    }
}
