namespace Omnijure.Core.Shared.Infrastructure.EventBus;

public class PanelResizedEvent
{
    public string PanelId { get; }
    public float NewWidth { get; }
    public float NewHeight { get; }

    public PanelResizedEvent(string panelId, float newWidth, float newHeight)
    {
        PanelId = panelId;
        NewWidth = newWidth;
        NewHeight = newHeight;
    }
}

public class DockLayoutChangedEvent
{
    public string PanelId { get; }
    public string NewPosition { get; }

    public DockLayoutChangedEvent(string panelId, string newPosition)
    {
        PanelId = panelId;
        NewPosition = newPosition;
    }
}
