
using SkiaSharp;
using System;
using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

public enum ChartType { Candles, Line, Area, Bars }

public class UiButton 
{
    public SKRect Rect;
    public string Text;
    public Action Action;
    public bool IsHovered;

    public UiButton(float x, float y, float w, float h, string text, Action action)
    {
        Rect = new SKRect(x, y, x + w, y + h);
        Text = text;
        Action = action;
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);
}

public class UiDropdown
{
    public SKRect Rect;
    public string Label;
    public List<string> Items;
    public Action<string> OnSelected;
    public bool IsOpen;
    public bool IsHovered;
    public string SelectedItem;

    public UiDropdown(float x, float y, float w, float h, string label, List<string> items, Action<string> onSelected)
    {
        Rect = new SKRect(x, y, x + w, y + h);
        Label = label;
        Items = items;
        OnSelected = onSelected;
        SelectedItem = items.Count > 0 ? items[0] : "";
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);

    public bool ContainsItem(float x, float y, int index)
    {
        if (!IsOpen) return false;
        var itemRect = new SKRect(Rect.Left, Rect.Bottom + (index * Rect.Height), Rect.Right, Rect.Bottom + ((index + 1) * Rect.Height));
        return itemRect.Contains(x, y);
    }
}
