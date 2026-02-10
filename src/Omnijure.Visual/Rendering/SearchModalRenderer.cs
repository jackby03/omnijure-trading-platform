
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

public class SearchModalRenderer
{
    private readonly SKPaint _overlayPaint;
    private readonly SKPaint _modalBg;
    private readonly SKPaint _searchBoxBg;
    private readonly SKPaint _searchBoxBorder;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _textPaintLarge;
    private readonly SKPaint _textPaintSmall;
    private readonly SKPaint _textPaintDim;
    private readonly SKPaint _selectedBg;
    private readonly SKPaint _hoverBg;
    private readonly SKPaint _shadowPaint;
    private readonly SKPaint _separatorPaint;
    private readonly SKFont _font;
    private readonly SKFont _fontLarge;
    private readonly SKFont _fontSmall;
    private readonly SKFont _fontBold;
    private readonly SKFont _fontExtraSmall;

    public SearchModalRenderer()
    {
        _overlayPaint = new SKPaint
        {
            Color = ThemeManager.WithAlpha(ThemeManager.Background, 200),
            Style = SKPaintStyle.Fill
        };

        _modalBg = new SKPaint
        {
            Color = ThemeManager.Background,
            Style = SKPaintStyle.Fill
        };

        _searchBoxBg = new SKPaint
        {
            Color = ThemeManager.Surface,
            Style = SKPaintStyle.Fill
        };

        _searchBoxBorder = new SKPaint
        {
            Color = ThemeManager.Border,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };

        _textPaint = new SKPaint { Color = ThemeManager.TextPrimary, IsAntialias = true };
        _textPaintLarge = new SKPaint { Color = ThemeManager.TextWhite, IsAntialias = true };
        _textPaintSmall = new SKPaint { Color = ThemeManager.TextSecondary, IsAntialias = true };
        _textPaintDim = new SKPaint { Color = ThemeManager.TextMuted, IsAntialias = true };

        _selectedBg = new SKPaint
        {
            Color = ThemeManager.ButtonActive,
            Style = SKPaintStyle.Fill
        };

        _hoverBg = new SKPaint
        {
            Color = ThemeManager.SurfaceHover,
            Style = SKPaintStyle.Fill
        };

        _shadowPaint = new SKPaint
        {
            Color = ThemeManager.ShadowStrong,
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12)
        };

