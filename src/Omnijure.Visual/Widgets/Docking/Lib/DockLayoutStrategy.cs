using Omnijure.Visual.Widgets.Docking.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Widgets.Docking.Lib;

/// <summary>
/// Context object passed through layout strategies, tracking cumulative edge positions.
/// Mutable by design — each strategy updates the edges it consumes.
/// </summary>
internal class DockLayoutContext
{
    public required Dictionary<string, DockablePanel> Panels { get; init; }
    public required Dictionary<PanelPosition, string> ActiveTabIds { get; init; }
    public required string ActiveBottomTabId { get; set; }
    public required int ScreenWidth { get; init; }
    public required int ScreenHeight { get; init; }
    public required float HeaderHeight { get; init; }
    public required Dictionary<PanelPosition, SKRect> SideTabBarRects { get; init; }

    // Cumulative edges — updated by strategies
    public float CurrentLeftX { get; set; }
    public float CurrentRightX { get; set; }
    public float CurrentBottomY { get; set; }
    public float TopEdgeY { get; set; }
    public float AvailableBottom { get; set; }

    // Output
    public SKRect BottomTabBarRect { get; set; }
}

/// <summary>
/// Calculates layout bounds for a specific dock region.
/// </summary>
internal interface IDockLayoutStrategy
{
    void CalculateLayout(DockLayoutContext ctx);
}

/// <summary>
/// Lays out Left and Right side panels (horizontal resize).
/// </summary>
internal class SideDockStrategy : IDockLayoutStrategy
{
    private const float CollapsedWidth = DockingManager.CollapsedWidth;
    private const float MinCenterWidth = DockingManager.MinCenterWidth;
    private const float PanelGap = DockingManager.PanelGap;
    private const float TabBarHeight = DockingManager.TabBarHeight;

    public void CalculateLayout(DockLayoutContext ctx)
    {
        LayoutSide(ctx, PanelPosition.Left);
        LayoutSide(ctx, PanelPosition.Right);
    }

    private static void LayoutSide(DockLayoutContext ctx, PanelPosition side)
    {
        var panels = ctx.Panels.Values
            .Where(p => p.Position == side && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (panels.Count == 0)
        {
            ctx.SideTabBarRects.Remove(side);
            return;
        }

        if (!panels.Any(p => p.Config.Id == ctx.ActiveTabIds.GetValueOrDefault(side)))
            ctx.ActiveTabIds[side] = panels[0].Config.Id;

        var active = panels.First(p => p.Config.Id == ctx.ActiveTabIds[side]);
        float w = active.IsCollapsed ? CollapsedWidth : active.Width;

        if (side == PanelPosition.Left)
        {
            float maxW = ctx.ScreenWidth - MinCenterWidth - PanelGap * 2;
            w = Math.Min(w, Math.Max(CollapsedWidth, maxW));

            if (panels.Count > 1)
            {
                ctx.SideTabBarRects[side] = new SKRect(
                    ctx.CurrentLeftX, ctx.AvailableBottom - TabBarHeight,
                    ctx.CurrentLeftX + w - PanelGap, ctx.AvailableBottom);
                active.Bounds = new SKRect(
                    ctx.CurrentLeftX, ctx.TopEdgeY,
                    ctx.CurrentLeftX + w - PanelGap, ctx.AvailableBottom - TabBarHeight);
            }
            else
            {
                ctx.SideTabBarRects.Remove(side);
                active.Bounds = new SKRect(
                    ctx.CurrentLeftX, ctx.TopEdgeY,
                    ctx.CurrentLeftX + w - PanelGap, ctx.AvailableBottom);
            }
            ctx.CurrentLeftX += w;
        }
        else // Right
        {
            float maxW = ctx.CurrentRightX - ctx.CurrentLeftX - MinCenterWidth;
            w = Math.Min(w, Math.Max(CollapsedWidth, maxW));

            if (panels.Count > 1)
            {
                ctx.SideTabBarRects[side] = new SKRect(
                    ctx.CurrentRightX - w + PanelGap, ctx.AvailableBottom - TabBarHeight,
                    ctx.CurrentRightX, ctx.AvailableBottom);
                active.Bounds = new SKRect(
                    ctx.CurrentRightX - w + PanelGap, ctx.TopEdgeY,
                    ctx.CurrentRightX, ctx.AvailableBottom - TabBarHeight);
            }
            else
            {
                ctx.SideTabBarRects.Remove(side);
                active.Bounds = new SKRect(
                    ctx.CurrentRightX - w + PanelGap, ctx.TopEdgeY,
                    ctx.CurrentRightX, ctx.AvailableBottom);
            }
            ctx.CurrentRightX -= w;
        }
    }
}

/// <summary>
/// Lays out Bottom panel group (vertical resize, tabbed).
/// </summary>
internal class BottomDockStrategy : IDockLayoutStrategy
{
    private const float CollapsedWidth = DockingManager.CollapsedWidth;
    private const float TabBarHeight = DockingManager.TabBarHeight;

