using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Omnijure TDS panel layout.
/// Default: AI Assistant (left) | Chart (center) | Portfolio (right) | OrderBook (bottom)
/// Hidden: Script Editor, Trades, Positions, Alerts, Logs
/// </summary>
public static class PanelDefinitions
{
    // Core
    public const string CHART = "chart";

    // Visible by default
    public const string AI_ASSISTANT = "ai_assistant";
    public const string PORTFOLIO = "portfolio";
    public const string ORDERBOOK = "orderbook";

    // Hidden by default
    public const string TRADES = "trades";
    public const string POSITIONS = "positions";
    public const string SCRIPT_EDITOR = "script_editor";
    public const string ALERTS = "alerts";
    public const string LOGS = "logs";

    public static readonly Dictionary<string, PanelConfig> Panels = new()
    {
        [CHART] = new PanelConfig
        {
            Id = CHART, DisplayName = "Chart", Icon = SvgIconRenderer.Icon.Chart,
            DefaultPosition = PanelPosition.Center, DefaultWidth = 800, DefaultHeight = 600,
            CanClose = false, CanCollapse = false, CanFloat = false
        },
        [AI_ASSISTANT] = new PanelConfig
        {
            Id = AI_ASSISTANT, DisplayName = "AI Assistant", Icon = SvgIconRenderer.Icon.Star,
            DefaultPosition = PanelPosition.Left, DefaultWidth = 320, DefaultHeight = 600,
            CanClose = true, CanCollapse = true, CanFloat = true
        },
        [PORTFOLIO] = new PanelConfig
        {
            Id = PORTFOLIO, DisplayName = "Portfolio", Icon = SvgIconRenderer.Icon.Wallet,
            DefaultPosition = PanelPosition.Right, DefaultWidth = 280, DefaultHeight = 600,
            CanClose = true, CanCollapse = true, CanFloat = true
        },
        [ORDERBOOK] = new PanelConfig
        {
            Id = ORDERBOOK, DisplayName = "Order Book", Icon = SvgIconRenderer.Icon.OrderBook,
            DefaultPosition = PanelPosition.Bottom, DefaultWidth = 800, DefaultHeight = 220,
            CanClose = true, CanCollapse = true, CanFloat = true
        },
        [TRADES] = new PanelConfig
        {
            Id = TRADES, DisplayName = "Trades", Icon = SvgIconRenderer.Icon.Exchange,
            DefaultPosition = PanelPosition.Bottom, DefaultWidth = 800, DefaultHeight = 220,
            CanClose = true, CanCollapse = true, CanFloat = true
        },
        [POSITIONS] = new PanelConfig
        {
            Id = POSITIONS, DisplayName = "Positions", Icon = SvgIconRenderer.Icon.Wallet,
            DefaultPosition = PanelPosition.Bottom, DefaultWidth = 800, DefaultHeight = 220,
            CanClose = true, CanCollapse = true, CanFloat = true
        },
        [SCRIPT_EDITOR] = new PanelConfig
        {
            Id = SCRIPT_EDITOR, DisplayName = "Script Editor", Icon = SvgIconRenderer.Icon.Settings,
            DefaultPosition = PanelPosition.Right, DefaultWidth = 400, DefaultHeight = 600,
            CanClose = true, CanCollapse = true, CanFloat = true, StartClosed = true
        },
        [ALERTS] = new PanelConfig
        {
            Id = ALERTS, DisplayName = "Alerts", Icon = SvgIconRenderer.Icon.Bell,
            DefaultPosition = PanelPosition.Bottom, DefaultWidth = 800, DefaultHeight = 180,
            CanClose = true, CanCollapse = true, CanFloat = true, StartClosed = true
        },
        [LOGS] = new PanelConfig
        {
            Id = LOGS, DisplayName = "Console", Icon = SvgIconRenderer.Icon.Info,
            DefaultPosition = PanelPosition.Bottom, DefaultWidth = 800, DefaultHeight = 200,
            CanClose = true, CanCollapse = true, CanFloat = true, StartClosed = true
        }
    };
}

/// <summary>
/// Configuración de un panel individual
/// </summary>
public class PanelConfig
{
    public string Id { get; set; } = "";
    public string DisplayName { get; set; } = "";
    public SvgIconRenderer.Icon Icon { get; set; }
    public PanelPosition DefaultPosition { get; set; }
    public float DefaultWidth { get; set; }
    public float DefaultHeight { get; set; }
    public bool CanClose { get; set; }
    public bool CanCollapse { get; set; }
    public bool CanFloat { get; set; }
    public bool StartClosed { get; set; }
}

/// <summary>
/// Posiciones posibles de los paneles
/// </summary>
public enum PanelPosition
{
    Left,
    Right,
    Bottom,
    Top,
    Center,
    Floating
}

/// <summary>
/// Zonas de docking avanzadas (VS Code style)
/// </summary>
public enum DockRegion
{
    Left,
    Right,
    Top,
    Bottom,
    Center,
    SplitLeft,      // Dividir actual panel a la izquierda
    SplitRight,     // Dividir actual panel a la derecha
    SplitTop,       // Dividir actual panel arriba
    SplitBottom     // Dividir actual panel abajo
}
