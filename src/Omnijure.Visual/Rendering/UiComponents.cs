
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

public class SearchResult
{
    public string Symbol { get; set; }
    public string Exchange { get; set; }
    public float Price { get; set; }
    public float PercentChange { get; set; }
    
    public SearchResult(string symbol, string exchange = "Binance", float price = 0, float percentChange = 0)
    {
        Symbol = symbol;
        Exchange = exchange;
        Price = price;
        PercentChange = percentChange;
    }
}

public class UiSearchModal
{
    public bool IsVisible;
    public string SearchText = "";
    public int SelectedIndex = 0;
    public int ScrollOffset = 0;
    public int MaxVisibleResults = 10;
    public float AnimationProgress = 0f; // 0 to 1 for fade in/out
    
    private List<string> _allSymbols = new List<string>();
    private List<SearchResult> _filteredResults = new List<SearchResult>();
    
    public UiSearchModal()
    {
    }
    
    public void SetSymbols(List<string> symbols)
    {
        _allSymbols = symbols;
        UpdateFilteredResults();
    }
    
    public void UpdateSearchText(string text)
    {
        SearchText = text;
        SelectedIndex = 0;
        ScrollOffset = 0;
        UpdateFilteredResults();
    }
    
    public void AddChar(char c)
    {
        SearchText += c;
        SelectedIndex = 0;
        ScrollOffset = 0;
        UpdateFilteredResults();
    }
    
    public void Backspace()
    {
        if (SearchText.Length > 0)
        {
            SearchText = SearchText[..^1];
            SelectedIndex = 0;
            ScrollOffset = 0;
            UpdateFilteredResults();
        }
    }
    
    public void Clear()
    {
        SearchText = "";
        SelectedIndex = 0;
        ScrollOffset = 0;
        UpdateFilteredResults();
    }
    
    public void MoveSelectionUp()
    {
        if (SelectedIndex > 0)
        {
            SelectedIndex--;
            if (SelectedIndex < ScrollOffset)
            {
                ScrollOffset = SelectedIndex;
            }
        }
    }
    
    public void MoveSelectionDown()
    {
        if (SelectedIndex < _filteredResults.Count - 1)
        {
            SelectedIndex++;
            if (SelectedIndex >= ScrollOffset + MaxVisibleResults)
            {
                ScrollOffset = SelectedIndex - MaxVisibleResults + 1;
            }
        }
    }
    
    public string GetSelectedSymbol()
    {
        if (_filteredResults.Count > 0 && SelectedIndex < _filteredResults.Count)
        {
            return _filteredResults[SelectedIndex].Symbol;
        }
        return null;
    }
    
    public List<SearchResult> GetVisibleResults()
    {
        return _filteredResults.Skip(ScrollOffset).Take(MaxVisibleResults).ToList();
    }
    
    public int GetTotalResultCount()
    {
        return _filteredResults.Count;
    }
    
    private void UpdateFilteredResults()
    {
        if (string.IsNullOrEmpty(SearchText))
        {
            _filteredResults = _allSymbols.Take(50).Select(s => new SearchResult(s)).ToList();
        }
        else
        {
            _filteredResults = _allSymbols
                .Where(s => s.Contains(SearchText, StringComparison.OrdinalIgnoreCase))
                .Take(100)
                .Select(s => new SearchResult(s))
                .ToList();
        }
    }
    
    public void UpdatePriceData(Dictionary<string, (float price, float change)> priceData)
    {
        foreach (var result in _filteredResults)
        {
            if (priceData.TryGetValue(result.Symbol, out var data))
            {
                result.Price = data.price;
                result.PercentChange = data.change;
            }
        }
    }
}
