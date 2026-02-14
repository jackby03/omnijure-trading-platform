using SkiaSharp;

namespace Omnijure.Visual.Rendering;

public class UiTextInput
{
    public SKRect Rect;
    public string Text = "";
    public string Placeholder = "";
    public string Label = "";
    public bool IsFocused;
    public bool IsHovered;
    public bool IsPassword;
    public bool IsReadOnly;
    public int CursorPosition;
    public int MaxLength = 256;

    public UiTextInput(string label, string placeholder = "", bool isPassword = false)
    {
        Label = label;
        Placeholder = placeholder;
        IsPassword = isPassword;
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);

    public string DisplayText => IsPassword ? new string('\u2022', Text.Length) : Text;

    public void AddChar(char c)
    {
        if (IsReadOnly || Text.Length >= MaxLength) return;
        Text = Text.Insert(CursorPosition, c.ToString());
        CursorPosition++;
    }

    public void Backspace()
    {
        if (IsReadOnly || CursorPosition <= 0) return;
        Text = Text.Remove(CursorPosition - 1, 1);
        CursorPosition--;
    }

    public void Delete()
    {
        if (IsReadOnly || CursorPosition >= Text.Length) return;
        Text = Text.Remove(CursorPosition, 1);
    }

    public void SelectAll()
    {
        CursorPosition = Text.Length;
    }

    public void Clear()
    {
        Text = "";
        CursorPosition = 0;
    }
}
