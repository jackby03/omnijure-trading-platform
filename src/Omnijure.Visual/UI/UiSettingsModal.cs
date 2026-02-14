using Omnijure.Core.Settings;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Rendering;

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
