using SkiaSharp;

namespace Omnijure.Visual.Widgets.Docking.Model;

/// <summary>
/// Zona de docking visual
/// </summary>
public class DockZone
{
    public PanelPosition Position { get; set; }
    public SKRect PreviewRect { get; set; }

    public DockZone(PanelPosition position, SKRect previewRect)
    {
        Position = position;
        PreviewRect = previewRect;
    }
}
