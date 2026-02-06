using SkiaSharp;
using Silk.NET.Input;

namespace Omnijure.Visual.Rendering;

public class LayoutManager
{
    // Layout Config
    public float SidebarWidth { get; private set; } = 300;
    private const float MinSidebarWidth = 100;
    private const float DividerWidth = 6;
    
    // Bounds
    public SKRect ChartRect { get; private set; }
    public SKRect SidebarRect { get; private set; }
    public SKRect DividerRect { get; private set; }
    
    // State
    public bool IsResizingSidebar { get; private set; }
    
    // Renderers
    private readonly SidebarRenderer _sidebar;
    
    public LayoutManager()
    {
        _sidebar = new SidebarRenderer();
    }
    
    public void UpdateLayout(int width, int height)
    {
        // Clamp Sidebar
        if (SidebarWidth > width - 100) SidebarWidth = width - 100;
        if (SidebarWidth < MinSidebarWidth) SidebarWidth = MinSidebarWidth;
        
        float chartW = width - SidebarWidth - DividerWidth;
        
        ChartRect = new SKRect(0, 0, chartW, height);
        DividerRect = new SKRect(chartW, 0, chartW + DividerWidth, height);
        SidebarRect = new SKRect(chartW + DividerWidth, 0, width, height);
    }
    
    public void HandleMouseDown(float x, float y)
    {
        if (DividerRect.Contains(x, y))
        {
            IsResizingSidebar = true;
        }
    }
    
    public void HandleMouseUp()
    {
        IsResizingSidebar = false;
    }
    
    public void HandleMouseMove(float x, float y, float deltaX)
    {
        if (IsResizingSidebar)
        {
            SidebarWidth -= deltaX; // Moving left increases sidebar width
        }
    }
    
    public bool IsMouseOverDivider(float x, float y) => DividerRect.Contains(x,y);
    
    public void Render(SKCanvas canvas, ChartRenderer chartRenderer, 
        Omnijure.Core.DataStructures.RingBuffer<Omnijure.Core.DataStructures.Candle> buffer, 
        string decision, int scrollOffset, float zoom, 
        string symbol, string interval, ChartType chartType, 
        System.Collections.Generic.List<UiButton> buttons,
        float minPrice, float maxPrice)
    {
        // 1. Draw Divider
        using var divPaint = new SKPaint { Color = new SKColor(30,30,30), Style = SKPaintStyle.Fill };
        if (IsResizingSidebar) divPaint.Color = SKColors.Blue;
        canvas.DrawRect(DividerRect, divPaint);
        
        // 2. Draw Sidebar
        canvas.Save();
        canvas.ClipRect(SidebarRect);
        canvas.Translate(SidebarRect.Left, SidebarRect.Top);
        _sidebar.Render(canvas, SidebarRect.Width, SidebarRect.Height);
        canvas.Restore();
        
        // 3. Draw Chart (Updating its internal logic to clip or just draw within Rect)
        // ChartRenderer usually draws 0..Width. We probably want to Translate/Clip for it too?
        // Wait, ChartRenderer expects a full "Width/Height" to draw axes. 
        // If we just clip/translate, the axes will be drawn at 0..ChartWidth. This is correct.
        canvas.Save();
        canvas.ClipRect(ChartRect);
        // ChartRenderer draws at 0,0 so no translate needed if ChartRect is at 0,0. Which it is.
        // But we must pass ChartRect.Width/Height to it.
        chartRenderer.Render(canvas, (int)ChartRect.Width, (int)ChartRect.Height, buffer, decision, scrollOffset, zoom, symbol, interval, chartType, buttons, minPrice, maxPrice);
        canvas.Restore();
    }
}
