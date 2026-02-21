namespace Omnijure.Core.Features.Settings.Model;

public class AppSettings
{
    public int Version { get; set; } = 1;
    public ExchangeSettings Exchange { get; set; } = new();
    public GeneralSettings General { get; set; } = new();
    public ChartSettings Chart { get; set; } = new();
    public LayoutSettings Layout { get; set; } = new();
}

public class ExchangeSettings
{
    public List<ExchangeCredential> Credentials { get; set; } = new();
    public string ActiveCredentialId { get; set; } = "";
}

public class GeneralSettings
{
    public string Language { get; set; } = "en";
    public string Theme { get; set; } = "dark";
    public bool RestoreLastSession { get; set; } = true;
    public bool MinimizeToTray { get; set; }
}

public class ChartSettings
{
    public string DefaultSymbol { get; set; } = "BTCUSDT";
    public string DefaultTimeframe { get; set; } = "1m";
    public string DefaultChartType { get; set; } = "Candles";
    public float DefaultZoom { get; set; } = 1.0f;
    public bool ShowVolume { get; set; } = true;
    public bool ShowGrid { get; set; } = true;
    public List<ChartTabSaved> Tabs { get; set; } = new();
    public int ActiveTabIndex { get; set; } = 0;
}

public class ChartTabSaved
{
    public string Symbol { get; set; } = "BTCUSDT";
    public string Timeframe { get; set; } = "1m";
    public string ChartType { get; set; } = "Candles";
    public float Zoom { get; set; } = 1.0f;
}

public class LayoutSettings
{
    public List<PanelState> Panels { get; set; } = new();
    public string ActiveBottomTab { get; set; } = "orderbook";
    public string ActiveLeftTab { get; set; } = "ai_assistant";
    public string ActiveRightTab { get; set; } = "portfolio";
    public string ActiveCenterTab { get; set; } = "chart";
    public int WindowWidth { get; set; } = 1440;
    public int WindowHeight { get; set; } = 900;
    public int WindowX { get; set; } = -1;
    public int WindowY { get; set; } = -1;
    public bool IsMaximized { get; set; }
}

public class PanelState
{
    public string Id { get; set; } = "";
    public string Position { get; set; } = "Left";
    public float Width { get; set; }
    public float Height { get; set; }
    public bool IsClosed { get; set; }
    public bool IsCollapsed { get; set; }
    public bool IsFloating { get; set; }
    public int DockOrder { get; set; }
}
