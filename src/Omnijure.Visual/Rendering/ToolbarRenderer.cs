
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
                string display = $"{dd.Label}: {dd.SelectedItem}";
                float tw = _font.MeasureText(display, out var bounds);
                canvas.DrawText(display, dd.Rect.Left + 10, dd.Rect.MidY + 5, _font, _textPaint);

                // Chevron (optional)
                canvas.DrawText("v", dd.Rect.Right - 20, dd.Rect.MidY + 5, _font, _textPaint);

                // Items if open
                if (dd.IsOpen)
                {
                    canvas.Save();
                    // We need to render ABOVE everything else. 
                    // Usually this is handled by a post-render pass or by drawing last.
                    // For now, simple vertical list.
                    float itemH = dd.Rect.Height;
                    float fullH = dd.Items.Count * itemH;
                    var listRect = new SKRect(dd.Rect.Left, dd.Rect.Bottom, dd.Rect.Right, dd.Rect.Bottom + fullH);
                    
                    canvas.DrawRect(listRect, _dropdownBg);
                    
                    for (int i = 0; i < dd.Items.Count; i++)
                    {
                        var itemRect = new SKRect(dd.Rect.Left, dd.Rect.Bottom + (i * itemH), dd.Rect.Right, dd.Rect.Bottom + ((i + 1) * itemH));
                        
                        // Highlight if mouse is here (needs global hover check)
                        // For now just draw text
                        canvas.DrawText(dd.Items[i], itemRect.Left + 10, itemRect.MidY + 5, _font, _textPaint);
                    }
                    canvas.Restore();
                }
            }
        }
    }
}
