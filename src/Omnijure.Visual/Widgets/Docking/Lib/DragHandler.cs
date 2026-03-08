using Omnijure.Visual.Widgets.Docking.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Widgets.Docking.Lib;

/// <summary>
/// Handles the full drag-to-dock lifecycle: threshold detection, floating conversion,
/// position tracking, dock zone calculation, and dock completion/cancellation.
/// </summary>
internal class DragHandler
{
    private const float DragThreshold = 5f;
    private const float GuideButtonSize = 36f;
    private const float GuideHitRadius = 22f;
    private const float EdgeGuideMargin = 20f;
    private const float CollapsedWidth = DockingManager.CollapsedWidth;
    private const float TabBarHeight = DockingManager.TabBarHeight;

    private DockablePanel? _draggingPanel;
    private DockablePanel? _potentialDragPanel;
    private SKPoint _mouseDownPosition;
    private SKPoint _dragOffset;
    private DockZone? _currentDockZone;

    // Original state for restore on cancel
    private PanelPosition _originalPosition;
    private bool _originalIsFloating;
    private SKRect _originalBounds;

    public DockablePanel? DraggingPanel => _draggingPanel;
    public DockablePanel? PotentialDragPanel => _potentialDragPanel;
    public bool IsDragging => _draggingPanel != null;
    public bool HasPendingDrag => _potentialDragPanel != null;
    public DockZone? CurrentDockZone => _currentDockZone;

    public void SetMouseDownPosition(SKPoint pos)
    {
        _mouseDownPosition = pos;
    }

    public void PrepareDrag(DockablePanel panel, SKPoint dragOffset)
    {
        _potentialDragPanel = panel;
        _dragOffset = dragOffset;
    }

    public void HandleDragMove(
        float x, float y,
        int screenWidth, int screenHeight, float headerHeight,
        Dictionary<string, DockablePanel> panels,
        ref string activeBottomTabId,
        Dictionary<PanelPosition, string> activeTabIds,
        Func<int, int, float, SKRect> getChartArea)
    {
        // Drag threshold check
        if (_potentialDragPanel != null && _draggingPanel == null)
        {
            float distance = (float)Math.Sqrt(
                Math.Pow(x - _mouseDownPosition.X, 2) +
                Math.Pow(y - _mouseDownPosition.Y, 2));

            if (distance > DragThreshold)
            {
                _draggingPanel = _potentialDragPanel;
                _potentialDragPanel = null;

                _originalPosition = _draggingPanel.Position;
                _originalIsFloating = _draggingPanel.IsFloating;
                _originalBounds = _draggingPanel.Bounds;

                if (!_draggingPanel.IsFloating)
                {
                    _draggingPanel.IsFloating = true;

                    if (_draggingPanel.Position == PanelPosition.Bottom)
                    {
                        _draggingPanel.FloatingWidth = Math.Min(_draggingPanel.Width, 400);
                        _draggingPanel.FloatingHeight = Math.Min(_draggingPanel.Height, 300);

                        var remainingTabs = panels.Values
                            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed && p != _draggingPanel)
                            .OrderBy(p => p.DockOrder).ToList();
                        if (remainingTabs.Count > 0)
                            activeBottomTabId = remainingTabs[0].Config.Id;
                    }
                    else if (_draggingPanel.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                    {
                        _draggingPanel.FloatingWidth = Math.Min(_draggingPanel.Bounds.Width, 600);
                        _draggingPanel.FloatingHeight = Math.Min(_draggingPanel.Bounds.Height, 400);

                        var remainingSide = panels.Values
                            .Where(p => p.Position == _draggingPanel.Position && !p.IsFloating && !p.IsClosed && p != _draggingPanel)
                            .OrderBy(p => p.DockOrder).ToList();
                        if (remainingSide.Count > 0)
                            activeTabIds[_draggingPanel.Position] = remainingSide[0].Config.Id;
                    }
                    else
                    {
                        _draggingPanel.FloatingWidth = _draggingPanel.Bounds.Width;
                        _draggingPanel.FloatingHeight = _draggingPanel.Bounds.Height;
                    }
                }
            }
        }

        // Update drag position
        if (_draggingPanel != null)
        {
            float newX = x - _dragOffset.X;
            float newY = y - _dragOffset.Y;
            float width = _draggingPanel.FloatingWidth > 0 ? _draggingPanel.FloatingWidth : _draggingPanel.Bounds.Width;
            float height = _draggingPanel.FloatingHeight > 0 ? _draggingPanel.FloatingHeight : _draggingPanel.Bounds.Height;

            _draggingPanel.Bounds = new SKRect(newX, newY, newX + width, newY + height);
            _currentDockZone = CalculateDockZone(x, y, screenWidth, screenHeight, headerHeight, getChartArea);
        }
    }

