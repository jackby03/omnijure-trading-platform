using Omnijure.Core.Features.Settings.Model;
using Omnijure.Core.Shared.Infrastructure.EventBus;
using Omnijure.Visual.Widgets.Docking.Api;
using Omnijure.Visual.Widgets.Docking.Lib;
using Omnijure.Visual.Widgets.Docking.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Widgets.Docking;

/// <summary>
/// Manages panel docking layout, resize, drag, and tab state.
/// Orchestrates ResizeHandler and DragHandler for input processing.
/// </summary>
public class DockingManager : IDockingManager
{
    private readonly Dictionary<string, DockablePanel> _panels = new();
    private readonly ResizeHandler _resizeHandler = new();
    private readonly DragHandler _dragHandler = new();
    private readonly IEventBus _eventBus;
    private readonly IDockLayoutStrategy[] _layoutStrategies =
    [
        new SideDockStrategy(),
        new BottomDockStrategy(),
        new CenterDockStrategy()
    ];

    // Hover state
    private DockablePanel? _hoveredPanel;
    public string? HoveredHandle => _hoveredHandle;
    private string? _hoveredHandle;
    public DockablePanel? ActivePanel => _activePanel;
    private DockablePanel? _activePanel;

    // Delegated state
    public DockablePanel? DraggingPanel => _dragHandler.DraggingPanel;
    public DockablePanel? ResizingPanel => _resizeHandler.ResizingPanel;
    public DockZone? CurrentDockZone => _dragHandler.CurrentDockZone;
    public bool IsDragging => _dragHandler.IsDragging;
    public bool HasPendingDrag => _dragHandler.HasPendingDrag;
    public bool IsResizing => _resizeHandler.IsResizing;

    // Layout constants
    internal const float HandleSize = 24f;
    internal const float CollapsedWidth = 32f;
    internal const float HandlePadding = 6f;
    internal const float PanelGap = 4f;
    internal const float MinCenterWidth = 200f;

    // Cached screen dims
    public float LastHeaderHeight => _lastHeaderHeight;
    private float _lastHeaderHeight;
    public int LastScreenWidth => _lastScreenWidth;
    private int _lastScreenWidth;
    public int LastScreenHeight => _lastScreenHeight;
    private int _lastScreenHeight;

    // Tab layout constants
    internal const float TabBarHeight = 28f;
    private const float TabPaddingX = 6f;
    private const float TabIconWidth = 16f;
    private const float TabRightPad = 8f;
    private const float TabSpacing = 2f;
    private const float TabInsetY = 3f;
    private static readonly SKFont _tabFont = new(SKTypeface.FromFamilyName("Segoe UI"), 11);

    // Bottom tab system
    public string ActiveBottomTabId => _activeBottomTabId;
    private string _activeBottomTabId = PanelDefinitions.ORDERBOOK;
    public SKRect BottomTabBarRect => _bottomTabBarRect;
    private SKRect _bottomTabBarRect;
    public IReadOnlyList<(string id, SKRect rect)> BottomTabRects => _bottomTabRects;
    internal List<(string id, SKRect rect)> _bottomTabRects = new();

    // Side tab system
    public Dictionary<PanelPosition, string> ActiveTabIds => _activeTabIds;
    private readonly Dictionary<PanelPosition, string> _activeTabIds = new()
    {
        [PanelPosition.Left] = PanelDefinitions.AI_ASSISTANT,
        [PanelPosition.Right] = PanelDefinitions.PORTFOLIO,
        [PanelPosition.Center] = PanelDefinitions.CHART
    };
    public Dictionary<PanelPosition, List<(string id, SKRect rect)>> SideTabRects => _sideTabRects;
    internal readonly Dictionary<PanelPosition, List<(string id, SKRect rect)>> _sideTabRects = new()
    {
        [PanelPosition.Left] = new(),
        [PanelPosition.Right] = new(),
        [PanelPosition.Center] = new()
    };
    public Dictionary<PanelPosition, SKRect> SideTabBarRects => _sideTabBarRects;
    private readonly Dictionary<PanelPosition, SKRect> _sideTabBarRects = new();

