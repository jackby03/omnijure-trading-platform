using Omnijure.Core.Features.Settings.Model;
using SkiaSharp;
using System.Collections.Generic;

namespace Omnijure.Visual.Widgets.Docking.Api;

public interface IDockingManager
{
    // Layout
    void UpdateLayout(int screenWidth, int screenHeight, float headerHeight);
    SKRect GetChartArea(int screenWidth, int screenHeight, float headerHeight);

    // Panel access
    DockablePanel? GetPanel(string id);
    IReadOnlyCollection<DockablePanel> Panels { get; }
    void TogglePanel(string panelId);

    // Input
    void OnMouseDown(float x, float y);
    void OnMouseMove(float x, float y, int screenWidth, int screenHeight, float headerHeight);
    void OnMouseUp(float x, float y, int screenWidth, int screenHeight);

    // State queries
    bool IsDragging { get; }
    bool HasPendingDrag { get; }
    bool IsResizing { get; }
    DockablePanel? DraggingPanel { get; }
    DockablePanel? ResizingPanel { get; }
    DockablePanel? ActivePanel { get; }
    string? HoveredHandle { get; }
    DockZone? CurrentDockZone { get; }
    bool IsMouseOverPanel(float x, float y);

    // Tab management
    string ActiveBottomTabId { get; }
    Dictionary<PanelPosition, string> ActiveTabIds { get; }
    SKRect BottomTabBarRect { get; }
    IReadOnlyList<(string id, SKRect rect)> BottomTabRects { get; }
    Dictionary<PanelPosition, List<(string id, SKRect rect)>> SideTabRects { get; }
    Dictionary<PanelPosition, SKRect> SideTabBarRects { get; }

    // Cached screen dims
    float LastHeaderHeight { get; }
    int LastScreenWidth { get; }
    int LastScreenHeight { get; }

    // Persistence
    List<PanelState> ExportLayout();
    void ImportLayout(List<PanelState> states);
    void ImportActiveTabs(string bottom, string left, string right, string center = "");
    (string bottom, string left, string right, string center) ExportActiveTabs();

    // Panel-specific
    void UpdateChartTitle(string symbol, string interval, float price);
    string GetActiveCenterTabId();
    bool IsBottomTabActive(DockablePanel panel);
}
