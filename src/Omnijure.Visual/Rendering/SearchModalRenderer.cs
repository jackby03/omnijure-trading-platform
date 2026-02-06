
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

public class SearchModalRenderer
{
    private readonly SKPaint _overlayPaint;
    private readonly SKPaint _modalBg;
    private readonly SKPaint _searchBoxBg;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _textPaintLarge;
    private readonly SKPaint _textPaintSmall;
    private readonly SKPaint _selectedBg;
    private readonly SKPaint _hoverBg;
    private readonly SKPaint _shadowPaint;
    private readonly SKFont _font;
    private readonly SKFont _fontLarge;
    private readonly SKFont _fontSmall;
    private readonly SKFont _fontBold;

    public SearchModalRenderer()
    {
        _overlayPaint = new SKPaint 
        { 
            Color = new SKColor(0, 0, 0, 180), 
            Style = SKPaintStyle.Fill 
        };
        
        _modalBg = new SKPaint 
        { 
            Color = new SKColor(25, 28, 35), 
            Style = SKPaintStyle.Fill 
        };
        
        _searchBoxBg = new SKPaint 
        { 
            Color = new SKColor(35, 40, 50), 
            Style = SKPaintStyle.Fill 
        };
        
        _textPaint = new SKPaint { Color = new SKColor(220, 220, 220), IsAntialias = true };
        _textPaintLarge = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _textPaintSmall = new SKPaint { Color = new SKColor(150, 150, 150), IsAntialias = true };
        
        _selectedBg = new SKPaint 
        { 
            Color = new SKColor(45, 85, 140), 
            Style = SKPaintStyle.Fill 
        };
        
        _hoverBg = new SKPaint 
        { 
            Color = new SKColor(40, 45, 55), 
            Style = SKPaintStyle.Fill 
        };
        
        _shadowPaint = new SKPaint 
        { 
            Color = new SKColor(0, 0, 0, 120), 
            MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 8) 
        };
        
        _font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 14);
        _fontLarge = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 18);
        _fontSmall = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 12);
        _fontBold = new SKFont(SKTypeface.FromFamilyName("Segoe UI", SKFontStyle.Bold), 15);
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
        
        float y = modalY + 20;
        
        // Title
        canvas.DrawText("Search Symbols", modalX + 25, y + 20, _fontLarge, _textPaintLarge);
        y += 50;
        
        // Search box
        var searchRect = new SKRect(modalX + 20, y, modalX + modalWidth - 20, y + 50);
        canvas.DrawRoundRect(searchRect, 8, 8, _searchBoxBg);
        
        // Search icon
        canvas.DrawText("🔍", searchRect.Left + 15, searchRect.MidY + 8, _fontLarge, _textPaint);
        
        // Search text or placeholder
        string displayText = string.IsNullOrEmpty(modal.SearchText) ? "Type to search..." : modal.SearchText;
        var searchTextPaint = string.IsNullOrEmpty(modal.SearchText) ? _textPaintSmall : _textPaintLarge;
        canvas.DrawText(displayText, searchRect.Left + 50, searchRect.MidY + 8, _fontLarge, searchTextPaint);
        
        // Cursor
        if (modal.IsVisible && !string.IsNullOrEmpty(modal.SearchText))
        {
            float cursorX = searchRect.Left + 50 + _fontLarge.MeasureText(modal.SearchText);
            canvas.DrawLine(cursorX, searchRect.Top + 12, cursorX, searchRect.Bottom - 12, _textPaintLarge);
        }
        
        y += 70;
        
        // Results header
        int totalResults = modal.GetTotalResultCount();
        string resultsText = totalResults == 0 ? "No results" : $"{totalResults} result{(totalResults == 1 ? "" : "s")}";
        canvas.DrawText(resultsText, modalX + 25, y, _fontSmall, _textPaintSmall);
        y += 25;
        
        // Results list
        var results = modal.GetVisibleResults();
        float itemHeight = 50;
        
        if (results.Count == 0 && !string.IsNullOrEmpty(modal.SearchText))
        {
            // No results message
            canvas.DrawText("No symbols found", modalX + modalWidth / 2 - 60, y + 100, _font, _textPaintSmall);
        }
        else
        {
            for (int i = 0; i < results.Count; i++)
            {
                var result = results[i];
                var itemRect = new SKRect(modalX + 20, y, modalX + modalWidth - 20, y + itemHeight);
                
                // Highlight selected item
                int globalIndex = modal.ScrollOffset + i;
                if (globalIndex == modal.SelectedIndex)
                {
                    canvas.DrawRoundRect(itemRect, 6, 6, _selectedBg);
                }
                
                // Symbol (left, bold)
                canvas.DrawText(result.Symbol, itemRect.Left + 15, itemRect.MidY + 5, _fontBold, _textPaintLarge);
                
                // Exchange (below symbol, small)
                canvas.DrawText(result.Exchange, itemRect.Left + 15, itemRect.MidY + 20, _fontSmall, _textPaintSmall);
                
                // Price and change (right side)
                if (result.Price > 0)
                {
                    string priceText = $"${result.Price:F2}";
                    float priceWidth = _font.MeasureText(priceText);
                    canvas.DrawText(priceText, itemRect.Right - 15 - priceWidth, itemRect.MidY - 5, _font, _textPaint);
                    
                    // Change %
                    string arrow = result.PercentChange >= 0 ? "▲" : "▼";
                    string changeText = $"{arrow} {Math.Abs(result.PercentChange):F2}%";
                    float changeWidth = _fontSmall.MeasureText(changeText);
                    using var changePaint = new SKPaint 
                    { 
                        Color = result.PercentChange >= 0 ? new SKColor(0, 200, 100) : new SKColor(255, 80, 80),
                        IsAntialias = true 
                    };
                    canvas.DrawText(changeText, itemRect.Right - 15 - changeWidth, itemRect.MidY + 15, _fontSmall, changePaint);
                }
                
                y += itemHeight + 5;
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