    public void CalculateLayout(DockLayoutContext ctx)
    {
        var bottomTabs = ctx.Panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (bottomTabs.Count == 0)
        {
            ctx.BottomTabBarRect = SKRect.Empty;
            return;
        }

        if (!bottomTabs.Any(t => t.Config.Id == ctx.ActiveBottomTabId))
            ctx.ActiveBottomTabId = bottomTabs[0].Config.Id;

        var activeTab = bottomTabs.First(t => t.Config.Id == ctx.ActiveBottomTabId);
        float contentHeight = activeTab.IsCollapsed ? CollapsedWidth : activeTab.Height;
        float totalHeight = contentHeight + TabBarHeight;

        activeTab.Bounds = new SKRect(
            ctx.CurrentLeftX, ctx.CurrentBottomY - totalHeight,
            ctx.CurrentRightX, ctx.CurrentBottomY - TabBarHeight);

        ctx.BottomTabBarRect = new SKRect(
            ctx.CurrentLeftX, ctx.CurrentBottomY - TabBarHeight,
            ctx.CurrentRightX, ctx.CurrentBottomY);

        ctx.CurrentBottomY -= totalHeight;
    }
}

/// <summary>
/// Lays out Center panels (fills remaining space).
/// </summary>
internal class CenterDockStrategy : IDockLayoutStrategy
{
    private const float TabBarHeight = DockingManager.TabBarHeight;

    public void CalculateLayout(DockLayoutContext ctx)
    {
        var centerPanels = ctx.Panels.Values
            .Where(p => p.Position == PanelPosition.Center && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (centerPanels.Count == 0)
        {
            ctx.SideTabBarRects.Remove(PanelPosition.Center);
            return;
        }

        if (!centerPanels.Any(p => p.Config.Id == ctx.ActiveTabIds.GetValueOrDefault(PanelPosition.Center)))
            ctx.ActiveTabIds[PanelPosition.Center] = centerPanels[0].Config.Id;

        var activeCenter = centerPanels.First(p => p.Config.Id == ctx.ActiveTabIds[PanelPosition.Center]);

        if (centerPanels.Count > 1)
        {
            ctx.SideTabBarRects[PanelPosition.Center] = new SKRect(
                ctx.CurrentLeftX, ctx.TopEdgeY,
                ctx.CurrentRightX, ctx.TopEdgeY + TabBarHeight);
            activeCenter.Bounds = new SKRect(
                ctx.CurrentLeftX, ctx.TopEdgeY + TabBarHeight,
                ctx.CurrentRightX, ctx.CurrentBottomY);
        }
        else
        {
            ctx.SideTabBarRects.Remove(PanelPosition.Center);
            activeCenter.Bounds = new SKRect(
                ctx.CurrentLeftX, ctx.TopEdgeY,
                ctx.CurrentRightX, ctx.CurrentBottomY);
        }
    }
}
