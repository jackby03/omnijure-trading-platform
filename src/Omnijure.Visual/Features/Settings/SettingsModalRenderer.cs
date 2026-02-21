using Omnijure.Core.Features.Settings.Api;
using Omnijure.Core.Features.Settings.Model;
using SkiaSharp;
using System;
using Omnijure.Visual.Shared.UI.Rendering;
using System.Collections.Generic;

namespace Omnijure.Visual.Features.Settings;

public class SettingsModalRenderer
{
    // Paints
    private readonly SKPaint _overlayPaint;
    private readonly SKPaint _modalBg;
    private readonly SKPaint _sidebarBg;
    private readonly SKPaint _surfacePaint;
    private readonly SKPaint _surfaceHoverPaint;
    private readonly SKPaint _borderPaint;
    private readonly SKPaint _borderFocusPaint;
    private readonly SKPaint _textPrimary;
    private readonly SKPaint _textSecondary;
    private readonly SKPaint _textMuted;
    private readonly SKPaint _textWhite;
    private readonly SKPaint _primaryPaint;
    private readonly SKPaint _primaryHoverPaint;
    private readonly SKPaint _shadowPaint;
    private readonly SKPaint _separatorPaint;
    private readonly SKPaint _successPaint;
    private readonly SKPaint _errorPaint;
    private readonly SKPaint _warningPaint;

    // Fonts
    private readonly SKFont _font;
    private readonly SKFont _fontSmall;
    private readonly SKFont _fontBold;
    private readonly SKFont _fontTitle;
    private readonly SKFont _fontSection;

    // Sidebar nav rects
    private readonly List<(SettingsSection section, SKRect rect)> _navRects = new();

    // Button rects
    private SKRect _saveButtonRect;
    private SKRect _cancelButtonRect;
    private SKRect _addCredRect;
    private SKRect _saveCredRect;
    private SKRect _cancelCredRect;
    private SKRect _testConnRect;
    private readonly List<(int index, SKRect editRect, SKRect deleteRect)> _credButtonRects = new();

    // Exchange type dropdown
    private readonly string[] _exchangeNames = Enum.GetNames<ExchangeType>();
    private readonly string[] _timeframes = { "1m", "3m", "5m", "15m", "30m", "1h", "2h", "4h", "6h", "12h", "1d", "1w" };
    private readonly string[] _chartTypes = { "Candles", "Line", "Area", "Bars" };

    // Dropdown state (inline)
    private string? _openDropdown;
    private SKRect _dropdownListRect;
    private readonly List<(string value, SKRect rect)> _dropdownItemRects = new();

    // Layout section button rects
    private SKRect _saveLayoutRect;
    private SKRect _resetLayoutRect;

    // Hover tracking
    private SKRect? _hoveredButtonRect;

    // Callbacks
    public Action? OnSaveLayout;
    public Action? OnResetLayout;

