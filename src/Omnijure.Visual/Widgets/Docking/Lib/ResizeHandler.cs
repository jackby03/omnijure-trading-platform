using Omnijure.Visual.Widgets.Docking.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Widgets.Docking.Lib;

internal enum ResizeEdge { None, Right, Left, Top, Bottom }

/// <summary>
/// Handles panel resize hit-testing, state tracking, and size delta application.
/// </summary>
internal class ResizeHandler
{
    private const float ResizeEdgeWidth = 6f;

    private DockablePanel? _resizingPanel;
    private ResizeEdge _resizeEdge;
    private float _resizeStartMousePos;
    private float _resizeStartSize;

    public DockablePanel? ResizingPanel => _resizingPanel;
    public bool IsResizing => _resizingPanel != null;

    public bool TryStartResize(
        float x, float y,
        IEnumerable<DockablePanel> panels,
        string activeBottomTabId)
    {
        DockablePanel? bestPanel = null;
        ResizeEdge bestEdge = ResizeEdge.None;
        float bestDist = float.MaxValue;

        foreach (var panel in panels.Where(p =>
            !p.IsClosed && !p.IsFloating && !p.IsCollapsed && p.Position != PanelPosition.Center))
        {
            if (panel.Position == PanelPosition.Bottom && panel.Config.Id != activeBottomTabId)
                continue;
            var edge = GetResizeEdge(panel, x, y);
            if (edge != ResizeEdge.None)
            {
                float dist = DistanceToEdge(panel, edge, x, y);
                if (dist < bestDist)
                {
                    bestPanel = panel;
                    bestEdge = edge;
                    bestDist = dist;
                }
            }
        }

        if (bestPanel != null)
        {
            _resizingPanel = bestPanel;
            _resizeEdge = bestEdge;
            _resizeStartMousePos = (bestEdge is ResizeEdge.Right or ResizeEdge.Left) ? x : y;
            _resizeStartSize = (bestEdge is ResizeEdge.Right or ResizeEdge.Left) ? bestPanel.Width : bestPanel.Height;
            return true;
        }

        return false;
    }

    public void HandleResizeMove(float x, float y, int screenWidth, int screenHeight)
    {
        if (_resizingPanel == null) return;

        if (_resizeEdge is ResizeEdge.Right or ResizeEdge.Left)
        {
            float delta = x - _resizeStartMousePos;
            if (_resizingPanel.Position == PanelPosition.Left)
                _resizingPanel.Width = Math.Clamp(_resizeStartSize + delta, 100, screenWidth * 0.5f);
            else if (_resizingPanel.Position == PanelPosition.Right)
                _resizingPanel.Width = Math.Clamp(_resizeStartSize - delta, 100, screenWidth * 0.5f);
        }
        else
        {
            float delta = y - _resizeStartMousePos;
            if (_resizingPanel.Position == PanelPosition.Bottom)
                _resizingPanel.Height = Math.Clamp(_resizeStartSize - delta, 80, screenHeight * 0.5f);
            else if (_resizingPanel.Position == PanelPosition.Top)
                _resizingPanel.Height = Math.Clamp(_resizeStartSize + delta, 80, screenHeight * 0.5f);
        }
    }

    public void EndResize()
    {
        _resizingPanel = null;
        _resizeEdge = ResizeEdge.None;
    }

    private static ResizeEdge GetResizeEdge(DockablePanel panel, float x, float y)
    {
        var b = panel.Bounds;

        switch (panel.Position)
        {
            case PanelPosition.Left:
                if (x >= b.Right - ResizeEdgeWidth && x <= b.Right + ResizeEdgeWidth && y >= b.Top && y <= b.Bottom)
                    return ResizeEdge.Right;
                break;
            case PanelPosition.Right:
                if (x >= b.Left - ResizeEdgeWidth && x <= b.Left + ResizeEdgeWidth && y >= b.Top && y <= b.Bottom)
                    return ResizeEdge.Left;
                break;
            case PanelPosition.Bottom:
                if (y >= b.Top - ResizeEdgeWidth && y <= b.Top + ResizeEdgeWidth && x >= b.Left && x <= b.Right)
                    return ResizeEdge.Top;
                break;
            case PanelPosition.Top:
                if (y >= b.Bottom - ResizeEdgeWidth && y <= b.Bottom + ResizeEdgeWidth && x >= b.Left && x <= b.Right)
                    return ResizeEdge.Bottom;
                break;
        }
        return ResizeEdge.None;
    }

    private static float DistanceToEdge(DockablePanel panel, ResizeEdge edge, float x, float y)
    {
        return edge switch
        {
            ResizeEdge.Right => MathF.Abs(x - panel.Bounds.Right),
            ResizeEdge.Left => MathF.Abs(x - panel.Bounds.Left),
            ResizeEdge.Top => MathF.Abs(y - panel.Bounds.Top),
            ResizeEdge.Bottom => MathF.Abs(y - panel.Bounds.Bottom),
            _ => float.MaxValue
        };
    }
}
