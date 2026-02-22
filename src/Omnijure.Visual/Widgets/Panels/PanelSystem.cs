using Omnijure.Core.Features.Settings.Api;
using Omnijure.Core.Features.Settings.Model;
using SkiaSharp;
using System;
using System.Collections.Generic;
using System.Linq;

namespace Omnijure.Visual.Rendering;

/// <summary>
/// Sistema de paneles moderno sin barras de t�tulo.
/// Interacci�n mediante handles minimalistas en las esquinas.
/// Estilo inspirado en herramientas profesionales como OBS/Figma.
/// </summary>
public class PanelSystem
{
    private readonly Dictionary<string, DockablePanel> _panels = new();
    public DockablePanel? DraggingPanel => _draggingPanel;
    private DockablePanel? _draggingPanel;
    private DockablePanel? _potentialDragPanel;
    private SKPoint _mouseDownPosition;
    private DockablePanel? _hoveredPanel;
    public string? HoveredHandle => _hoveredHandle;
    private string? _hoveredHandle;
    public DockablePanel? ActivePanel => _activePanel;
    private DockablePanel? _activePanel;
    private SKPoint _dragOffset;
    public DockZone? CurrentDockZone => _currentDockZone;
    private DockZone? _currentDockZone;
    
    // ? Guardar estado original para restaurar si se cancela el drag
    private PanelPosition _originalPosition;
    private bool _originalIsFloating;
    private SKRect _originalBounds;
    
    private const float HandleSize = 24f;
    private const float CollapsedWidth = 32f;
    private const float HandlePadding = 6f;
    private const float DragThreshold = 5f;
    private const float PanelGap = 4f;
    private const float ResizeEdgeWidth = 6f;
    
    // Dock guide constants (VS-style)
    private const float GuideButtonSize = 36f;
    private const float GuideHitRadius = 22f;
    private const float EdgeGuideMargin = 20f;
    
    // Cached layout for dock guides
    public float LastHeaderHeight => _lastHeaderHeight;
    private float _lastHeaderHeight;
    public int LastScreenWidth => _lastScreenWidth;
    private int _lastScreenWidth;
    public int LastScreenHeight => _lastScreenHeight;
    private int _lastScreenHeight;
    
    // Resize state
    public DockablePanel? ResizingPanel => _resizingPanel;
    private DockablePanel? _resizingPanel;
    private ResizeEdge _resizeEdge;
    private float _resizeStartMousePos;
    private float _resizeStartSize;
    
    private enum ResizeEdge { None, Right, Left, Top, Bottom }
    
    // Tab layout constants
    private const float TabBarHeight = 28f;
    private const float TabPaddingX = 6f;
    private const float TabIconWidth = 16f; // icon(12) + gap(4)
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
    
    // Side tab system (Left/Right/Center docking with tabs)
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

