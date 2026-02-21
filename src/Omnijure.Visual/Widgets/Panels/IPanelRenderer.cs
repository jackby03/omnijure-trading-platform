using SkiaSharp;

namespace Omnijure.Visual.Widgets.Panels;

public interface IPanelRenderer
{
    string PanelId { get; }
    void Render(SKCanvas canvas, SKRect rect, float scrollY);
    float GetContentHeight();
}
