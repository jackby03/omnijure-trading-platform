using SkiaSharp;
using Silk.NET.Input;
using Silk.NET.Maths;

namespace Omnijure.Visual.Rendering;

public class LayoutManager
{
    // Layout Config
    public float LeftSidebarWidth { get; private set; } = 250;
    public float RightSidebarWidth { get; private set; } = 300;
    private const float MinSidebarWidth = 100;
    private const float DividerWidth = 6;
    
    // Bounds
    public SKRect LeftSidebarRect { get; private set; }
    public SKRect ChartRect { get; private set; }
    public SKRect RightSidebarRect { get; private set; }
    public SKRect DividerLeftRect { get; private set; }
    public SKRect DividerRightRect { get; private set; }
    
    // State
    public bool IsResizingLeft { get; private set; }
    public bool IsResizingRight { get; private set; }
    
    // Renderers
    private readonly SidebarRenderer _sidebar;
    
    public LayoutManager()
    {
        _sidebar = new SidebarRenderer();
    }
    
    public void UpdateLayout(int width, int height)
    {
        // Clamp Sidebar widths
        if (LeftSidebarWidth < MinSidebarWidth) LeftSidebarWidth = MinSidebarWidth;
        if (RightSidebarWidth < MinSidebarWidth) RightSidebarWidth = MinSidebarWidth;
        
        float remainingW = width - (DividerWidth * 2);
        if (LeftSidebarWidth + RightSidebarWidth > remainingW - 200)
        {
             // Adjust if chart gets too small
             float ratio = LeftSidebarWidth / (LeftSidebarWidth + RightSidebarWidth);
             float totalSidebars = remainingW - 200;
             LeftSidebarWidth = totalSidebars * ratio;
             RightSidebarWidth = totalSidebars * (1 - ratio);
        }

        // 1. Left Sidebar
        LeftSidebarRect = new SKRect(0, 0, LeftSidebarWidth, height);
        
        // 2. Left Divider
        DividerLeftRect = new SKRect(LeftSidebarWidth, 0, LeftSidebarWidth + DividerWidth, height);
        
        // 3. Right Sidebar (from right side)
        RightSidebarRect = new SKRect(width - RightSidebarWidth, 0, width, height);
        
        // 4. Right Divider
        DividerRightRect = new SKRect(width - RightSidebarWidth - DividerWidth, 0, width - RightSidebarWidth, height);
        
        // 5. Center Chart
        ChartRect = new SKRect(LeftSidebarWidth + DividerWidth, 0, width - RightSidebarWidth - DividerWidth, height);
    }
    
    public void HandleMouseDown(float x, float y)
    {
        if (DividerLeftRect.Contains(x, y)) IsResizingLeft = true;
        else if (DividerRightRect.Contains(x, y)) IsResizingRight = true;
    }
    
    public void HandleMouseUp()
    {
        IsResizingLeft = false;
        IsResizingRight = false;
    }
    
    public void HandleMouseMove(float x, float y, float deltaX)
    {
        if (IsResizingLeft)
        {
            LeftSidebarWidth += deltaX;
        }
        else if (IsResizingRight)
        {
            RightSidebarWidth -= deltaX;
        }
    }
    
    public bool IsMouseOverDivider(float x, float y) => DividerLeftRect.Contains(x,y) || DividerRightRect.Contains(x,y);
    
    public void Render(SKCanvas canvas, ChartRenderer renderer, 
        Omnijure.Core.DataStructures.RingBuffer<Omnijure.Core.DataStructures.Candle> buffer, 
        string decision, int scrollOffset, float zoom, 
        string symbol, string interval, ChartType chartType, 
        System.Collections.Generic.List<UiButton> buttons,
        float minPrice, float maxPrice, Vector2D<float> mousePos,
        Omnijure.Core.DataStructures.OrderBook orderBook,
        Omnijure.Core.DataStructures.RingBuffer<Omnijure.Core.DataStructures.MarketTrade> trades)
    {
        using var divPaint = new SKPaint { Color = new SKColor(30,30,30), Style = SKPaintStyle.Fill };
        using var activeDivPaint = new SKPaint { Color = SKColors.Blue, Style = SKPaintStyle.Fill };

        // 1. Draw Dividers
        canvas.DrawRect(DividerLeftRect, IsResizingLeft ? activeDivPaint : divPaint);
        canvas.DrawRect(DividerRightRect, IsResizingRight ? activeDivPaint : divPaint);
        
        // 2. Draw Left Sidebar (Order Book)
        canvas.Save();
        canvas.ClipRect(LeftSidebarRect);
        canvas.Translate(LeftSidebarRect.Left, LeftSidebarRect.Top);
        _sidebar.RenderOrderBook(canvas, LeftSidebarRect.Width, LeftSidebarRect.Height, orderBook);
        canvas.Restore();

        // 3. Draw Right Sidebar (Watchlist & Trades)
        canvas.Save();
        canvas.ClipRect(RightSidebarRect);
        canvas.Translate(RightSidebarRect.Left, RightSidebarRect.Top);
        _sidebar.RenderRightSidebar(canvas, RightSidebarRect.Width, RightSidebarRect.Height, trades);
        canvas.Restore();
        
        // 4. Draw Chart
        canvas.Save();
        canvas.ClipRect(ChartRect);
        canvas.Translate(ChartRect.Left, ChartRect.Top);
        // Correcting mousePos: ChartRenderer expects local mouse coordinates
        var localMouse = new Vector2D<float>(mousePos.X - ChartRect.Left, mousePos.Y);
        renderer.Render(canvas, (int)ChartRect.Width, (int)ChartRect.Height, buffer, decision, scrollOffset, zoom, symbol, interval, chartType, buttons, minPrice, maxPrice, localMouse);
        canvas.Restore();
    }
}