    public PanelSystem()
    {
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

    public void Update(int screenWidth, int screenHeight, float headerHeight)
    {
        _lastScreenWidth = screenWidth;
        _lastScreenHeight = screenHeight;
        _lastHeaderHeight = headerHeight;
        
        float statusBarHeight = StatusBarRenderer.Height;
        float availableBottom = screenHeight - statusBarHeight - PanelGap;
        
        float currentLeftX = PanelGap;
        float currentRightX = screenWidth - PanelGap;
        float currentBottomY = availableBottom;
        float topEdgeY = headerHeight + PanelGap;

        // PASO 1: Left panels (tabbed when multiple)
        var leftPanels = _panels.Values.Where(p => p.Position == PanelPosition.Left && !p.IsFloating && !p.IsClosed).OrderBy(p => p.DockOrder).ToList();
        if (leftPanels.Count > 0)
        {
            if (!leftPanels.Any(p => p.Config.Id == _activeTabIds.GetValueOrDefault(PanelPosition.Left)))
                _activeTabIds[PanelPosition.Left] = leftPanels[0].Config.Id;
            var activeLeft = leftPanels.First(p => p.Config.Id == _activeTabIds[PanelPosition.Left]);
            float lw = activeLeft.IsCollapsed ? CollapsedWidth : activeLeft.Width;
            if (leftPanels.Count > 1)
            {
                _sideTabBarRects[PanelPosition.Left] = new SKRect(currentLeftX, availableBottom - TabBarHeight, currentLeftX + lw - PanelGap, availableBottom);
                activeLeft.Bounds = new SKRect(currentLeftX, topEdgeY, currentLeftX + lw - PanelGap, availableBottom - TabBarHeight);
            }
            else
            {
                _sideTabBarRects.Remove(PanelPosition.Left);
                activeLeft.Bounds = new SKRect(currentLeftX, topEdgeY, currentLeftX + lw - PanelGap, availableBottom);
            }
            currentLeftX += lw;
        }
        else { _sideTabBarRects.Remove(PanelPosition.Left); }

        // PASO 2: Right panels (tabbed when multiple)
        var rightPanels = _panels.Values.Where(p => p.Position == PanelPosition.Right && !p.IsFloating && !p.IsClosed).OrderBy(p => p.DockOrder).ToList();
        if (rightPanels.Count > 0)
        {
            if (!rightPanels.Any(p => p.Config.Id == _activeTabIds.GetValueOrDefault(PanelPosition.Right)))
                _activeTabIds[PanelPosition.Right] = rightPanels[0].Config.Id;
            var activeRight = rightPanels.First(p => p.Config.Id == _activeTabIds[PanelPosition.Right]);
            float rw = activeRight.IsCollapsed ? CollapsedWidth : activeRight.Width;
            if (rightPanels.Count > 1)
            {
                _sideTabBarRects[PanelPosition.Right] = new SKRect(currentRightX - rw + PanelGap, availableBottom - TabBarHeight, currentRightX, availableBottom);
                activeRight.Bounds = new SKRect(currentRightX - rw + PanelGap, topEdgeY, currentRightX, availableBottom - TabBarHeight);
            }
            else
            {
                _sideTabBarRects.Remove(PanelPosition.Right);
                activeRight.Bounds = new SKRect(currentRightX - rw + PanelGap, topEdgeY, currentRightX, availableBottom);
            }
            currentRightX -= rw;
        }
        else { _sideTabBarRects.Remove(PanelPosition.Right); }

        // ???????????????????????????????????????????????????????????
        // PASO 3: Bottom tab group (tabbed, only active panel gets bounds)
        // ???????????????????????????????????????????????????????????
        var bottomTabs = _panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (bottomTabs.Count > 0)
        {
            // Ensure active tab is valid
            if (!bottomTabs.Any(t => t.Config.Id == _activeBottomTabId))
                _activeBottomTabId = bottomTabs[0].Config.Id;
            
            var activeTab = bottomTabs.First(t => t.Config.Id == _activeBottomTabId);
            float contentHeight = activeTab.IsCollapsed ? CollapsedWidth : activeTab.Height;
            float totalHeight = contentHeight + TabBarHeight;
            
            // Active panel bounds (content area above tab bar)
            activeTab.Bounds = new SKRect(
                currentLeftX, currentBottomY - totalHeight,
                currentRightX, currentBottomY - TabBarHeight);
            
            // Tab bar rect (below panel content)
            _bottomTabBarRect = new SKRect(
                currentLeftX, currentBottomY - TabBarHeight,
                currentRightX, currentBottomY);
            
            currentBottomY -= totalHeight;
        }
        else
        {
            _bottomTabBarRect = SKRect.Empty;
        }

        // ???????????????????????????????????????????????????????????
        // PASO 4: Center panel ocupa el espacio restante
        // ???????????????????????????????????????????????????????????
        var centerPanels = _panels.Values
            .Where(p => p.Position == PanelPosition.Center && !p.IsFloating && !p.IsClosed)
            .OrderBy(p => p.DockOrder)
            .ToList();

        if (centerPanels.Count > 0)
        {
            if (!centerPanels.Any(p => p.Config.Id == _activeTabIds.GetValueOrDefault(PanelPosition.Center)))
                _activeTabIds[PanelPosition.Center] = centerPanels[0].Config.Id;
            var activeCenter = centerPanels.First(p => p.Config.Id == _activeTabIds[PanelPosition.Center]);

            if (centerPanels.Count > 1)
            {
                // Tab bar at top of center area
                _sideTabBarRects[PanelPosition.Center] = new SKRect(currentLeftX, topEdgeY, currentRightX, topEdgeY + TabBarHeight);
                activeCenter.Bounds = new SKRect(currentLeftX, topEdgeY + TabBarHeight, currentRightX, currentBottomY);
            }
            else
            {
                _sideTabBarRects.Remove(PanelPosition.Center);
                activeCenter.Bounds = new SKRect(currentLeftX, topEdgeY, currentRightX, currentBottomY);
            }
        }
        else { _sideTabBarRects.Remove(PanelPosition.Center); }

        // PASO 5: Calculate individual tab rectangles for all tab bars
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

        // Update handle positions for visible panels (skip inactive tabs)
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

            // Clamp tab to not exceed bar right edge
            if (x + tabW > barRect.Right - TabPaddingX)
                tabW = barRect.Right - TabPaddingX - x;
            if (tabW < 20) break; // Too small to render

            output.Add((tab.Config.Id, new SKRect(x, tabY, x + tabW, tabY + tabH)));
            x += tabW + TabSpacing;
        }
    }

