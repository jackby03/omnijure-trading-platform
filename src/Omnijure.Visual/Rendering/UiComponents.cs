
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

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
    public string SearchQuery = "";
    public float ScrollOffset = 0;
    public int MaxVisibleItems = 15;
    
    // Stats for Display
    public float CurrentPrice = 0;
    public float PercentChange = 0;

    public UiDropdown(float x, float y, float w, float h, string label, List<string> items, Action<string> onSelected)
    {
        Rect = new SKRect(x, y, x + w, y + h);
        Label = label;
        Items = items;
        OnSelected = onSelected;
        SelectedItem = items.Count > 0 ? items[0] : "";
    }

    public List<string> GetFilteredItems()
    {
        if (string.IsNullOrEmpty(SearchQuery)) return Items;
        return Items.Where(i => i.Contains(SearchQuery, StringComparison.OrdinalIgnoreCase)).ToList();
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);

    public bool ContainsItem(float x, float y, int index)
    {
        if (!IsOpen) return false;
        // Adjust for index relative to viewport (scrolling)
        float visibleIndex = index - ScrollOffset;
        if (visibleIndex < 0 || visibleIndex >= MaxVisibleItems) return false;

        // Item 0 is Search Bar, so actual items start at 1.
        var itemRect = new SKRect(Rect.Left, Rect.Bottom + ((visibleIndex + 1) * Rect.Height), Rect.Right, Rect.Bottom + ((visibleIndex + 2) * Rect.Height));
        return itemRect.Contains(x, y);
    }
}
