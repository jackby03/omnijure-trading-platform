using SkiaSharp;

namespace Omnijure.Visual.Widgets.Docking.Model;

/// <summary>
/// Panel dockable individual
/// </summary>
public class DockablePanel
{
    public PanelConfig Config { get; }
    public PanelPosition Position { get; set; }
    public SKRect Bounds { get; set; }
    public SKRect ContentBounds { get; set; }
    public SKRect DragHandleBounds { get; set; }
    public SKRect CollapseHandleBounds { get; set; }
    public SKRect CloseHandleBounds { get; set; }
    public float Width { get; set; }
    public float Height { get; set; }
    public float FloatingWidth { get; set; }
    public float FloatingHeight { get; set; }
    public bool IsCollapsed { get; set; }
    public bool IsFloating { get; set; }
    public bool IsClosed { get; set; }
    public int DockOrder { get; set; }

    /// <summary>
    /// Título dinámico (ej: "BTCUSDT · 1m" para el chart)
    /// </summary>
    public string? DynamicTitle { get; set; }

    public DockablePanel(PanelConfig config)
    {
        Config = config;
        Position = config.DefaultPosition;
        Width = config.DefaultWidth;
        Height = config.DefaultHeight;
        IsCollapsed = false;
        IsFloating = false;
        IsClosed = false;
        DockOrder = 0;
    }
}