    private void UpdatePanelHandles(DockablePanel panel)
    {
        // Drag handle (esquina superior izquierda)
        panel.DragHandleBounds = new SKRect(
            panel.Bounds.Left + HandlePadding,
            panel.Bounds.Top + HandlePadding,
            panel.Bounds.Left + HandlePadding + HandleSize,
            panel.Bounds.Top + HandlePadding + HandleSize
        );

        // Collapse handle (esquina superior derecha)
        if (panel.Config.CanCollapse)
        {
            panel.CollapseHandleBounds = new SKRect(
                panel.Bounds.Right - HandlePadding - HandleSize,
                panel.Bounds.Top + HandlePadding,
                panel.Bounds.Right - HandlePadding,
                panel.Bounds.Top + HandlePadding + HandleSize
            );
        }

        // Close handle (al lado del collapse) - SIEMPRE si puede cerrarse
        if (panel.Config.CanClose)
        {
            panel.CloseHandleBounds = new SKRect(
                panel.Bounds.Right - HandlePadding - HandleSize * 2 - 4,
                panel.Bounds.Top + HandlePadding,
                panel.Bounds.Right - HandlePadding - HandleSize - 4,
                panel.Bounds.Top + HandlePadding + HandleSize
            );
        }

        // Content bounds (�rea �til del panel)
        float topPadding = 40f; // Espacio para handles y nombre
        panel.ContentBounds = new SKRect(
            panel.Bounds.Left + 8,
            panel.Bounds.Top + topPadding,
            panel.Bounds.Right - 8,
            panel.Bounds.Bottom - 8
        );
    }



