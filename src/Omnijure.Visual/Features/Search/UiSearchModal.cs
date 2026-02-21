using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Features.Search;

public enum AssetCategory
{
    All,
    Crypto,
    Forex,
    Stocks,
    Futures,
    Indices,
    Bonds,
    Options
}

public class SearchResult
{
    public string Symbol { get; set; }
    public string BaseSymbol { get; set; } // e.g., "BTC" from "BTCUSDT"
    public AssetCategory Category { get; set; }
    public List<string> Exchanges { get; set; } // Multiple exchanges
    public string PrimaryExchange { get; set; } // Default exchange to show
    public float Price { get; set; }
    public float PercentChange { get; set; }

    public SearchResult(string symbol, string baseSymbol = "", AssetCategory category = AssetCategory.Crypto, List<string>? exchanges = null, float price = 0, float percentChange = 0)
    {
        Symbol = symbol;
        BaseSymbol = string.IsNullOrEmpty(baseSymbol) ? symbol : baseSymbol;
        Category = category;
        Exchanges = exchanges ?? new List<string> { "Binance" };
        PrimaryExchange = Exchanges.FirstOrDefault() ?? "Binance";
        Price = price;
        PercentChange = percentChange;
    }
}

public class UiSearchModal
{
    public bool IsVisible;
    public string SearchText = "";
    public AssetCategory SelectedCategory = AssetCategory.All;
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

    public void UpdateFilteredResults()
    {
        IEnumerable<string> query = _allSymbols;

        // Filter by search text
        if (!string.IsNullOrEmpty(SearchText))
        {
            query = query.Where(s => s.Contains(SearchText, StringComparison.OrdinalIgnoreCase));
        }

        // Filter by category (for now, all symbols are crypto from Binance)
        if (SelectedCategory != AssetCategory.All)
        {
            query = query.Where(s => true); // All Binance symbols are crypto
        }

        _filteredResults = query
            .Take(100)
            .Select(s => CreateSearchResult(s))
            .ToList();
    }

    private SearchResult CreateSearchResult(string symbol)
    {
        // Extract base symbol (e.g., "BTC" from "BTCUSDT")
        string baseSymbol = symbol;
        if (symbol.EndsWith("USDT"))
            baseSymbol = symbol[..^4];
        else if (symbol.EndsWith("BTC"))
            baseSymbol = symbol[..^3];
        else if (symbol.EndsWith("ETH"))
            baseSymbol = symbol[..^3];

        // For now, all symbols are from Binance, but we can add more exchanges later
        var exchanges = new List<string> { "Binance" };

        // Could add: "Coinbase", "Kraken", "Bitfinex", etc.
        // For popular pairs like BTCUSDT, we could show multiple exchanges
        if (symbol == "BTCUSDT" || symbol == "ETHUSDT" || symbol == "BNBUSDT")
        {
            exchanges.AddRange(new[] { "Coinbase", "Kraken", "Bitfinex" });
        }

        return new SearchResult(symbol, baseSymbol, AssetCategory.Crypto, exchanges);
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