    public IReadOnlyCollection<DockablePanel> Panels => _panels.Values;
    public bool IsMouseOverPanel(float x, float y) => _panels.Values.Any(p => p.Bounds.Contains(x, y));

    public DockingManager(IEventBus eventBus)
    {
        _eventBus = eventBus;
        foreach (var config in PanelDefinitions.Panels.Values)
        {
            CreatePanel(config.Id);
        }
    }

    private void CreatePanel(string panelId)
    {
        if (!PanelDefinitions.Panels.TryGetValue(panelId, out var config))
            return;

        var panel = new DockablePanel(config);
        if (config.StartClosed)
            panel.IsClosed = true;
        _panels[panelId] = panel;
    }

    public void UpdateLayout(int screenWidth, int screenHeight, float headerHeight)
    {
        _lastScreenWidth = screenWidth;
        _lastScreenHeight = screenHeight;
        _lastHeaderHeight = headerHeight;

        float statusBarHeight = StatusBarRenderer.Height;
        float availableBottom = screenHeight - statusBarHeight - PanelGap;

        var ctx = new DockLayoutContext
        {
            Panels = _panels,
            ActiveTabIds = _activeTabIds,
            ActiveBottomTabId = _activeBottomTabId,
            ScreenWidth = screenWidth,
            ScreenHeight = screenHeight,
            HeaderHeight = headerHeight,
            SideTabBarRects = _sideTabBarRects,
            CurrentLeftX = PanelGap,
            CurrentRightX = screenWidth - PanelGap,
            CurrentBottomY = availableBottom,
            TopEdgeY = headerHeight + PanelGap,
            AvailableBottom = availableBottom
        };

        // Execute layout strategies in order: Side → Bottom → Center
        foreach (var strategy in _layoutStrategies)
            strategy.CalculateLayout(ctx);

        // Sync mutable state back from context
        _activeBottomTabId = ctx.ActiveBottomTabId;
        _bottomTabBarRect = ctx.BottomTabBarRect;

        // Calculate individual tab rectangles
        var bottomTabs = _panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();
        CalculateTabRects(bottomTabs, _bottomTabBarRect, _bottomTabRects);

        foreach (var pos in new[] { PanelPosition.Left, PanelPosition.Right, PanelPosition.Center })
        {
            var sidePanels = _panels.Values
                .Where(p => p.Position == pos && !p.IsFloating && !p.IsClosed)
                .OrderBy(p => p.DockOrder)
                .ToList();
            if (_sideTabBarRects.TryGetValue(pos, out var barRect) && sidePanels.Count > 1)
                CalculateTabRects(sidePanels, barRect, _sideTabRects[pos]);
            else
                _sideTabRects[pos].Clear();
        }

        // Update handle positions for visible panels
        foreach (var panel in _panels.Values.Where(p => !p.IsClosed))
        {
            if (panel.Position == PanelPosition.Bottom && !panel.IsFloating && panel.Config.Id != _activeBottomTabId)
                continue;
            if ((panel.Position == PanelPosition.Left || panel.Position == PanelPosition.Right || panel.Position == PanelPosition.Center)
                && !panel.IsFloating
                && panel.Config.Id != _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id))
                continue;
            UpdatePanelHandles(panel);
        }
    }

    private static void CalculateTabRects(List<DockablePanel> tabs, SKRect barRect, List<(string id, SKRect rect)> output)
    {
        output.Clear();
        if (tabs.Count == 0 || barRect.IsEmpty) return;

        float x = barRect.Left + TabPaddingX;
        float tabY = barRect.Top + TabInsetY;
        float tabH = barRect.Height - TabInsetY * 2;

        foreach (var tab in tabs)
        {
            float textW = TextMeasureCache.Instance.MeasureText(tab.Config.DisplayName, _tabFont);
            float tabW = TabPaddingX + TabIconWidth + textW + TabRightPad;

            if (x + tabW > barRect.Right - TabPaddingX)
                tabW = barRect.Right - TabPaddingX - x;
            if (tabW < 20) break;

            output.Add((tab.Config.Id, new SKRect(x, tabY, x + tabW, tabY + tabH)));
            x += tabW + TabSpacing;
        }
    }

    private void UpdatePanelHandles(DockablePanel panel)
    {
        panel.DragHandleBounds = new SKRect(
            panel.Bounds.Left + HandlePadding,
            panel.Bounds.Top + HandlePadding,
            panel.Bounds.Left + HandlePadding + HandleSize,
            panel.Bounds.Top + HandlePadding + HandleSize);

        if (panel.Config.CanCollapse)
        {
            panel.CollapseHandleBounds = new SKRect(
                panel.Bounds.Right - HandlePadding - HandleSize,
                panel.Bounds.Top + HandlePadding,
                panel.Bounds.Right - HandlePadding,
                panel.Bounds.Top + HandlePadding + HandleSize);
        }

        if (panel.Config.CanClose)
        {
            panel.CloseHandleBounds = new SKRect(
                panel.Bounds.Right - HandlePadding - HandleSize * 2 - 4,
                panel.Bounds.Top + HandlePadding,
                panel.Bounds.Right - HandlePadding - HandleSize - 4,
                panel.Bounds.Top + HandlePadding + HandleSize);
        }

        float topPadding = 40f;
        panel.ContentBounds = new SKRect(
            panel.Bounds.Left + 8,
            panel.Bounds.Top + topPadding,
            panel.Bounds.Right - 8,
            panel.Bounds.Bottom - 8);
    }

    public void OnMouseDown(float x, float y)
    {
        _dragHandler.SetMouseDownPosition(new SKPoint(x, y));

        // Set active panel on click
        _activePanel = null;
        foreach (var ap in _panels.Values.OrderByDescending(ap => ap.IsFloating))
        {
            if (ap.IsClosed) continue;
            if (ap.Position == PanelPosition.Bottom && !ap.IsFloating && ap.Config.Id != _activeBottomTabId) continue;
            if (ap.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center && !ap.IsFloating
                && ap.Config.Id != _activeTabIds.GetValueOrDefault(ap.Position, ap.Config.Id)) continue;
            if (ap.Bounds.Contains(x, y)) { _activePanel = ap; break; }
        }

        // Bottom tab bar
        if (_bottomTabBarRect.Contains(x, y))
        {
            foreach (var (id, rect) in _bottomTabRects)
            {
                if (rect.Contains(x, y))
                {
                    _activeBottomTabId = id;
                    var tabPanel = GetPanel(id);
                    if (tabPanel != null && tabPanel.Config.CanFloat)
                        _dragHandler.PrepareDrag(tabPanel, new SKPoint(tabPanel.Width / 2, TabBarHeight / 2));
                    return;
                }
            }
            return;
        }

        // Side tab bar clicks
        foreach (var kvp in _sideTabBarRects)
        {
            if (kvp.Value.Contains(x, y))
            {
                foreach (var (id, rect) in _sideTabRects[kvp.Key])
                {
                    if (rect.Contains(x, y))
                    {
                        _activeTabIds[kvp.Key] = id;
                        var tabPanel = GetPanel(id);
                        if (tabPanel != null && tabPanel.Config.CanFloat)
                            _dragHandler.PrepareDrag(tabPanel, new SKPoint(tabPanel.Width / 2, TabBarHeight / 2));
                        return;
                    }
                }
                return;
            }
        }

        // Resize edges — delegate to handler (picks closest edge when zones overlap)
        if (_resizeHandler.TryStartResize(x, y, _panels.Values, _activeBottomTabId))
            return;

        // Panel handles
        foreach (var panel in _panels.Values.OrderByDescending(p => p.IsFloating))
        {
            if (panel.IsClosed) continue;
            if (panel.Position == PanelPosition.Bottom && !panel.IsFloating && panel.Config.Id != _activeBottomTabId)
                continue;
            if ((panel.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                && !panel.IsFloating
                && panel.Config.Id != _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id))
                continue;

            if (panel.Config.CanClose && panel.CloseHandleBounds.Contains(x, y))
            {
                panel.IsClosed = true;
                return;
            }

            if (panel.Config.CanCollapse && panel.CollapseHandleBounds.Contains(x, y))
            {
                panel.IsCollapsed = !panel.IsCollapsed;
                return;
            }

            if (panel.Config.CanFloat && panel.DragHandleBounds.Contains(x, y))
            {
                _dragHandler.PrepareDrag(panel, new SKPoint(x - panel.Bounds.Left, y - panel.Bounds.Top));
                return;
            }
        }
    }

    public void OnMouseUp(float x, float y, int screenWidth, int screenHeight)
    {
        var resizedPanel = _resizeHandler.ResizingPanel;
        _resizeHandler.EndResize();
        if (resizedPanel != null)
            _eventBus.Publish(new PanelResizedEvent(resizedPanel.Config.Id, resizedPanel.Width, resizedPanel.Height));

        var draggedPanel = _dragHandler.DraggingPanel;
        _dragHandler.CompleteDrag(ref _activeBottomTabId, _activeTabIds, GetNextDockOrder);
        if (draggedPanel != null)
            _eventBus.Publish(new DockLayoutChangedEvent(draggedPanel.Config.Id, draggedPanel.Position.ToString()));
    }

    public void OnMouseMove(float x, float y, int screenWidth, int screenHeight, float headerHeight)
    {
        // Handle resize
        if (_resizeHandler.IsResizing)
        {
            _resizeHandler.HandleResizeMove(x, y, screenWidth, screenHeight);
            return;
        }

        // Update hovered panel and handle
        if (!_dragHandler.IsDragging)
        {
            _hoveredPanel = null;
            _hoveredHandle = null;

            foreach (var panel in _panels.Values.OrderByDescending(p => p.IsFloating))
            {
                if (panel.IsClosed) continue;
                if (panel.Position == PanelPosition.Bottom && !panel.IsFloating && panel.Config.Id != _activeBottomTabId)
                    continue;
                if ((panel.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                    && !panel.IsFloating
                    && panel.Config.Id != _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id))
                    continue;
                if (panel.Bounds.Contains(x, y))
                {
                    _hoveredPanel = panel;

                    if (panel.DragHandleBounds.Contains(x, y))
                        _hoveredHandle = $"{panel.Config.Id}_drag";
                    else if (panel.CollapseHandleBounds.Contains(x, y))
                        _hoveredHandle = $"{panel.Config.Id}_collapse";
                    else if (panel.CloseHandleBounds.Contains(x, y))
                        _hoveredHandle = $"{panel.Config.Id}_close";

                    break;
                }
            }
        }

        // Delegate drag movement (threshold check + position update + dock zone calc)
        _dragHandler.HandleDragMove(x, y, screenWidth, screenHeight, headerHeight,
            _panels, ref _activeBottomTabId, _activeTabIds, GetChartArea);
    }

    private int GetNextDockOrder(PanelPosition position)
    {
        var panelsInPosition = _panels.Values.Where(p => p.Position == position && !p.IsFloating);
        return panelsInPosition.Any() ? panelsInPosition.Max(p => p.DockOrder) + 1 : 0;
    }

    public SKRect GetChartArea(int screenWidth, int screenHeight, float headerHeight)
    {
        float statusBarHeight = StatusBarRenderer.Height;
        float availableBottom = screenHeight - statusBarHeight;

        float leftMargin = 0;
        float rightMargin = 0;

        foreach (var panel in _panels.Values.Where(p => !p.IsFloating && !p.IsClosed))
        {
            float width = panel.IsCollapsed ? CollapsedWidth : panel.Width;

            if (panel.Position == PanelPosition.Left) leftMargin += width;
            if (panel.Position == PanelPosition.Right) rightMargin += width;
        }

        var bottomTabs = _panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .ToList();
        float bottomMargin = 0;
        if (bottomTabs.Count > 0)
        {
            var activeTab = bottomTabs.FirstOrDefault(t => t.Config.Id == _activeBottomTabId) ?? bottomTabs[0];
            float tabContentH = activeTab.IsCollapsed ? CollapsedWidth : activeTab.Height;
            bottomMargin = tabContentH + TabBarHeight;
        }

        float chartRight = Math.Max(screenWidth - rightMargin, leftMargin + 200);
        float chartBottom = Math.Max(availableBottom - bottomMargin, headerHeight + 120);
        return new SKRect(leftMargin, headerHeight, chartRight, chartBottom);
    }

    public DockablePanel? GetPanel(string panelId) => _panels.GetValueOrDefault(panelId);

    public void TogglePanel(string panelId)
    {
        var panel = GetPanel(panelId);
        if (panel != null)
        {
            panel.IsClosed = !panel.IsClosed;
            if (!panel.IsClosed)
            {
                panel.IsCollapsed = false;
                if (panel.Position == PanelPosition.Bottom)
                    _activeBottomTabId = panelId;
                else if (panel.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                    _activeTabIds[panel.Position] = panelId;
            }
        }
    }

    public bool IsBottomTabActive(DockablePanel panel)
    {
        if (panel.Position == PanelPosition.Bottom)
            return panel.Config.Id == _activeBottomTabId;
        if (panel.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
            return panel.Config.Id == _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id);
        return true;
    }

    public void UpdateChartTitle(string symbol, string interval, float price)
    {
        var chart = GetPanel(PanelDefinitions.CHART);
        if (chart != null)
            chart.DynamicTitle = $"{symbol} \u2022 {interval} \u2022 {price:F2}";
    }

    public List<PanelState> ExportLayout()
    {
        var states = new List<PanelState>();
        foreach (var panel in _panels.Values)
        {
            states.Add(new PanelState
            {
                Id = panel.Config.Id,
                Position = panel.Position.ToString(),
                Width = panel.Width,
                Height = panel.Height,
                IsClosed = panel.IsClosed,
                IsCollapsed = panel.IsCollapsed,
                IsFloating = panel.IsFloating,
                DockOrder = panel.DockOrder
            });
        }
        return states;
    }

    public void ImportLayout(List<PanelState> states)
    {
        foreach (var state in states)
        {
            if (!_panels.TryGetValue(state.Id, out var panel)) continue;
            if (Enum.TryParse<PanelPosition>(state.Position, out var pos))
                panel.Position = pos;
            panel.Width = state.Width;
            panel.Height = state.Height;
            panel.IsClosed = state.IsClosed;
            panel.IsCollapsed = state.IsCollapsed;
            panel.IsFloating = state.IsFloating;
            panel.DockOrder = state.DockOrder;
        }
    }

    public void ImportActiveTabs(string bottomTab, string leftTab, string rightTab, string centerTab = "")
    {
        if (!string.IsNullOrEmpty(bottomTab)) _activeBottomTabId = bottomTab;
        if (!string.IsNullOrEmpty(leftTab)) _activeTabIds[PanelPosition.Left] = leftTab;
        if (!string.IsNullOrEmpty(rightTab)) _activeTabIds[PanelPosition.Right] = rightTab;
        if (!string.IsNullOrEmpty(centerTab)) _activeTabIds[PanelPosition.Center] = centerTab;
    }

    public (string bottom, string left, string right, string center) ExportActiveTabs()
    {
        return (
            _activeBottomTabId,
            _activeTabIds.GetValueOrDefault(PanelPosition.Left, ""),
            _activeTabIds.GetValueOrDefault(PanelPosition.Right, ""),
            _activeTabIds.GetValueOrDefault(PanelPosition.Center, PanelDefinitions.CHART));
    }

    public string GetActiveCenterTabId() => _activeTabIds.GetValueOrDefault(PanelPosition.Center, PanelDefinitions.CHART);
}