    public void OnMouseDown(float x, float y)
    {
        _mouseDownPosition = new SKPoint(x, y);
        
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
        
        if (_bottomTabBarRect.Contains(x, y))
        {
            foreach (var (id, rect) in _bottomTabRects)
            {
                if (rect.Contains(x, y))
                {
                    _activeBottomTabId = id;
                    // Prepare potential tear-off drag (like VS tab dragging)
                    var tabPanel = GetPanel(id);
                    if (tabPanel != null && tabPanel.Config.CanFloat)
                    {
                        _potentialDragPanel = tabPanel;
                        _dragOffset = new SKPoint(
                            tabPanel.Width / 2,
                            TabBarHeight / 2);
                    }
                    return;
                }
            }
            return;
        }
        
        // Check side tab bar clicks (Left/Right)
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
                        {
                            _potentialDragPanel = tabPanel;
                            _dragOffset = new SKPoint(tabPanel.Width / 2, TabBarHeight / 2);
                        }
                        return;
                    }
                }
                return;
            }
        }
        
        // Check resize edges
        foreach (var panel in _panels.Values.Where(p => !p.IsClosed && !p.IsFloating && !p.IsCollapsed && p.Position != PanelPosition.Center))
        {
            if (panel.Position == PanelPosition.Bottom && panel.Config.Id != _activeBottomTabId)
                continue;
            var edge = GetResizeEdge(panel, x, y);
            if (edge != ResizeEdge.None)
            {
                _resizingPanel = panel;
                _resizeEdge = edge;
                _resizeStartMousePos = (edge == ResizeEdge.Right || edge == ResizeEdge.Left) ? x : y;
                _resizeStartSize = (edge == ResizeEdge.Right || edge == ResizeEdge.Left) ? panel.Width : panel.Height;
                return;
            }
        }
        
        // Check handles
        foreach (var panel in _panels.Values.OrderByDescending(p => p.IsFloating))
        {
            if (panel.IsClosed) continue;
            // Skip inactive tabs (they don't have valid bounds)
            if (panel.Position == PanelPosition.Bottom && !panel.IsFloating && panel.Config.Id != _activeBottomTabId)
                continue;
            if ((panel.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                && !panel.IsFloating
                && panel.Config.Id != _activeTabIds.GetValueOrDefault(panel.Position, panel.Config.Id))
                continue;
            
            // Close handle - CERRAR panel (no flotar)
            if (panel.Config.CanClose && panel.CloseHandleBounds.Contains(x, y))
            {
                panel.IsClosed = true;  // ? Ocultar completamente
                return;
            }

            // Collapse handle
            if (panel.Config.CanCollapse && panel.CollapseHandleBounds.Contains(x, y))
            {
                panel.IsCollapsed = !panel.IsCollapsed;
                return;
            }

            // Drag handle - preparar para arrastrar pero NO mover todav�a
            if (panel.Config.CanFloat && panel.DragHandleBounds.Contains(x, y))
            {
                _potentialDragPanel = panel;
                _dragOffset = new SKPoint(x - panel.Bounds.Left, y - panel.Bounds.Top);
                return;
            }
        }
    }

    public void OnMouseUp(float x, float y, int screenWidth, int screenHeight)
    {
        _resizingPanel = null;
        _resizeEdge = ResizeEdge.None;
        
        if (_draggingPanel != null)
        {
            if (_currentDockZone != null)
            {
                _draggingPanel.Position = _currentDockZone.Position;
                _draggingPanel.IsFloating = false;
                _draggingPanel.DockOrder = GetNextDockOrder(_currentDockZone.Position);
                
                // Auto-activate as the active tab when docking
                if (_currentDockZone.Position == PanelPosition.Bottom)
                    _activeBottomTabId = _draggingPanel.Config.Id;
                else if (_currentDockZone.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                    _activeTabIds[_currentDockZone.Position] = _draggingPanel.Config.Id;
            }
            else
            {
                // No valid zone � restore to original position
                _draggingPanel.Position = _originalPosition;
                _draggingPanel.IsFloating = _originalIsFloating;
                _draggingPanel.Bounds = _originalBounds;
                
                // Re-activate tab if restoring to docked position
                if (_originalPosition == PanelPosition.Bottom && !_originalIsFloating)
                    _activeBottomTabId = _draggingPanel.Config.Id;
                else if (_originalPosition is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center && !_originalIsFloating)
                    _activeTabIds[_originalPosition] = _draggingPanel.Config.Id;
            }
        }

        _draggingPanel = null;
        _potentialDragPanel = null;
        _currentDockZone = null;
    }

    public void OnMouseMove(float x, float y, int screenWidth, int screenHeight, float headerHeight)
    {
        // Handle resize
        if (_resizingPanel != null)
        {
            float delta;
            if (_resizeEdge == ResizeEdge.Right || _resizeEdge == ResizeEdge.Left)
            {
                delta = x - _resizeStartMousePos;
                if (_resizingPanel.Position == PanelPosition.Left)
                    _resizingPanel.Width = Math.Clamp(_resizeStartSize + delta, 100, screenWidth * 0.5f);
                else if (_resizingPanel.Position == PanelPosition.Right)
                    _resizingPanel.Width = Math.Clamp(_resizeStartSize - delta, 100, screenWidth * 0.5f);
            }
            else
            {
                delta = y - _resizeStartMousePos;
                if (_resizingPanel.Position == PanelPosition.Bottom)
                    _resizingPanel.Height = Math.Clamp(_resizeStartSize - delta, 80, screenHeight * 0.5f);
                else if (_resizingPanel.Position == PanelPosition.Top)
                    _resizingPanel.Height = Math.Clamp(_resizeStartSize + delta, 80, screenHeight * 0.5f);
            }
            return;
        }
        
        // Update hovered panel and handle
        if (_draggingPanel == null)
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

        // Si hay un panel potencial para arrastrar, verificar threshold
        if (_potentialDragPanel != null && _draggingPanel == null)
        {
            float distance = (float)Math.Sqrt(
                Math.Pow(x - _mouseDownPosition.X, 2) + 
                Math.Pow(y - _mouseDownPosition.Y, 2)
            );

            // Solo empezar a arrastrar si se movi� m�s que el threshold
            if (distance > DragThreshold)
            {
                _draggingPanel = _potentialDragPanel;
                _potentialDragPanel = null;
                
                // ? GUARDAR estado original para restaurar si se cancela
                _originalPosition = _draggingPanel.Position;
                _originalIsFloating = _draggingPanel.IsFloating;
                _originalBounds = _draggingPanel.Bounds;
                
                // Hacer flotante si no lo era
                if (!_draggingPanel.IsFloating)
                {
                    _draggingPanel.IsFloating = true;
                    
                    // For bottom tabs: use default floating size, switch to next tab
                    if (_draggingPanel.Position == PanelPosition.Bottom)
                    {
                        _draggingPanel.FloatingWidth = Math.Min(_draggingPanel.Width, 400);
                        _draggingPanel.FloatingHeight = Math.Min(_draggingPanel.Height, 300);
                        
                        var remainingTabs = _panels.Values
                            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed && p != _draggingPanel)
                            .OrderBy(p => p.DockOrder).ToList();
                        if (remainingTabs.Count > 0)
                            _activeBottomTabId = remainingTabs[0].Config.Id;
                    }
                    else if (_draggingPanel.Position is PanelPosition.Left or PanelPosition.Right or PanelPosition.Center)
                    {
                        _draggingPanel.FloatingWidth = Math.Min(_draggingPanel.Bounds.Width, 600);
                        _draggingPanel.FloatingHeight = Math.Min(_draggingPanel.Bounds.Height, 400);

                        var remainingSide = _panels.Values
                            .Where(p => p.Position == _draggingPanel.Position && !p.IsFloating && !p.IsClosed && p != _draggingPanel)
                            .OrderBy(p => p.DockOrder).ToList();
                        if (remainingSide.Count > 0)
                            _activeTabIds[_draggingPanel.Position] = remainingSide[0].Config.Id;
                    }
                    else
                    {
                        _draggingPanel.FloatingWidth = _draggingPanel.Bounds.Width;
                        _draggingPanel.FloatingHeight = _draggingPanel.Bounds.Height;
                    }
                }
            }
        }

        // Si ya estamos arrastrando, actualizar posici�n
        if (_draggingPanel != null)
        {
            float newX = x - _dragOffset.X;
            float newY = y - _dragOffset.Y;
            float width = _draggingPanel.FloatingWidth > 0 ? _draggingPanel.FloatingWidth : _draggingPanel.Bounds.Width;
            float height = _draggingPanel.FloatingHeight > 0 ? _draggingPanel.FloatingHeight : _draggingPanel.Bounds.Height;
            
            _draggingPanel.Bounds = new SKRect(newX, newY, newX + width, newY + height);
            _currentDockZone = CalculateDockZone(x, y, screenWidth, screenHeight, headerHeight);
        }
    }

    private DockZone? CalculateDockZone(float x, float y, int screenWidth, int screenHeight, float headerHeight)
    {
        float statusBarH = StatusBarRenderer.Height;
        float availH = screenHeight - statusBarH;
        
        // Calculate center area (chart panel bounds)
        var chartArea = GetChartArea(screenWidth, screenHeight, headerHeight);
        float cx = chartArea.MidX;
        float cy = chartArea.MidY;

        // Center compass guides (inside chart area)
        if (DistanceTo(x, y, cx, cy - 50) < GuideHitRadius) // Top guide
            return new DockZone(PanelPosition.Top, 
                new SKRect(chartArea.Left, chartArea.Top, chartArea.Right, chartArea.MidY));
        
        if (DistanceTo(x, y, cx, cy + 50) < GuideHitRadius) // Bottom guide
            return new DockZone(PanelPosition.Bottom,
                new SKRect(chartArea.Left, chartArea.MidY, chartArea.Right, chartArea.Bottom));
        
        if (DistanceTo(x, y, cx - 50, cy) < GuideHitRadius) // Left guide
            return new DockZone(PanelPosition.Left,
                new SKRect(chartArea.Left, chartArea.Top, chartArea.MidX, chartArea.Bottom));
        
        if (DistanceTo(x, y, cx + 50, cy) < GuideHitRadius) // Right guide
            return new DockZone(PanelPosition.Right,
                new SKRect(chartArea.MidX, chartArea.Top, chartArea.Right, chartArea.Bottom));
        
        if (DistanceTo(x, y, cx, cy) < GuideHitRadius) // Center guide
            return new DockZone(PanelPosition.Center,
                chartArea);

        // Edge guides (at screen edges)
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
        float bottomMargin = 0;

        foreach (var panel in _panels.Values.Where(p => !p.IsFloating && !p.IsClosed))
        {
            float width = panel.IsCollapsed ? CollapsedWidth : panel.Width;
            float height = panel.IsCollapsed ? CollapsedWidth : panel.Height;

            if (panel.Position == PanelPosition.Left) leftMargin += width;
            if (panel.Position == PanelPosition.Right) rightMargin += width;
        }
        
        // Bottom: only count active tab + tab bar height
        var bottomTabs = _panels.Values
            .Where(p => p.Position == PanelPosition.Bottom && !p.IsFloating && !p.IsClosed)
            .ToList();
        if (bottomTabs.Count > 0)
        {
            var activeTab = bottomTabs.FirstOrDefault(t => t.Config.Id == _activeBottomTabId) ?? bottomTabs[0];
            float tabContentH = activeTab.IsCollapsed ? CollapsedWidth : activeTab.Height;
            bottomMargin = tabContentH + TabBarHeight;
        }

        // Ensure chart never collapses to zero — leave at least 200x120 px
        float chartRight  = Math.Max(screenWidth - rightMargin, leftMargin + 200);
        float chartBottom = Math.Max(availableBottom - bottomMargin, headerHeight + 120);
        return new SKRect(leftMargin, headerHeight, chartRight, chartBottom);
    }

    public bool IsMouseOverPanel(float x, float y) => _panels.Values.Any(p => p.Bounds.Contains(x, y));
    public bool IsDraggingPanel => _draggingPanel != null;
    public bool IsResizing => _resizingPanel != null;
    public bool IsPanelBeingDragged(DockablePanel panel) => _draggingPanel == panel;
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
                // Make it the active tab in its dock position
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
    
    private ResizeEdge GetResizeEdge(DockablePanel panel, float x, float y)
    {
        var b = panel.Bounds;
        
        switch (panel.Position)
        {
            case PanelPosition.Left:
                // Right edge of left panel
                if (x >= b.Right - ResizeEdgeWidth && x <= b.Right + ResizeEdgeWidth && y >= b.Top && y <= b.Bottom)
                    return ResizeEdge.Right;
                break;
            case PanelPosition.Right:
                // Left edge of right panel
                if (x >= b.Left - ResizeEdgeWidth && x <= b.Left + ResizeEdgeWidth && y >= b.Top && y <= b.Bottom)
                    return ResizeEdge.Left;
                break;
            case PanelPosition.Bottom:
                // Top edge of bottom panel content area
                if (y >= b.Top - ResizeEdgeWidth && y <= b.Top + ResizeEdgeWidth && x >= b.Left && x <= b.Right)
                    return ResizeEdge.Top;
                break;
            case PanelPosition.Top:
                // Bottom edge of top panel
                if (y >= b.Bottom - ResizeEdgeWidth && y <= b.Bottom + ResizeEdgeWidth && x >= b.Left && x <= b.Right)
                    return ResizeEdge.Bottom;
                break;
        }
        return ResizeEdge.None;
    }
    
    public void UpdateChartTitle(string symbol, string interval, float price)
    {
        var chart = GetPanel(PanelDefinitions.CHART);
        if (chart != null)
        {
            chart.DynamicTitle = $"{symbol} \u2022 {interval} \u2022 {price:F2}";
        }
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
            _activeTabIds.GetValueOrDefault(PanelPosition.Center, PanelDefinitions.CHART)
        );
    }

    public string GetActiveCenterTabId() => _activeTabIds.GetValueOrDefault(PanelPosition.Center, PanelDefinitions.CHART);
}

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
    /// T�tulo din�mico (ej: "BTCUSDT � 1m" para el chart)
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
