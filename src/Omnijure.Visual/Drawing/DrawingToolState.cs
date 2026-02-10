using System.Collections.Generic;

namespace Omnijure.Visual.Drawing;

/// <summary>
/// Available drawing tools in the TradingView-style toolbar
/// </summary>
public enum DrawingTool
{
    None,           // Default cursor (no drawing)
    TrendLine,      // Draw trend lines between two points
    HorizontalLine, // Draw horizontal price levels
    VerticalLine,   // Draw vertical time markers
    Rectangle,      // Draw boxes/rectangles
    Circle,         // Draw circles/ellipses
    Fibonacci,      // Fibonacci retracement levels
    Text            // Add text annotations
}

/// <summary>
/// Manages the state of drawing tools and drawn objects
/// </summary>
public class DrawingToolState
{
    /// <summary>
    /// Currently active drawing tool
    /// </summary>
    public DrawingTool ActiveTool { get; set; } = DrawingTool.None;

    /// <summary>
    /// List of all drawn objects on the chart
    /// </summary>
    public List<DrawingObject> Objects { get; } = new();

    /// <summary>
    /// The object currently being drawn (not yet completed)
    /// </summary>
    public DrawingObject? CurrentDrawing { get; set; } = null;

    /// <summary>
    /// The currently selected object (for editing/deletion)
    /// </summary>
    public DrawingObject? SelectedObject { get; set; } = null;

    /// <summary>
    /// Whether the user is currently dragging to create/edit a drawing
    /// </summary>
    public bool IsDragging { get; set; } = false;

    /// <summary>
    /// Adds a completed drawing to the objects list
    /// </summary>
    public void AddDrawing(DrawingObject obj)
    {
        Objects.Add(obj);
        CurrentDrawing = null;
    }

    /// <summary>
    /// Removes the selected object from the chart
    /// </summary>
    public void DeleteSelected()
    {
        if (SelectedObject != null)
        {
            Objects.Remove(SelectedObject);
            SelectedObject = null;
        }
    }

    /// <summary>
    /// Clears all drawings from the chart
    /// </summary>
    public void ClearAll()
    {
        Objects.Clear();
        CurrentDrawing = null;
        SelectedObject = null;
    }

    /// <summary>
    /// Selects the object at the given screen coordinates
    /// </summary>
    public void SelectAt(float x, float y)
    {
        // Check in reverse order (top-most drawings first)
        for (int i = Objects.Count - 1; i >= 0; i--)
        {
            if (Objects[i].HitTest(x, y, 10)) // 10px tolerance
            {
                SelectedObject = Objects[i];
                return;
            }
        }

        SelectedObject = null;
    }

    /// <summary>
    /// Cancels the current drawing operation
    /// </summary>
    public void CancelDrawing()
    {
        CurrentDrawing = null;
        IsDragging = false;
    }
}
