using SkiaSharp;

namespace Omnijure.Visual.Features.Search;

public class UiSearchBox
{
    public SKRect Rect;
    public string Text = "";
    public string Placeholder = "Search assets...";
    public bool IsFocused;
    public bool IsHovered;
    public int CursorPosition = 0;

    public UiSearchBox(float x, float y, float w, float h)
    {
        Rect = new SKRect(x, y, x + w, y + h);
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);

    public void AddChar(char c)
    {
        Text = Text.Insert(CursorPosition, c.ToString());
        CursorPosition++;
    }

    public void Backspace()
    {
        if (CursorPosition > 0)
        {
            Text = Text.Remove(CursorPosition - 1, 1);
            CursorPosition--;
        }
    }

    public void Clear()
    {
        Text = "";
        CursorPosition = 0;
    }
}
