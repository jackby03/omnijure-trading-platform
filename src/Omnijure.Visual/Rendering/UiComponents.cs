
using Omnijure.Core.Settings;
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

// ─── Text Input ──────────────────────────────────────────────────

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

// ─── Toggle ──────────────────────────────────────────────────────

public class UiToggle
{
    public SKRect Rect;
    public string Label = "";
    public string Description = "";
    public bool IsOn;
    public bool IsHovered;
    public float AnimationProgress;

    public UiToggle(string label, bool defaultValue = false, string description = "")
    {
        Label = label;
        IsOn = defaultValue;
        AnimationProgress = defaultValue ? 1f : 0f;
        Description = description;
    }

    public bool Contains(float x, float y) => Rect.Contains(x, y);

    public void Toggle()
    {
        IsOn = !IsOn;
    }

    public void UpdateAnimation(float dt)
    {
        float target = IsOn ? 1f : 0f;
        AnimationProgress += (target - AnimationProgress) * Math.Min(1f, dt * 12f);
    }
}

// ─── Settings Modal ──────────────────────────────────────────────

public enum SettingsSection
{
    Exchange,
    General,
    Chart,
    Layout
}

public class UiSettingsModal
{
    public bool IsVisible;
    public float AnimationProgress;
    public SettingsSection ActiveSection = SettingsSection.General;

    // Exchange section
    public List<ExchangeCredential> Credentials = new();
    public int SelectedCredentialIndex = -1;
    public bool IsEditingCredential;
    public UiTextInput CredentialName = new("Name", "My Binance Account");
    public UiTextInput ApiKeyInput = new("API Key", "Enter API key...");
    public UiTextInput ApiSecretInput = new("Secret", "Enter secret...", isPassword: true);
    public int SelectedExchangeType; // ExchangeType enum index
    public UiToggle TestnetToggle = new("Testnet Mode", false, "Use testnet API endpoints");
    public string? TestConnectionStatus;

    // General section
    public UiToggle RestoreSessionToggle = new("Restore Last Session", true, "Reopen last symbol and layout on startup");
    public UiToggle MinimizeToTrayToggle = new("Minimize to Tray", false, "Keep running in system tray when closed");

    // Chart section
    public string SelectedSymbol = "BTCUSDT";
    public string SelectedTimeframe = "1m";
    public string SelectedChartType = "Candles";
    public float SelectedZoom = 1.0f;
    public UiToggle ShowVolumeToggle = new("Show Volume", true, "Display volume bars below chart");
    public UiToggle ShowGridToggle = new("Show Grid", true, "Display price/time grid lines");

    // Input focus
    public UiTextInput? FocusedInput;
    public bool HasUnsavedChanges;

    // All text inputs for tab cycling
    public UiTextInput[] AllInputs => new[] { CredentialName, ApiKeyInput, ApiSecretInput };
    // All toggles for animation update
    public UiToggle[] AllToggles => new[] { TestnetToggle, RestoreSessionToggle, MinimizeToTrayToggle, ShowVolumeToggle, ShowGridToggle };

    public void Open()
    {
        IsVisible = true;
        AnimationProgress = 0f;
        HasUnsavedChanges = false;
        TestConnectionStatus = null;
    }

    public void Close()
    {
        IsVisible = false;
        FocusedInput = null;
        IsEditingCredential = false;
    }

    public void LoadFromSettings(AppSettings settings)
    {
        // Exchange
        Credentials = settings.Exchange.Credentials
            .Select(c => new ExchangeCredential
            {
                Id = c.Id, Name = c.Name, Exchange = c.Exchange,
                ApiKey = c.ApiKey, Secret = c.Secret,
                EncryptedApiKey = c.EncryptedApiKey, EncryptedSecret = c.EncryptedSecret,
                IsTestnet = c.IsTestnet, CreatedAt = c.CreatedAt
            }).ToList();
        SelectedCredentialIndex = -1;
        IsEditingCredential = false;

        // General
        RestoreSessionToggle.IsOn = settings.General.RestoreLastSession;
        MinimizeToTrayToggle.IsOn = settings.General.MinimizeToTray;

        // Chart
        SelectedSymbol = settings.Chart.DefaultSymbol;
        SelectedTimeframe = settings.Chart.DefaultTimeframe;
        SelectedChartType = settings.Chart.DefaultChartType;
        SelectedZoom = settings.Chart.DefaultZoom;
        ShowVolumeToggle.IsOn = settings.Chart.ShowVolume;
        ShowGridToggle.IsOn = settings.Chart.ShowGrid;

        HasUnsavedChanges = false;
    }

    public void SaveToSettings(AppSettings settings)
    {
        // Exchange
        settings.Exchange.Credentials = Credentials;

        // General
        settings.General.RestoreLastSession = RestoreSessionToggle.IsOn;
        settings.General.MinimizeToTray = MinimizeToTrayToggle.IsOn;

        // Chart
        settings.Chart.DefaultSymbol = SelectedSymbol;
        settings.Chart.DefaultTimeframe = SelectedTimeframe;
        settings.Chart.DefaultChartType = SelectedChartType;
        settings.Chart.DefaultZoom = SelectedZoom;
        settings.Chart.ShowVolume = ShowVolumeToggle.IsOn;
        settings.Chart.ShowGrid = ShowGridToggle.IsOn;
    }

    public void StartNewCredential()
    {
        IsEditingCredential = true;
        SelectedCredentialIndex = -1;
        CredentialName.Clear();
        ApiKeyInput.Clear();
        ApiSecretInput.Clear();
        SelectedExchangeType = 0;
        TestnetToggle.IsOn = false;
        TestConnectionStatus = null;
    }

    public void EditCredential(int index)
    {
        if (index < 0 || index >= Credentials.Count) return;
        var c = Credentials[index];
        IsEditingCredential = true;
        SelectedCredentialIndex = index;
        CredentialName.Text = c.Name;
        CredentialName.CursorPosition = c.Name.Length;
        ApiKeyInput.Text = c.ApiKey;
        ApiKeyInput.CursorPosition = c.ApiKey.Length;
        ApiSecretInput.Text = c.Secret;
        ApiSecretInput.CursorPosition = c.Secret.Length;
        SelectedExchangeType = (int)c.Exchange;
        TestnetToggle.IsOn = c.IsTestnet;
        TestConnectionStatus = null;
    }

    public void SaveCurrentCredential()
    {
        var cred = SelectedCredentialIndex >= 0 && SelectedCredentialIndex < Credentials.Count
            ? Credentials[SelectedCredentialIndex]
            : new ExchangeCredential();

        cred.Name = CredentialName.Text;
        cred.ApiKey = ApiKeyInput.Text;
        cred.Secret = ApiSecretInput.Text;
        cred.Exchange = (ExchangeType)SelectedExchangeType;
        cred.IsTestnet = TestnetToggle.IsOn;

        if (SelectedCredentialIndex < 0 || SelectedCredentialIndex >= Credentials.Count)
            Credentials.Add(cred);

        IsEditingCredential = false;
        HasUnsavedChanges = true;
    }

    public void DeleteCredential(int index)
    {
        if (index >= 0 && index < Credentials.Count)
        {
            Credentials.RemoveAt(index);
            HasUnsavedChanges = true;
            if (IsEditingCredential && SelectedCredentialIndex == index)
                IsEditingCredential = false;
        }
    }
}