        _separatorPaint = new SKPaint
        {
            Color = ThemeManager.Divider,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        
        _font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 13);
        _fontLarge = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 16);
        _fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
        _fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 14);
        _fontExtraSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 9);
    }

    public void Render(SKCanvas canvas, int screenWidth, int screenHeight, UiSearchModal modal)
    {
        if (!modal.IsVisible && modal.AnimationProgress <= 0) return;
        
        // Apply animation alpha
        byte alpha = (byte)(180 * modal.AnimationProgress);
        _overlayPaint.Color = new SKColor(0, 0, 0, alpha);
        
        // Draw overlay
        canvas.DrawRect(0, 0, screenWidth, screenHeight, _overlayPaint);
        
        // Modal dimensions
        float modalWidth = Math.Min(600, screenWidth - 80);
        float modalHeight = Math.Min(700, screenHeight - 100);
        float modalX = (screenWidth - modalWidth) / 2;
        float modalY = (screenHeight - modalHeight) / 2 - 50; // Slightly above center
        
        var modalRect = new SKRect(modalX, modalY, modalX + modalWidth, modalY + modalHeight);
        
        // Apply animation scale
        if (modal.AnimationProgress < 1)
        {
            canvas.Save();
            float scale = 0.9f + (0.1f * modal.AnimationProgress);
            canvas.Scale(scale, scale, modalRect.MidX, modalRect.MidY);
        }
        
        // Draw shadow
        canvas.DrawRoundRect(modalRect, 12, 12, _shadowPaint);
        
        // Draw modal background
        canvas.DrawRoundRect(modalRect, 12, 12, _modalBg);
        
        float y = modalY + 18;
        
        // Title
        canvas.DrawText("Search Symbols", modalX + 24, y + 18, _fontLarge, _textPaintLarge);
        y += 48;
        
        // Separator line after title
        canvas.DrawLine(modalX, y, modalX + modalWidth, y, _separatorPaint);
        y += 18;
        
        // Search box with border
        var searchRect = new SKRect(modalX + 20, y, modalX + modalWidth - 20, y + 44);
        canvas.DrawRoundRect(searchRect, 6, 6, _searchBoxBg);
        canvas.DrawRoundRect(searchRect, 6, 6, _searchBoxBorder);
        
        // Search icon (SVG for better quality)
        SvgIconRenderer.DrawIcon(canvas, SvgIconRenderer.Icon.Search, searchRect.Left + 14, searchRect.Top + 12, 20, ThemeManager.TextMuted);
        
        // Search text or placeholder
        string displayText = string.IsNullOrEmpty(modal.SearchText) ? "Type to search..." : modal.SearchText;
        var searchTextPaint = string.IsNullOrEmpty(modal.SearchText) ? _textPaintDim : _textPaintLarge;
        canvas.DrawText(displayText, searchRect.Left + 42, searchRect.MidY + 5, _font, searchTextPaint);
        
        // Clear button (X) if text present
        if (!string.IsNullOrEmpty(modal.SearchText))
        {
            float clearX = searchRect.Right - 30;
            float clearY = searchRect.MidY;
            using var p = new SKPaint { Color = _textPaintDim.Color, StrokeWidth = 2, IsAntialias = true, Style = SKPaintStyle.Stroke };
            canvas.DrawLine(clearX - 8, clearY - 8, clearX + 8, clearY + 8, p);
            canvas.DrawLine(clearX + 8, clearY - 8, clearX - 8, clearY + 8, p);
        }

        // Cursor (if visible)
        if (modal.IsVisible)
        {
            float textWidth = _font.MeasureText(modal.SearchText);
            float cursorX = searchRect.Left + 42 + textWidth;
            // Blink cursor
            if ((DateTime.Now.Millisecond / 500) % 2 == 0)
            {
                canvas.DrawLine(cursorX, searchRect.Top + 10, cursorX, searchRect.Bottom - 10, _textPaintLarge);
            }
        }
        
        y += 58;
        
        // Category Tabs
        float tabX = modalX + 24;
        string[] categories = Enum.GetNames(typeof(AssetCategory));
        for (int i = 0; i < categories.Length; i++)
        {
            AssetCategory cat = (AssetCategory)i;
            string catLabel = categories[i];
            float labelWidth = _fontSmall.MeasureText(catLabel) + 20;
            var tabRect = new SKRect(tabX, y, tabX + labelWidth, y + 24);
            
            bool isSelected = modal.SelectedCategory == cat;
            if (isSelected)
            {
                using var p = new SKPaint { Color = ThemeManager.TextWhite, Style = SKPaintStyle.Fill, IsAntialias = true };
                canvas.DrawRoundRect(tabRect, 12, 12, p);
                using var tp = new SKPaint { Color = ThemeManager.Background, IsAntialias = true };
                canvas.DrawText(catLabel, tabRect.Left + 10, tabRect.MidY + 4, _fontSmall, tp);
            }
            else
            {
                using var tp = new SKPaint { Color = new SKColor(200, 205, 210), IsAntialias = true };
                canvas.DrawText(catLabel, tabRect.Left + 10, tabRect.MidY + 4, _fontSmall, tp);
            }
            
            tabX += labelWidth + 10;
        }
        
        y += 38;
        
        // Separator line
        canvas.DrawLine(modalX, y, modalX + modalWidth, y, _separatorPaint);
        y += 18;
        
        // Results header
        int totalResults = modal.GetTotalResultCount();
        string resultsText = totalResults == 0 ? "No results" : $"{totalResults} symbol{(totalResults == 1 ? "" : "s")}";
        canvas.DrawText(resultsText, modalX + 24, y, _fontSmall, _textPaintSmall);
        y += 18;
        
        // Clip results area to prevent overflow
        float resultsAreaHeight = modalHeight - (y - modalY) - 40; // Leave space for hint at bottom
        var clipRect = new SKRect(modalX, y, modalX + modalWidth, y + resultsAreaHeight);
        canvas.Save();
        canvas.ClipRect(clipRect);
        
        // Results list
        var results = modal.GetVisibleResults();
        float itemHeight = 48;
        
        if (results.Count == 0 && !string.IsNullOrEmpty(modal.SearchText))
        {
            // No results message
            canvas.DrawText("No symbols found", modalX + modalWidth / 2 - 60, y + 80, _font, _textPaintDim);
        }
        else
        {
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var itemRect = new SKRect(modalX + 16, y, modalX + modalWidth - 16, y + itemHeight);
                
                // Highlight selected item with rounded rect
                int globalIndex = modal.ScrollOffset + i;
                if (globalIndex == modal.SelectedIndex)
                {
                    canvas.DrawRoundRect(itemRect, 4, 4, _selectedBg);
                }
                
                // Crypto icon (left side)
                float iconSize = 24;
                float iconX = itemRect.Left + 12;
                float iconY = itemRect.Top + (itemHeight - iconSize) / 2 - 4;
                CryptoIconProvider.DrawCryptoIcon(canvas, result.Symbol, iconX, iconY, iconSize);

                // Symbol (after icon, bold)
                canvas.DrawText(result.Symbol, itemRect.Left + 44, itemRect.MidY + 2, _fontBold, _textPaintLarge);
                
                // Exchange display - pills like TradingView
                float exchangeX = itemRect.Left + 44;
                float exchangeY = itemRect.MidY + 16;
                
                foreach (var exch in result.Exchanges)
                {
                    float exchWidth = _fontExtraSmall.MeasureText(exch) + 8;
                    var exchRect = new SKRect(exchangeX, exchangeY - 8, exchangeX + exchWidth, exchangeY + 4);
                    
                    using var exchBg = new SKPaint { Color = new SKColor(40, 45, 55), Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawRoundRect(exchRect, 2, 2, exchBg);
                    
                    using var exchTxt = new SKPaint { Color = new SKColor(200, 205, 210), IsAntialias = true };
                    canvas.DrawText(exch, exchRect.Left + 4, exchRect.MidY + 3, _fontExtraSmall, exchTxt);
                    
                    exchangeX += exchWidth + 6;
                    if (exchangeX > itemRect.MidX) break; // Don't overflow to the price area
                }
                
                // Price and change (right side)
                if (result.Price > 0)
                {
                    string priceText = $"${result.Price:F2}";
                    float priceWidth = _font.MeasureText(priceText);
                    canvas.DrawText(priceText, itemRect.Right - 12 - priceWidth, itemRect.MidY - 6, _font, _textPaint);
                    
                    // Change % with vector arrow
                    SKColor changeColor = result.PercentChange >= 0
                        ? ThemeManager.BullishGreen
                        : ThemeManager.BearishRed;

                    string changeText = $"{Math.Abs(result.PercentChange):F2}%";
                    float changeWidth = _fontSmall.MeasureText(changeText);

                    float changeRightX = itemRect.Right - 12;
                    float changeTextX = changeRightX - changeWidth;
                    float changeTextY = itemRect.MidY + 14;

                    // Draw arrow triangle
                    float arrowX = changeTextX - 12;
                    float arrowY = changeTextY - 4;
                    using var arrowPath = new SKPath();
                    if (result.PercentChange >= 0)
                    {
                        arrowPath.MoveTo(arrowX, arrowY + 6);
                        arrowPath.LineTo(arrowX + 4, arrowY);
                        arrowPath.LineTo(arrowX + 8, arrowY + 6);
                    }
                    else
                    {
                        arrowPath.MoveTo(arrowX, arrowY);
                        arrowPath.LineTo(arrowX + 4, arrowY + 6);
                        arrowPath.LineTo(arrowX + 8, arrowY);
                    }
                    arrowPath.Close();
                    using var arrowPaint = new SKPaint { Color = changeColor, Style = SKPaintStyle.Fill, IsAntialias = true };
                    canvas.DrawPath(arrowPath, arrowPaint);

                    using var changePaint = new SKPaint { Color = changeColor, IsAntialias = true };
                    canvas.DrawText(changeText, changeTextX, changeTextY, _fontSmall, changePaint);
                }
                
                y += itemHeight + 2;
            }
        }
        
        // Restore canvas after clipping
        canvas.Restore();
        
        // Scrollbar
        if (totalResults > modal.MaxVisibleResults)
        {
            float scrollTrackH = modal.MaxVisibleResults * (itemHeight + 5);
            float scrollThumbH = (modal.MaxVisibleResults / (float)totalResults) * scrollTrackH;
            float scrollThumbY = (modal.ScrollOffset / (float)totalResults) * scrollTrackH;
            
            float scrollX = modalX + modalWidth - 12;
            float scrollY = modalY + 145;
            
            var thumbRect = new SKRect(scrollX, scrollY + scrollThumbY, scrollX + 4, scrollY + scrollThumbY + scrollThumbH);
            using var scrollPaint = new SKPaint { Color = new SKColor(120, 120, 120), Style = SKPaintStyle.Fill };
            canvas.DrawRoundRect(thumbRect, 2, 2, scrollPaint);
        }
        
        // Hint text at bottom
        string hint = "↑↓ Navigate  •  Enter Select  •  Esc Close";
        float hintWidth = _fontSmall.MeasureText(hint);
        canvas.DrawText(hint, modalX + (modalWidth - hintWidth) / 2, modalY + modalHeight - 20, _fontSmall, _textPaintSmall);
        
        if (modal.AnimationProgress < 1)
        {
            canvas.Restore();
        }
    }
}
