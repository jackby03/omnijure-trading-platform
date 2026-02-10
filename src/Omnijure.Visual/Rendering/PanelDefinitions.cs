using System.Collections.Generic;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Definiciones centralizadas de todos los paneles de la aplicación.
/// Sistema de nomenclatura claro para referencia fácil.
/// </summary>
public static class PanelDefinitions
{
    // ???????????????????????????????????????????????????????????
    // IDENTIFICADORES DE PANELES
    // ???????????????????????????????????????????????????????????
    
    public const string CHART = "chart";
    public const string ORDERBOOK = "orderbook";
    public const string TRADES = "trades";
    public const string WATCHLIST = "watchlist";
    public const string POSITIONS = "positions";
    public const string INDICATORS = "indicators";
    public const string DRAWINGS = "drawings";
    public const string ALERTS = "alerts";

    /// <summary>
    /// Configuración de todos los paneles disponibles
    /// </summary>
    public static readonly Dictionary<string, PanelConfig> Panels = new()
    {
        [CHART] = new PanelConfig
        {
            Id = CHART,
            DisplayName = "Chart",
            Icon = SvgIconRenderer.Icon.Chart,
            DefaultPosition = PanelPosition.Center,
            DefaultWidth = 800,
            DefaultHeight = 600,
            CanClose = false,
            CanCollapse = false,
            CanFloat = false
        },
        [ORDERBOOK] = new PanelConfig
        {
            Id = ORDERBOOK,
            DisplayName = "Order Book",
            Icon = SvgIconRenderer.Icon.OrderBook,
            DefaultPosition = PanelPosition.Right,
            DefaultWidth = 320,
            DefaultHeight = 600,
            CanClose = true,
            CanCollapse = true,
            CanFloat = true
        },
        [TRADES] = new PanelConfig
        {
            Id = TRADES,
            DisplayName = "Trades",
            Icon = SvgIconRenderer.Icon.Exchange,
            DefaultPosition = PanelPosition.Right,
            DefaultWidth = 320,
            DefaultHeight = 400,
            CanClose = true,
            CanCollapse = true,
            CanFloat = true
        },
        [WATCHLIST] = new PanelConfig
        {
            Id = WATCHLIST,
            DisplayName = "Watchlist",
            Icon = SvgIconRenderer.Icon.Star,
            DefaultPosition = PanelPosition.Left,
            DefaultWidth = 280,
            DefaultHeight = 600,
            CanClose = true,
            CanCollapse = true,
            CanFloat = true
        },
        [POSITIONS] = new PanelConfig
        {
            Id = POSITIONS,
            DisplayName = "Positions",
            Icon = SvgIconRenderer.Icon.Wallet,
            DefaultPosition = PanelPosition.Bottom,
            DefaultWidth = 800,
            DefaultHeight = 200,
            CanClose = true,
            CanCollapse = true,
            CanFloat = true
        },
        [INDICATORS] = new PanelConfig
        {
            Id = INDICATORS,
            DisplayName = "Indicators",
            Icon = SvgIconRenderer.Icon.Chart,
            DefaultPosition = PanelPosition.Left,
            DefaultWidth = 280,
            DefaultHeight = 400,
            CanClose = true,
            CanCollapse = true,
            CanFloat = true
        },
        [DRAWINGS] = new PanelConfig
        {
            Id = DRAWINGS,
            DisplayName = "Drawings",
            Icon = SvgIconRenderer.Icon.TrendLine,
            DefaultPosition = PanelPosition.Left,
            DefaultWidth = 260,
            DefaultHeight = 300,
            CanClose = true,
            CanCollapse = true,
            CanFloat = true
        },
        [ALERTS] = new PanelConfig
        {
            Id = ALERTS,
            DisplayName = "Alerts",
            Icon = SvgIconRenderer.Icon.Bell,
            DefaultPosition = PanelPosition.Bottom,
            DefaultWidth = 800,
            DefaultHeight = 180,
            CanClose = true,
            CanCollapse = true,
            CanFloat = true
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