    public SettingsModalRenderer()
    {
        _overlayPaint = new SKPaint { Color = new SKColor(0, 0, 0, 180), Style = SKPaintStyle.Fill };
        _modalBg = new SKPaint { Color = ThemeManager.Background, Style = SKPaintStyle.Fill };
        _sidebarBg = new SKPaint { Color = ThemeManager.Surface, Style = SKPaintStyle.Fill };
        _surfacePaint = new SKPaint { Color = ThemeManager.Surface, Style = SKPaintStyle.Fill };
        _surfaceHoverPaint = new SKPaint { Color = ThemeManager.SurfaceHover, Style = SKPaintStyle.Fill };
        _borderPaint = new SKPaint { Color = ThemeManager.Border, Style = SKPaintStyle.Stroke, StrokeWidth = 1, IsAntialias = true };
        _borderFocusPaint = new SKPaint { Color = ThemeManager.BorderFocused, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, IsAntialias = true };
        _textPrimary = new SKPaint { Color = ThemeManager.TextPrimary, IsAntialias = true };
        _textSecondary = new SKPaint { Color = ThemeManager.TextSecondary, IsAntialias = true };
        _textMuted = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };
        _textWhite = new SKPaint { Color = ThemeManager.TextWhite, IsAntialias = true };
        _primaryPaint = new SKPaint { Color = ThemeManager.Primary, Style = SKPaintStyle.Fill };
        _primaryHoverPaint = new SKPaint { Color = ThemeManager.PrimaryHover, Style = SKPaintStyle.Fill };
        _shadowPaint = new SKPaint { Color = ThemeManager.ShadowStrong, MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 16) };
        _separatorPaint = new SKPaint { Color = ThemeManager.Divider, Style = SKPaintStyle.Stroke, StrokeWidth = 1 };
        _successPaint = new SKPaint { Color = ThemeManager.Success, IsAntialias = true };
        _errorPaint = new SKPaint { Color = ThemeManager.Error, IsAntialias = true };
        _warningPaint = new SKPaint { Color = ThemeManager.Warning, IsAntialias = true };

        _font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 13);
        _fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
        _fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 13);
        _fontTitle = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 18);
        _fontSection = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 15);
    }

    // ─── Modal Bounds ─────────────────────────────────────────────

    private (SKRect modal, SKRect sidebar, SKRect content, SKRect bottomBar) GetModalRects(int screenWidth, int screenHeight)
    {
        float w = Math.Min(900, screenWidth - 80);
        float h = Math.Min(700, screenHeight - 80);
        float x = (screenWidth - w) / 2;
        float y = (screenHeight - h) / 2 - 30;

        var modal = new SKRect(x, y, x + w, y + h);
        var sidebar = new SKRect(x, y, x + 200, y + h);
        var content = new SKRect(x + 200, y, x + w, y + h - 52);
        var bottomBar = new SKRect(x + 200, y + h - 52, x + w, y + h);
        return (modal, sidebar, content, bottomBar);
    }

    // ─── Render ──────────────────────────────────────────────────

    public void Render(SKCanvas canvas, int screenWidth, int screenHeight, UiSettingsModal modal)
    {
        if (!modal.IsVisible && modal.AnimationProgress <= 0) return;

        // Update toggle animations
        foreach (var t in modal.AllToggles) t.UpdateAnimation(0.016f);

        // Overlay
        _overlayPaint.Color = new SKColor(0, 0, 0, (byte)(180 * modal.AnimationProgress));
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayPaint);

        var (modalRect, sidebarRect, contentRect, bottomBarRect) = GetModalRects(screenWidth, screenHeight);

        // Animation scale
        canvas.Save();
        if (modal.AnimationProgress < 1)
        {
            float scale = 0.92f + (0.08f * modal.AnimationProgress);
            canvas.Scale(scale, scale, modalRect.MidX, modalRect.MidY);
        }

        // Shadow + Background
        canvas.DrawRoundRect(modalRect, 10, 10, _shadowPaint);
        canvas.DrawRoundRect(modalRect, 10, 10, _modalBg);

        // Sidebar
        canvas.Save();
        canvas.ClipRoundRect(new SKRoundRect(new SKRect(sidebarRect.Left, sidebarRect.Top, sidebarRect.Right, sidebarRect.Bottom), 10, 0), SKClipOperation.Intersect);
        canvas.DrawRect(sidebarRect, _sidebarBg);
        canvas.Restore();

        // Sidebar divider
        canvas.DrawLine(sidebarRect.Right, sidebarRect.Top + 10, sidebarRect.Right, sidebarRect.Bottom - 10, _separatorPaint);

        RenderSidebar(canvas, sidebarRect, modal);
        RenderContent(canvas, contentRect, modal);
        RenderBottomBar(canvas, bottomBarRect, modal);

        // Border
        canvas.DrawRoundRect(modalRect, 10, 10, _borderPaint);

        canvas.Restore(); // animation

        // Dropdown overlay (rendered above everything)
        if (_openDropdown != null)
            RenderDropdownOverlay(canvas);
    }

    // ─── Sidebar ────────────────────────────────────────────────

    private void RenderSidebar(SKCanvas canvas, SKRect rect, UiSettingsModal modal)
    {
        float y = rect.Top + 24;
        canvas.DrawText("Settings", rect.Left + 20, y + 16, _fontTitle, _textWhite);
        y += 50;

        canvas.DrawLine(rect.Left + 16, y, rect.Right - 16, y, _separatorPaint);
        y += 16;

        _navRects.Clear();
        var sections = new (SettingsSection s, SvgIconRenderer.Icon icon, string label)[]
        {
            (SettingsSection.Exchange, SvgIconRenderer.Icon.Exchange, "Exchange APIs"),
            (SettingsSection.General, SvgIconRenderer.Icon.Settings, "General"),
            (SettingsSection.Chart, SvgIconRenderer.Icon.Chart, "Chart"),
            (SettingsSection.Layout, SvgIconRenderer.Icon.Layout, "Layout"),
        };

        foreach (var (section, icon, label) in sections)
        {
            var itemRect = new SKRect(rect.Left + 8, y, rect.Right - 8, y + 40);
            _navRects.Add((section, itemRect));
            bool isActive = modal.ActiveSection == section;
            bool isHovered = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == itemRect;

            if (isActive)
            {
                canvas.DrawRoundRect(itemRect, 6, 6, _primaryPaint);
            }
            else if (isHovered)
            {
                canvas.DrawRoundRect(itemRect, 6, 6, _surfaceHoverPaint);
            }

            SvgIconRenderer.DrawIcon(canvas, icon, itemRect.Left + 12, itemRect.Top + 10, 16,
                isActive ? SKColors.White : ThemeManager.TextSecondary);
            canvas.DrawText(label, itemRect.Left + 38, itemRect.Top + 26, _fontBold,
                isActive ? _textWhite : _textSecondary);

            y += 44;
        }
    }

    // ─── Content Sections ────────────────────────────────────────

    private void RenderContent(SKCanvas canvas, SKRect rect, UiSettingsModal modal)
    {
        canvas.Save();
        canvas.ClipRect(rect);

        switch (modal.ActiveSection)
        {
            case SettingsSection.Exchange: RenderExchangeSection(canvas, rect, modal); break;
            case SettingsSection.General: RenderGeneralSection(canvas, rect, modal); break;
            case SettingsSection.Chart: RenderChartSection(canvas, rect, modal); break;
            case SettingsSection.Layout: RenderLayoutSection(canvas, rect, modal); break;
        }

        canvas.Restore();
    }

    // ─── Exchange Section ────────────────────────────────────────

    private void RenderExchangeSection(SKCanvas canvas, SKRect rect, UiSettingsModal modal)
    {
        float x = rect.Left + 24;
        float y = rect.Top + 24;
        float w = rect.Width - 48;

        RenderSectionHeader(canvas, "Exchange API Keys", SvgIconRenderer.Icon.Key, x, ref y, w);

        if (modal.IsEditingCredential)
        {
            RenderCredentialForm(canvas, x, ref y, w, modal);
        }
        else
        {
            RenderCredentialList(canvas, x, ref y, w, modal);
        }
    }

    private void RenderCredentialList(SKCanvas canvas, float x, ref float y, float w, UiSettingsModal modal)
    {
        _credButtonRects.Clear();

        if (modal.Credentials.Count == 0)
        {
            canvas.DrawText("No API keys configured.", x, y + 16, _font, _textMuted);
            y += 32;
            canvas.DrawText("Add an exchange API key to enable trading features.", x, y + 14, _fontSmall, _textMuted);
            y += 36;
        }
        else
        {
            for (int i = 0; i < modal.Credentials.Count; i++)
            {
                var cred = modal.Credentials[i];
                var cardRect = new SKRect(x, y, x + w, y + 56);

                // Card background
                canvas.DrawRoundRect(cardRect, 6, 6, _surfacePaint);
                canvas.DrawRoundRect(cardRect, 6, 6, _borderPaint);

                // Exchange icon
                SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Exchange, x + 12, y + 18, 16, ThemeManager.Primary);

                // Name & exchange type
                canvas.DrawText(cred.Name, x + 38, y + 22, _fontBold, _textPrimary);
                canvas.DrawText(cred.Exchange.ToString() + (cred.IsTestnet ? " (Testnet)" : ""), x + 38, y + 40, _fontSmall, _textSecondary);

                // Key preview (masked)
                string preview = cred.ApiKey.Length > 8 ? cred.ApiKey[..4] + "..." + cred.ApiKey[^4..] : "****";
                float previewX = x + w - 200;
                canvas.DrawText(preview, previewX, y + 30, _fontSmall, _textMuted);

                // Edit button
                var editRect = new SKRect(x + w - 64, y + 14, x + w - 36, y + 42);
                bool editHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == editRect;
                canvas.DrawRoundRect(editRect, 4, 4, editHover ? _surfaceHoverPaint : _surfacePaint);
                SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Pencil, editRect.Left + 5, editRect.Top + 5, 14, ThemeManager.TextSecondary);

                // Delete button
                var deleteRect = new SKRect(x + w - 32, y + 14, x + w - 4, y + 42);
                bool delHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == deleteRect;
                canvas.DrawRoundRect(deleteRect, 4, 4, delHover ? _surfaceHoverPaint : _surfacePaint);
                SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Trash, deleteRect.Left + 5, deleteRect.Top + 5, 14, ThemeManager.Error);

                _credButtonRects.Add((i, editRect, deleteRect));
                y += 62;
            }
        }

        // Add button
        _addCredRect = new SKRect(x, y, x + 160, y + 36);
        bool addHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _addCredRect;
        canvas.DrawRoundRect(_addCredRect, 6, 6, addHover ? _primaryHoverPaint : _primaryPaint);
        SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Plus, x + 10, y + 8, 16, SKColors.White);
        canvas.DrawText("Add API Key", x + 34, y + 24, _fontBold, _textWhite);
        y += 48;
    }

    private void RenderCredentialForm(SKCanvas canvas, float x, ref float y, float w, UiSettingsModal modal)
    {
        string title = modal.SelectedCredentialIndex >= 0 ? "Edit API Key" : "New API Key";
        canvas.DrawText(title, x, y + 14, _fontBold, _textPrimary);
        y += 30;

        // Name
        RenderTextInput(canvas, modal.CredentialName, x, ref y, w, modal);

        // Exchange type dropdown
        RenderInlineDropdown(canvas, "Exchange", _exchangeNames, modal.SelectedExchangeType, "exchange_type", x, ref y, w);

        // API Key
        RenderTextInput(canvas, modal.ApiKeyInput, x, ref y, w, modal);

        // Secret
        RenderTextInput(canvas, modal.ApiSecretInput, x, ref y, w, modal);

        // Testnet toggle
        RenderToggle(canvas, modal.TestnetToggle, x, ref y, w);

        y += 8;

        // Test connection button
        _testConnRect = new SKRect(x, y, x + 150, y + 32);
        bool testHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _testConnRect;
        canvas.DrawRoundRect(_testConnRect, 6, 6, _surfacePaint);
        canvas.DrawRoundRect(_testConnRect, 6, 6, testHover ? _borderFocusPaint : _borderPaint);
        canvas.DrawText("Test Connection", x + 14, y + 22, _fontSmall, _textPrimary);

        // Connection status
        if (modal.TestConnectionStatus != null)
        {
            var statusPaint = modal.TestConnectionStatus.Contains("Failed") ? _errorPaint
                : modal.TestConnectionStatus.Contains("Testing") ? _warningPaint : _successPaint;
            canvas.DrawText(modal.TestConnectionStatus, x + 162, y + 22, _fontSmall, statusPaint);
        }
        y += 44;

        // Save / Cancel credential buttons
        float btnY = y;
        _saveCredRect = new SKRect(x, btnY, x + 100, btnY + 34);
        bool saveHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _saveCredRect;
        canvas.DrawRoundRect(_saveCredRect, 6, 6, saveHover ? _primaryHoverPaint : _primaryPaint);
        canvas.DrawText("Save Key", x + 16, btnY + 23, _fontBold, _textWhite);

        _cancelCredRect = new SKRect(x + 112, btnY, x + 200, btnY + 34);
        bool cancelHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _cancelCredRect;
        canvas.DrawRoundRect(_cancelCredRect, 6, 6, _surfacePaint);
        canvas.DrawRoundRect(_cancelCredRect, 6, 6, cancelHover ? _borderFocusPaint : _borderPaint);
        canvas.DrawText("Cancel", x + 128, btnY + 23, _fontBold, _textSecondary);

        y += 48;
    }

    // ─── General Section ─────────────────────────────────────────

    private void RenderGeneralSection(SKCanvas canvas, SKRect rect, UiSettingsModal modal)
    {
        float x = rect.Left + 24;
        float y = rect.Top + 24;
        float w = rect.Width - 48;

        RenderSectionHeader(canvas, "General", SvgIconRenderer.Icon.Settings, x, ref y, w);

        // Language (placeholder)
        RenderInlineDropdown(canvas, "Language", new[] { "English" }, 0, "language", x, ref y, w);

        // Theme (placeholder)
        RenderInlineDropdown(canvas, "Theme", new[] { "Dark" }, 0, "theme", x, ref y, w);

        y += 8;
        canvas.DrawLine(x, y, x + w, y, _separatorPaint);
        y += 16;

        // Toggles
        RenderToggle(canvas, modal.RestoreSessionToggle, x, ref y, w);
        RenderToggle(canvas, modal.MinimizeToTrayToggle, x, ref y, w);
    }

    // ─── Chart Section ───────────────────────────────────────────

    private void RenderChartSection(SKCanvas canvas, SKRect rect, UiSettingsModal modal)
    {
        float x = rect.Left + 24;
        float y = rect.Top + 24;
        float w = rect.Width - 48;

        RenderSectionHeader(canvas, "Chart Defaults", SvgIconRenderer.Icon.Chart, x, ref y, w);

        // Default Symbol (read-only display)
        canvas.DrawText("Default Symbol", x, y + 14, _fontSmall, _textSecondary);
        canvas.DrawText(modal.SelectedSymbol, x + 130, y + 14, _fontBold, _textPrimary);
        y += 30;

        // Default Timeframe
        int tfIdx = Array.IndexOf(_timeframes, modal.SelectedTimeframe);
        if (tfIdx < 0) tfIdx = 0;
        RenderInlineDropdown(canvas, "Timeframe", _timeframes, tfIdx, "timeframe", x, ref y, w);

        // Chart Type
        int ctIdx = Array.IndexOf(_chartTypes, modal.SelectedChartType);
        if (ctIdx < 0) ctIdx = 0;
        RenderInlineDropdown(canvas, "Chart Type", _chartTypes, ctIdx, "chart_type", x, ref y, w);

        // Zoom
        canvas.DrawText("Default Zoom", x, y + 14, _fontSmall, _textSecondary);
        canvas.DrawText($"{modal.SelectedZoom:F1}x", x + 130, y + 14, _fontBold, _textPrimary);
        y += 30;

        y += 8;
        canvas.DrawLine(x, y, x + w, y, _separatorPaint);
        y += 16;

        // Toggles
        RenderToggle(canvas, modal.ShowVolumeToggle, x, ref y, w);
        RenderToggle(canvas, modal.ShowGridToggle, x, ref y, w);
    }

    // ─── Layout Section ──────────────────────────────────────────

    private void RenderLayoutSection(SKCanvas canvas, SKRect rect, UiSettingsModal modal)
    {
        float x = rect.Left + 24;
        float y = rect.Top + 24;
        float w = rect.Width - 48;

        RenderSectionHeader(canvas, "Layout", SvgIconRenderer.Icon.Layout, x, ref y, w);

        canvas.DrawText("Panel layout is automatically saved when you close the application.", x, y + 14, _font, _textSecondary);
        y += 36;
        canvas.DrawText("You can also manually save or reset the layout below.", x, y + 14, _fontSmall, _textMuted);
        y += 40;

        // Save current layout
        _saveLayoutRect = new SKRect(x, y, x + 180, y + 36);
        bool saveHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _saveLayoutRect;
        canvas.DrawRoundRect(_saveLayoutRect, 6, 6, saveHover ? _primaryHoverPaint : _primaryPaint);
        canvas.DrawText("Save Current Layout", x + 14, y + 24, _fontBold, _textWhite);
        y += 48;

        // Reset layout
        _resetLayoutRect = new SKRect(x, y, x + 180, y + 36);
        bool resetHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _resetLayoutRect;
        canvas.DrawRoundRect(_resetLayoutRect, 6, 6, _surfacePaint);
        canvas.DrawRoundRect(_resetLayoutRect, 6, 6, resetHover ? _borderFocusPaint : _borderPaint);
        canvas.DrawText("Reset to Default", x + 14, y + 24, _fontBold, _textSecondary);
    }

    // ─── Bottom Bar ──────────────────────────────────────────────

    private void RenderBottomBar(SKCanvas canvas, SKRect rect, UiSettingsModal modal)
    {
        // Separator
        canvas.DrawLine(rect.Left, rect.Top, rect.Right, rect.Top, _separatorPaint);

        float btnW = 90;
        float btnH = 32;
        float btnY = rect.Top + (rect.Height - btnH) / 2;

        // Save button (primary)
        _saveButtonRect = new SKRect(rect.Right - btnW - 16, btnY, rect.Right - 16, btnY + btnH);
        bool saveHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _saveButtonRect;
        canvas.DrawRoundRect(_saveButtonRect, 6, 6, saveHover ? _primaryHoverPaint : _primaryPaint);
        canvas.DrawText("Save", _saveButtonRect.Left + 28, _saveButtonRect.Top + 22, _fontBold, _textWhite);

        // Cancel button
        _cancelButtonRect = new SKRect(rect.Right - btnW * 2 - 24, btnY, rect.Right - btnW - 24, btnY + btnH);
        bool cancelHover = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == _cancelButtonRect;
        canvas.DrawRoundRect(_cancelButtonRect, 6, 6, _surfacePaint);
        canvas.DrawRoundRect(_cancelButtonRect, 6, 6, cancelHover ? _borderFocusPaint : _borderPaint);
        canvas.DrawText("Cancel", _cancelButtonRect.Left + 20, _cancelButtonRect.Top + 22, _fontBold, _textSecondary);

        // Unsaved indicator
        if (modal.HasUnsavedChanges)
        {
            canvas.DrawCircle(rect.Left + 16, rect.Top + rect.Height / 2, 4, _warningPaint);
            canvas.DrawText("Unsaved changes", rect.Left + 26, rect.Top + rect.Height / 2 + 5, _fontSmall, _warningPaint);
        }

        // Esc hint
        canvas.DrawText("Esc to close", rect.Left + (modal.HasUnsavedChanges ? 160 : 16), rect.Top + rect.Height / 2 + 5, _fontSmall, _textMuted);
    }

    // ─── Rendering Helpers ───────────────────────────────────────

    private void RenderSectionHeader(SKCanvas canvas, string title, SvgIconRenderer.Icon icon, float x, ref float y, float w)
    {
        SvgIconRenderer.DrawIcon(canvas, icon, x, y + 2, 20, ThemeManager.Primary);
        canvas.DrawText(title, x + 28, y + 18, _fontSection, _textWhite);
        y += 32;
        canvas.DrawLine(x, y, x + w, y, _separatorPaint);
        y += 20;
    }

    private void RenderTextInput(SKCanvas canvas, UiTextInput input, float x, ref float y, float w, UiSettingsModal modal)
    {
        // Label
        canvas.DrawText(input.Label, x, y + 14, _fontSmall, _textSecondary);
        y += 22;

        // Input box
        float inputH = 34;
        input.Rect = new SKRect(x, y, x + w, y + inputH);

        canvas.DrawRoundRect(input.Rect, 6, 6, _surfacePaint);
        canvas.DrawRoundRect(input.Rect, 6, 6, input.IsFocused ? _borderFocusPaint : _borderPaint);

        // Text or placeholder
        string display = input.DisplayText;
        if (string.IsNullOrEmpty(display))
        {
            canvas.DrawText(input.Placeholder, x + 10, y + 23, _font, _textMuted);
        }
        else
        {
            canvas.DrawText(display, x + 10, y + 23, _font, _textPrimary);
        }

        // Cursor (blink every 500ms)
        if (input.IsFocused && modal.FocusedInput == input)
        {
            bool cursorVisible = (DateTime.Now.Millisecond / 500) % 2 == 0;
            if (cursorVisible)
            {
                string beforeCursor = input.DisplayText[..Math.Min(input.CursorPosition, input.DisplayText.Length)];
                float cursorX = x + 10 + _font.MeasureText(beforeCursor);
                canvas.DrawLine(cursorX, y + 8, cursorX, y + inputH - 8, _borderFocusPaint);
            }
        }

        y += inputH + 10;
    }

    private void RenderToggle(SKCanvas canvas, UiToggle toggle, float x, ref float y, float w)
    {
        float h = string.IsNullOrEmpty(toggle.Description) ? 32 : 44;
        toggle.Rect = new SKRect(x, y, x + w, y + h);

        bool isHovered = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == toggle.Rect;
        if (isHovered)
        {
            canvas.DrawRoundRect(toggle.Rect, 4, 4, _surfaceHoverPaint);
        }

        // Label
        canvas.DrawText(toggle.Label, x + 4, y + 18, _font, _textPrimary);
        if (!string.IsNullOrEmpty(toggle.Description))
        {
            canvas.DrawText(toggle.Description, x + 4, y + 34, _fontSmall, _textMuted);
        }

        // Toggle switch (right-aligned)
        float toggleW = 38;
        float toggleH = 20;
        float toggleX = x + w - toggleW - 4;
        float toggleY = y + (h - toggleH) / 2;

        var trackRect = new SKRect(toggleX, toggleY, toggleX + toggleW, toggleY + toggleH);

        // Track
        using var trackPaint = new SKPaint
        {
            Color = toggle.AnimationProgress > 0.5f
                ? ThemeManager.Primary
                : ThemeManager.ButtonDefault,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawRoundRect(trackRect, toggleH / 2, toggleH / 2, trackPaint);

        // Thumb
        float thumbR = 7;
        float thumbMinX = toggleX + thumbR + 3;
        float thumbMaxX = toggleX + toggleW - thumbR - 3;
        float thumbX = thumbMinX + (thumbMaxX - thumbMinX) * toggle.AnimationProgress;
        float thumbY = toggleY + toggleH / 2;

        using var thumbPaint = new SKPaint
        {
            Color = SKColors.White,
            Style = SKPaintStyle.Fill,
            IsAntialias = true
        };
        canvas.DrawCircle(thumbX, thumbY, thumbR, thumbPaint);

        y += h + 8;
    }

    private void RenderInlineDropdown(SKCanvas canvas, string label, string[] items, int selectedIndex,
        string dropdownId, float x, ref float y, float w)
    {
        canvas.DrawText(label, x, y + 14, _fontSmall, _textSecondary);

        float ddW = 180;
        float ddH = 30;
        float ddX = x + 130;
        var ddRect = new SKRect(ddX, y, ddX + ddW, y + ddH);

        bool isOpen = _openDropdown == dropdownId;
        bool isHovered = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == ddRect;

        canvas.DrawRoundRect(ddRect, 4, 4, _surfacePaint);
        canvas.DrawRoundRect(ddRect, 4, 4, isOpen ? _borderFocusPaint : (isHovered ? _borderFocusPaint : _borderPaint));

        string selected = selectedIndex >= 0 && selectedIndex < items.Length ? items[selectedIndex] : "";
        canvas.DrawText(selected, ddX + 10, y + 20, _font, _textPrimary);

        // Chevron
        float chevX = ddX + ddW - 16;
        float chevY = y + 12;
        using var chevPaint = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true, Style = SKPaintStyle.Stroke, StrokeWidth = 1.5f, StrokeCap = SKStrokeCap.Round };
        canvas.DrawLine(chevX - 3, chevY, chevX, chevY + 4, chevPaint);
        canvas.DrawLine(chevX, chevY + 4, chevX + 3, chevY, chevPaint);

        // Store rect for click detection with dropdown ID
        if (isOpen)
        {
            _dropdownListRect = new SKRect(ddX, y + ddH + 2, ddX + ddW, y + ddH + 2 + items.Length * 30);
            _dropdownItemRects.Clear();
            for (int i = 0; i < items.Length; i++)
            {
                _dropdownItemRects.Add((items[i], new SKRect(ddX, y + ddH + 2 + i * 30, ddX + ddW, y + ddH + 2 + (i + 1) * 30)));
            }
        }

        // Register dropdown trigger rect for click handling
        // We tag it by using the same Rect check
        if (!isOpen)
        {
            // Store the rect to detect clicks
        }

        y += ddH + 12;
    }

    private void RenderDropdownOverlay(SKCanvas canvas)
    {
        if (_dropdownItemRects.Count == 0) return;

        // Background
        canvas.DrawRoundRect(_dropdownListRect, 6, 6, _surfacePaint);
        canvas.DrawRoundRect(_dropdownListRect, 6, 6, _borderPaint);

        foreach (var (value, itemRect) in _dropdownItemRects)
        {
            bool isHovered = _hoveredButtonRect.HasValue && _hoveredButtonRect.Value == itemRect;
            if (isHovered)
                canvas.DrawRect(itemRect, _surfaceHoverPaint);

            canvas.DrawText(value, itemRect.Left + 10, itemRect.Top + 20, _font, _textPrimary);
        }
    }

    // ─── Click Handling ──────────────────────────────────────────

    /// <summary>
    /// Returns false if clicked outside modal (caller should close it).
    /// </summary>
    public bool HandleMouseDown(float mx, float my, int screenWidth, int screenHeight, UiSettingsModal modal)
    {
        var (modalRect, _, _, _) = GetModalRects(screenWidth, screenHeight);
        if (!modalRect.Contains(mx, my))
            return false;

        // Close dropdown if open and clicked outside it
        if (_openDropdown != null)
        {
            if (_dropdownListRect.Contains(mx, my))
            {
                // Click on dropdown item
                foreach (var (value, itemRect) in _dropdownItemRects)
                {
                    if (itemRect.Contains(mx, my))
                    {
                        ApplyDropdownSelection(_openDropdown, value, modal);
                        _openDropdown = null;
                        return true;
                    }
                }
            }
            _openDropdown = null;
            return true;
        }

        // Sidebar nav
        foreach (var (section, rect) in _navRects)
        {
            if (rect.Contains(mx, my))
            {
                modal.ActiveSection = section;
                modal.FocusedInput = null;
                return true;
            }
        }

        // Bottom bar buttons
        if (_saveButtonRect.Contains(mx, my)) return true; // Save handled by caller
        if (_cancelButtonRect.Contains(mx, my)) { modal.Close(); return true; }

        // Section-specific clicks
        switch (modal.ActiveSection)
        {
            case SettingsSection.Exchange:
                return HandleExchangeClick(mx, my, modal);
            case SettingsSection.Layout:
                return HandleLayoutClick(mx, my, modal);
        }

        // Text input focus
        foreach (var input in modal.AllInputs)
        {
            if (input.Rect.Contains(mx, my))
            {
                modal.FocusedInput = input;
                input.IsFocused = true;
                // Unfocus others
                foreach (var other in modal.AllInputs)
                    if (other != input) other.IsFocused = false;
                return true;
            }
        }

        // Toggle clicks
        foreach (var toggle in modal.AllToggles)
        {
            if (toggle.Rect.Contains(mx, my))
            {
                toggle.Toggle();
                modal.HasUnsavedChanges = true;
                return true;
            }
        }

        // Inline dropdown triggers — check for dropdown rects in content area
        // This is handled by checking if click matches a dropdown trigger location
        // For simplicity, re-check dropdown rects
        return HandleDropdownTriggerClick(mx, my, modal);
    }

    private bool HandleExchangeClick(float mx, float my, UiSettingsModal modal)
    {
        if (modal.IsEditingCredential)
        {
            if (_saveCredRect.Contains(mx, my)) { modal.SaveCurrentCredential(); return true; }
            if (_cancelCredRect.Contains(mx, my)) { modal.IsEditingCredential = false; return true; }
            if (_testConnRect.Contains(mx, my))
            {
                modal.TestConnectionStatus = ValidateKeyFormat(modal) ? "Key format valid" : "Failed: Invalid key format";
                return true;
            }
        }
        else
        {
            if (_addCredRect.Contains(mx, my)) { modal.StartNewCredential(); return true; }
            foreach (var (idx, editRect, deleteRect) in _credButtonRects)
            {
                if (editRect.Contains(mx, my)) { modal.EditCredential(idx); return true; }
                if (deleteRect.Contains(mx, my)) { modal.DeleteCredential(idx); return true; }
            }
        }
        return false;
    }

    private bool HandleLayoutClick(float mx, float my, UiSettingsModal modal)
    {
        if (_saveLayoutRect.Contains(mx, my)) { OnSaveLayout?.Invoke(); return true; }
        if (_resetLayoutRect.Contains(mx, my)) { OnResetLayout?.Invoke(); return true; }
        return false;
    }

    private bool HandleDropdownTriggerClick(float mx, float my, UiSettingsModal modal)
    {
        // Check all known dropdown trigger positions
        // Since dropdowns are rendered inline, we check the approximate trigger areas
        var (_, _, contentRect, _) = GetModalRects(0, 0); // we need screen size...

        // Simpler approach: check if click is near any dropdown label area
        // This is a simplified version - the actual trigger rects are computed during render
        // We'll use the _openDropdown mechanism with y-position matching
        return false;
    }

    // Store dropdown trigger rects during render for click detection
    private readonly Dictionary<string, SKRect> _dropdownTriggerRects = new();

    private void ApplyDropdownSelection(string dropdownId, string value, UiSettingsModal modal)
    {
        switch (dropdownId)
        {
            case "exchange_type":
                int idx = Array.IndexOf(_exchangeNames, value);
                if (idx >= 0) modal.SelectedExchangeType = idx;
                modal.HasUnsavedChanges = true;
                break;
            case "timeframe":
                modal.SelectedTimeframe = value;
                modal.HasUnsavedChanges = true;
                break;
            case "chart_type":
                modal.SelectedChartType = value;
                modal.HasUnsavedChanges = true;
                break;
        }
    }

    private bool ValidateKeyFormat(UiSettingsModal modal)
    {
        return !string.IsNullOrWhiteSpace(modal.ApiKeyInput.Text) &&
               modal.ApiKeyInput.Text.Length >= 10 &&
               !string.IsNullOrWhiteSpace(modal.ApiSecretInput.Text) &&
               modal.ApiSecretInput.Text.Length >= 10;
    }

    // ─── Mouse Move ──────────────────────────────────────────────

    public void HandleMouseMove(float mx, float my, int screenWidth, int screenHeight, UiSettingsModal modal)
    {
        _hoveredButtonRect = null;
        var (modalRect, _, _, _) = GetModalRects(screenWidth, screenHeight);
        if (!modalRect.Contains(mx, my)) return;

        // Nav items
        foreach (var (_, rect) in _navRects)
            if (rect.Contains(mx, my)) { _hoveredButtonRect = rect; return; }

        // Bottom bar
        if (_saveButtonRect.Contains(mx, my)) { _hoveredButtonRect = _saveButtonRect; return; }
        if (_cancelButtonRect.Contains(mx, my)) { _hoveredButtonRect = _cancelButtonRect; return; }

        // Toggles
        foreach (var toggle in modal.AllToggles)
            if (toggle.Rect.Contains(mx, my)) { _hoveredButtonRect = toggle.Rect; return; }

        // Credential buttons
        foreach (var (_, editRect, deleteRect) in _credButtonRects)
        {
            if (editRect.Contains(mx, my)) { _hoveredButtonRect = editRect; return; }
            if (deleteRect.Contains(mx, my)) { _hoveredButtonRect = deleteRect; return; }
        }

        // Add/save/cancel cred buttons
        if (_addCredRect.Contains(mx, my)) { _hoveredButtonRect = _addCredRect; return; }
        if (_saveCredRect.Contains(mx, my)) { _hoveredButtonRect = _saveCredRect; return; }
        if (_cancelCredRect.Contains(mx, my)) { _hoveredButtonRect = _cancelCredRect; return; }
        if (_testConnRect.Contains(mx, my)) { _hoveredButtonRect = _testConnRect; return; }

        // Layout buttons
        if (_saveLayoutRect.Contains(mx, my)) { _hoveredButtonRect = _saveLayoutRect; return; }
        if (_resetLayoutRect.Contains(mx, my)) { _hoveredButtonRect = _resetLayoutRect; return; }

        // Dropdown items
        if (_openDropdown != null)
        {
            foreach (var (_, itemRect) in _dropdownItemRects)
                if (itemRect.Contains(mx, my)) { _hoveredButtonRect = itemRect; return; }
        }
    }

    // ─── Focus cycling ───────────────────────────────────────────

    public void FocusNextInput(UiSettingsModal modal)
    {
        var inputs = modal.AllInputs;
        int currentIdx = -1;
        for (int i = 0; i < inputs.Length; i++)
        {
            if (inputs[i] == modal.FocusedInput) { currentIdx = i; break; }
        }

        foreach (var inp in inputs) inp.IsFocused = false;

        int next = (currentIdx + 1) % inputs.Length;
        modal.FocusedInput = inputs[next];
        inputs[next].IsFocused = true;
    }

    /// <summary>Check if Save button was clicked (caller handles the actual save).</summary>
    public bool IsSaveClicked(float mx, float my) => _saveButtonRect.Contains(mx, my);
}