    public void CompleteDrag(
        ref string activeBottomTabId,
        Dictionary<PanelPosition, string> activeTabIds,
        Func<PanelPosition, int> getNextDockOrder)
    {
        if (_draggingPanel != null)
        {
            if (_currentDockZone != null)
            {
                _draggingPanel.Position = _currentDockZone.Position;
                _draggingPanel.IsFloating = false;
                _draggingPanel.DockOrder = getNextDockOrder(_currentDockZone.Position);

                if (_currentDockZone.Position == PanelPosition.Bottom)
                    activeBottomTabId = _draggingPanel.Config.Id;
                else if (_currentDockZone.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                    activeTabIds[_currentDockZone.Position] = _draggingPanel.Config.Id;
            }
            else
            {
                _draggingPanel.Position = _originalPosition;
                _draggingPanel.IsFloating = _originalIsFloating;
                _draggingPanel.Bounds = _originalBounds;

                if (_originalPosition == PanelPosition.Bottom && !_originalIsFloating)
                    activeBottomTabId = _draggingPanel.Config.Id;
                else if (_originalPosition is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center && !_originalIsFloating)
                    activeTabIds[_originalPosition] = _draggingPanel.Config.Id;
            }
        }

        _draggingPanel = null;
        _potentialDragPanel = null;
        _currentDockZone = null;
    }

    private DockZone? CalculateDockZone(
        float x, float y,
        int screenWidth, int screenHeight, float headerHeight,
        Func<int, int, float, SKRect> getChartArea)
    {
        float statusBarH = StatusBarRenderer.Height;
        float availH = screenHeight - statusBarH;

        var chartArea = getChartArea(screenWidth, screenHeight, headerHeight);
        float cx = chartArea.MidX;
        float cy = chartArea.MidY;

        if (DistanceTo(x, y, cx, cy - 50) < GuideHitRadius)
            return new DockZone(PanelPosition.Top,
                new SKRect(chartArea.Left, chartArea.Top, chartArea.Right, chartArea.MidY));

        if (DistanceTo(x, y, cx, cy + 50) < GuideHitRadius)
            return new DockZone(PanelPosition.Bottom,
                new SKRect(chartArea.Left, chartArea.MidY, chartArea.Right, chartArea.Bottom));

        if (DistanceTo(x, y, cx - 50, cy) < GuideHitRadius)
            return new DockZone(PanelPosition.Left,
                new SKRect(chartArea.Left, chartArea.Top, chartArea.MidX, chartArea.Bottom));

        if (DistanceTo(x, y, cx + 50, cy) < GuideHitRadius)
            return new DockZone(PanelPosition.Right,
                new SKRect(chartArea.MidX, chartArea.Top, chartArea.Right, chartArea.Bottom));

        if (DistanceTo(x, y, cx, cy) < GuideHitRadius)
            return new DockZone(PanelPosition.Center, chartArea);

        float edgeCy = (headerHeight + availH) / 2;

        if (DistanceTo(x, y, EdgeGuideMargin + GuideButtonSize / 2, edgeCy) < GuideHitRadius)
            return new DockZone(PanelPosition.Left,
                new SKRect(0, headerHeight, screenWidth * 0.25f, availH));

        if (DistanceTo(x, y, screenWidth - EdgeGuideMargin - GuideButtonSize / 2, edgeCy) < GuideHitRadius)
            return new DockZone(PanelPosition.Right,
                new SKRect(screenWidth * 0.75f, headerHeight, screenWidth, availH));

        float edgeCx = screenWidth / 2f;
        if (DistanceTo(x, y, edgeCx, availH - EdgeGuideMargin - GuideButtonSize / 2) < GuideHitRadius)
            return new DockZone(PanelPosition.Bottom,
                new SKRect(0, availH * 0.7f, screenWidth, availH));

        return null;
    }

    private static float DistanceTo(float x1, float y1, float x2, float y2)
    {
        float dx = x1 - x2;
        float dy = y1 - y2;
        return MathF.Sqrt(dx * dx + dy * dy);
    }
}
