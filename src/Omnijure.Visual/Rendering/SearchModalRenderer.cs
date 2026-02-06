
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

    public SearchModalRenderer()
    {
        _overlayPaint = new SKPaint 
        { 
            Color = new SKColor(0, 0, 0, 200), 
            Style = SKPaintStyle.Fill 
        };
        
        _modalBg = new SKPaint 
        { 
            Color = new SKColor(20, 23, 28), // Darker, more professional
            Style = SKPaintStyle.Fill 
        };
        
        _searchBoxBg = new SKPaint 
        { 
            Color = new SKColor(30, 34, 40), 
            Style = SKPaintStyle.Fill 
        };
        
        _searchBoxBorder = new SKPaint 
        { 
            Color = new SKColor(50, 55, 65), 
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1,
            IsAntialias = true
        };
        
        _textPaint = new SKPaint { Color = new SKColor(200, 205, 210), IsAntialias = true };
        _textPaintLarge = new SKPaint { Color = new SKColor(240, 245, 250), IsAntialias = true };
        _textPaintSmall = new SKPaint { Color = new SKColor(130, 135, 145), IsAntialias = true };
        _textPaintDim = new SKPaint { Color = new SKColor(90, 95, 105), IsAntialias = true };
        
        _selectedBg = new SKPaint 
        { 
            Color = new SKColor(40, 50, 65), // Subtle blue-gray
            Style = SKPaintStyle.Fill 
        };
        
        _hoverBg = new SKPaint 
        { 
            Color = new SKColor(32, 36, 42), 
            Style = SKPaintStyle.Fill 
        };
        
        _shadowPaint = new SKPaint 
        { 
            Color = new SKColor(0, 0, 0, 150), 
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 12) 
        };
        
        _separatorPaint = new SKPaint 
        { 
            Color = new SKColor(40, 45, 52), 
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 1
        };
        
        _font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 13);
        _fontLarge = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 16);
        _fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
        _fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 14);
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
        
        // Search icon
        canvas.DrawText("🔍", searchRect.Left + 14, searchRect.MidY + 6, _font, _textPaintDim);
        
        // Search text or placeholder
        string displayText = string.IsNullOrEmpty(modal.SearchText) ? "Type to search..." : modal.SearchText;
        var searchTextPaint = string.IsNullOrEmpty(modal.SearchText) ? _textPaintDim : _textPaintLarge;
        canvas.DrawText(displayText, searchRect.Left + 42, searchRect.MidY + 5, _font, searchTextPaint);
        
        // Cursor
        if (modal.IsVisible && !string.IsNullOrEmpty(modal.SearchText))
        {
            float cursorX = searchRect.Left + 42 + _font.MeasureText(modal.SearchText);
            canvas.DrawLine(cursorX, searchRect.Top + 10, cursorX, searchRect.Bottom - 10, _textPaintLarge);
        }
        
        y += 58;
        
        // Separator line
        canvas.DrawLine(modalX + 20, y, modalX + modalWidth - 20, y, _separatorPaint);
        y += 14;
        
        // Results header
        int totalResults = modal.GetTotalResultCount();
        string resultsText = totalResults == 0 ? "No results" : $"{totalResults} symbol{(totalResults == 1 ? "" : "s")}";
        canvas.DrawText(resultsText, modalX + 24, y, _fontSmall, _textPaintSmall);
        y += 18;
        
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
                
                // Symbol (left, bold)
                canvas.DrawText(result.Symbol, itemRect.Left + 12, itemRect.MidY + 2, _fontBold, _textPaintLarge);
                
                // Exchange (below symbol, small)
                canvas.DrawText(result.Exchange, itemRect.Left + 12, itemRect.MidY + 16, _fontSmall, _textPaintSmall);
                
                // Price and change (right side)
                if (result.Price > 0)
                {
                    string priceText = $"${result.Price:F2}";
                    float priceWidth = _font.MeasureText(priceText);
                    canvas.DrawText(priceText, itemRect.Right - 12 - priceWidth, itemRect.MidY - 6, _font, _textPaint);
                    
                    // Change %
                    string arrow = result.PercentChange >= 0 ? "▲" : "▼";
                    string changeText = $"{arrow} {Math.Abs(result.PercentChange):F2}%";
                    float changeWidth = _fontSmall.MeasureText(changeText);
                    using var changePaint = new SKPaint 
                    { 
                        Color = result.PercentChange >= 0 ? new SKColor(14, 203, 129) : new SKColor(246, 70, 93),
                        IsAntialias = true 
                    };
                    canvas.DrawText(changeText, itemRect.Right - 12 - changeWidth, itemRect.MidY + 14, _fontSmall, changePaint);
                }
                
                y += itemHeight + 2;
            }
        }
        
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
