using System.Collections.Generic;

namespace Omnijure.Core.Scripting;

// ═══════════════════════════════════════════════════════════════
// Script Output — produced by interpreter, consumed by renderer
// ═══════════════════════════════════════════════════════════════

public class ScriptOutput
{
    public string Title { get; set; } = "Untitled";
    public bool IsOverlay { get; set; } = true;
    public string? Error { get; set; }

    public List<PlotSeries> Plots { get; set; } = new();
    public List<HLineDef> HLines { get; set; } = new();
    public List<ShapeMark> Shapes { get; set; } = new();
    public List<BgColorEntry> Backgrounds { get; set; } = new();
    public List<AlertDef> Alerts { get; set; } = new();
    public List<StrategySignal> Signals { get; set; } = new();
    public List<ScriptInput> Inputs { get; set; } = new();

    public void Clear()
    {
        Plots.Clear();
        HLines.Clear();
        Shapes.Clear();
        Backgrounds.Clear();
        Alerts.Clear();
        Signals.Clear();
        Error = null;
    }
}

public class PlotSeries
{
    public string Title { get; set; } = "";
    public float[] Values { get; set; } = [];
    public uint Color { get; set; } = 0xFFFFD700; // gold default
    public float LineWidth { get; set; } = 1.5f;
}

public class HLineDef
{
    public float Price { get; set; }
    public string Title { get; set; } = "";
    public uint Color { get; set; } = 0xFF808080;
    public HLineStyle Style { get; set; } = HLineStyle.Dashed;
}

public enum HLineStyle { Solid, Dashed, Dotted }

public class ShapeMark
{
    public int BarIndex { get; set; }
    public float Price { get; set; }
    public ShapeStyle Style { get; set; } = ShapeStyle.TriangleUp;
    public uint Color { get; set; } = 0xFF2ECC71;
    public string? Text { get; set; }
}

public enum ShapeStyle { TriangleUp, TriangleDown, ArrowUp, ArrowDown, Circle, Cross, Diamond }

public class BgColorEntry
{
    public int BarIndex { get; set; }
    public uint Color { get; set; } // ARGB with alpha for transparency
}

public class AlertDef
{
    public string Title { get; set; } = "";
    public string Message { get; set; } = "";
    public bool[] Triggered { get; set; } = [];
}

public class StrategySignal
{
    public int BarIndex { get; set; }
    public string Id { get; set; } = "";
    public SignalDirection Direction { get; set; }
}

public enum SignalDirection { Long, Short, Close }

public class ScriptInput
{
    public string Name { get; set; } = "";
    public float Default { get; set; }
    public float Value { get; set; }
}
