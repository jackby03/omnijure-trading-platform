
using SkiaSharp;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Rendering;

public class ToolbarRenderer
{
    private readonly SKPaint _bgPaint;
    private readonly SKPaint _textPaint;
    private readonly SKPaint _btnFill;
    private readonly SKPaint _btnHover;
    private readonly SKPaint _dropdownBg;
    private readonly SKFont _font;

    public ToolbarRenderer()
    {
        _bgPaint = new SKPaint { Color = new SKColor(20, 22, 28), Style = SKPaintStyle.Fill };
        _textPaint = new SKPaint { Color = SKColors.White, IsAntialias = true };
        _btnFill = new SKPaint { Color = new SKColor(40, 44, 52), Style = SKPaintStyle.Fill };
        _btnHover = new SKPaint { Color = new SKColor(60, 64, 72), Style = SKPaintStyle.Fill };
        _dropdownBg = new SKPaint { Color = new SKColor(30, 34, 42), Style = SKPaintStyle.Fill };
        _font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 13);
    }

    public void Render(SKCanvas canvas, SKRect rect, List<UiDropdown> dropdowns, List<UiButton> buttons)
    {
        canvas.DrawRect(rect, _bgPaint);
        
        // Render Buttons
        if (buttons != null)
        {
            foreach (var btn in buttons)
            {
                canvas.DrawRoundRect(btn.Rect, 4, 4, btn.IsHovered ? _btnHover : _btnFill);
                float tw = _font.MeasureText(btn.Text, out var bounds);
                canvas.DrawText(btn.Text, btn.Rect.MidX - (tw / 2), btn.Rect.MidY + 5, _font, _textPaint);
            }
        }

        // Render Dropdowns
        if (dropdowns != null)
        {
            foreach (var dd in dropdowns)
            {
                // Main box
                canvas.DrawRoundRect(dd.Rect, 4, 4, dd.IsHovered ? _btnHover : _btnFill);
                
                string stats = dd.Label == "Asset" && dd.CurrentPrice > 0 
                               ? $" | ${dd.CurrentPrice:F2} ({dd.PercentChange:F2}%)" 
                               : "";
                
                string display = $"{dd.Label}: {dd.SelectedItem}{stats}";
                
                using var statsPaint = new SKPaint { 
                    Color = dd.PercentChange >= 0 ? SKColors.LimeGreen : SKColors.Red, 
                    IsAntialias = true 
                };
                
                canvas.DrawText($"{dd.Label}: {dd.SelectedItem}", dd.Rect.Left + 10, dd.Rect.MidY + 5, _font, _textPaint);
                if (!string.IsNullOrEmpty(stats))
                {
                    float labelW = _font.MeasureText($"{dd.Label}: {dd.SelectedItem}");
                    canvas.DrawText(stats, dd.Rect.Left + 10 + labelW, dd.Rect.MidY + 5, _font, statsPaint);
                }

                // Chevron
                canvas.DrawText("v", dd.Rect.Right - 20, dd.Rect.MidY + 5, _font, _textPaint);

                // Items if open
                if (dd.IsOpen)
                {
                    canvas.Save();
                    float itemH = dd.Rect.Height;
                    var filtered = dd.GetFilteredItems();
                    
                    int displayCount = Math.Min(filtered.Count, dd.MaxVisibleItems);
                    float fullH = (displayCount + 1) * itemH; // +1 for Search box
                    var listRect = new SKRect(dd.Rect.Left, dd.Rect.Bottom, dd.Rect.Right, dd.Rect.Bottom + fullH);
                    
                    // Shadow/Border
                    using var shadowPaint = new SKPaint { Color = new SKColor(0,0,0,100), MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 5) };
                    canvas.DrawRect(listRect, shadowPaint);
                    canvas.DrawRect(listRect, _dropdownBg);
                    
                    // Draw Search Bar
                    using var searchPaint = new SKPaint { Color = new SKColor(50, 54, 62), Style = SKPaintStyle.Fill };
                    var searchRect = new SKRect(dd.Rect.Left, dd.Rect.Bottom, dd.Rect.Right, dd.Rect.Bottom + itemH);
                    canvas.DrawRect(searchRect, searchPaint);
                    canvas.DrawText("> " + dd.SearchQuery + "_", searchRect.Left + 10, searchRect.MidY + 5, _font, _textPaint);

                    int startIdx = (int)dd.ScrollOffset;
                    for (int i = 0; i < displayCount; i++)
                    {
                        int actualIdx = startIdx + i;
                        if (actualIdx >= filtered.Count) break;

                        var itemRect = new SKRect(dd.Rect.Left, dd.Rect.Bottom + ((i+1) * itemH), dd.Rect.Right, dd.Rect.Bottom + ((i + 2) * itemH));
                        canvas.DrawText(filtered[actualIdx], itemRect.Left + 10, itemRect.MidY + 5, _font, _textPaint);
                    }
                    
                    // Scrollbar
                    if (filtered.Count > dd.MaxVisibleItems)
                    {
                        float scrollTrackH = displayCount * itemH;
                        float scrollThumbH = (dd.MaxVisibleItems / (float)filtered.Count) * scrollTrackH;
                        float scrollThumbY = (dd.ScrollOffset / (float)filtered.Count) * scrollTrackH;
                        
                        var thumbRect = new SKRect(dd.Rect.Right - 6, dd.Rect.Bottom + itemH + scrollThumbY, dd.Rect.Right - 2, dd.Rect.Bottom + itemH + scrollThumbY + scrollThumbH);
                        using var scrollPaint = new SKPaint { Color = new SKColor(100, 100, 100), Style = SKPaintStyle.Fill };
                        canvas.DrawRoundRect(thumbRect, 2, 2, scrollPaint);
                    }
                    
                    canvas.Restore();
                }
            }
        }
    }
}
