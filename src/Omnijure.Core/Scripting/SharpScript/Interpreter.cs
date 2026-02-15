using System;
using System.Collections.Generic;
using Omnijure.Core.DataStructures;

namespace Omnijure.Core.Scripting.SharpScript;

/// <summary>
/// Executes a SharpScript AST against a RingBuffer of candles.
/// Processes bar-by-bar from oldest to newest, building series arrays.
/// </summary>
public class Interpreter
{
    private readonly ScriptProgram _program;
    private readonly RingBuffer<Candle> _buffer;
    private readonly int _barCount;

    // Series storage: variable name → float array (index 0 = newest bar)
    private readonly Dictionary<string, float[]> _series = new();

    // EMA state tracking: "ema_{source}_{length}" → previous EMA value
    private readonly Dictionary<string, float> _emaState = new();

    // Script output
    private readonly ScriptOutput _output = new();

    // Current bar index during execution (0 = newest, Count-1 = oldest)
    private int _currentBar;

    // Input values (name → value)
    private readonly Dictionary<string, float> _inputValues = new();

    // Built-in source series (pre-populated from buffer)
    private float[] _close = [];
    private float[] _open = [];
    private float[] _high = [];
    private float[] _low = [];
    private float[] _volume = [];

    public Interpreter(ScriptProgram program, RingBuffer<Candle> buffer)
    {
        _program = program;
        _buffer = buffer;
        _barCount = buffer.Count;
    }

    /// <summary>
    /// Applies previously set input values (from ScriptManager).
    /// </summary>
    public void SetInputValues(Dictionary<string, float> values)
    {
        foreach (var kv in values)
            _inputValues[kv.Key] = kv.Value;
    }

    public ScriptOutput Execute()
    {
        if (_barCount == 0)
        {
            _output.Error = "No candle data";
            return _output;
        }

        _output.Clear();

        // Pre-populate built-in series from buffer
        // RingBuffer: index 0 = newest, Count-1 = oldest
        // We keep same indexing: series[0] = newest bar
        _close = new float[_barCount];
        _open = new float[_barCount];
        _high = new float[_barCount];
        _low = new float[_barCount];
        _volume = new float[_barCount];

        for (int i = 0; i < _barCount; i++)
        {
            ref readonly var candle = ref _buffer[i];
            _close[i] = candle.Close;
            _open[i] = candle.Open;
            _high[i] = candle.High;
            _low[i] = candle.Low;
            _volume[i] = candle.Volume;
        }

        // Process declaration
        if (_program.Declaration != null)
        {
            ProcessDeclaration(_program.Declaration);
        }

        // Execute bar-by-bar from oldest to newest
        // Series index: 0 = newest, barCount-1 = oldest
        // We iterate from oldest to newest so EMA/stateful functions work correctly
        for (int bar = _barCount - 1; bar >= 0; bar--)
        {
            _currentBar = bar;
            foreach (var stmt in _program.Statements)
            {
                ExecuteStatement(stmt);
            }
        }

        return _output;
    }

    private void ProcessDeclaration(FunctionCallExpr decl)
    {
        // indicator("title", overlay=true/false)
        // strategy("title", overlay=true/false)
        if (decl.Args.Count > 0 && decl.Args[0] is StringLiteral title)
            _output.Title = title.Value;

        if (decl.NamedArgs.TryGetValue("overlay", out var ov))
        {
            if (ov is BoolLiteral b) _output.IsOverlay = b.Value;
            else if (ov is IdentifierExpr id) _output.IsOverlay = id.Name == "true";
        }
        else
        {
            // indicator defaults to overlay=true, strategy defaults to overlay=true
            _output.IsOverlay = true;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Statement Execution
    // ═══════════════════════════════════════════════════════════

    private void ExecuteStatement(AstNode node)
    {
        switch (node)
        {
            case AssignmentStmt assign:
                var val = EvalFloat(assign.Value);
                GetOrCreateSeries(assign.Name)[_currentBar] = val;
                break;

            case ExpressionStmt exprStmt:
                EvalAny(exprStmt.Expression);
                break;

            case IfStmt ifStmt:
                if (EvalBool(ifStmt.Condition))
                {
                    foreach (var s in ifStmt.ThenBlock) ExecuteStatement(s);
                }
                else if (ifStmt.ElseBlock != null)
                {
                    foreach (var s in ifStmt.ElseBlock) ExecuteStatement(s);
                }
                break;
        }
    }

    // ═══════════════════════════════════════════════════════════
    // Expression Evaluation
    // ═══════════════════════════════════════════════════════════

    private object EvalAny(AstNode node)
    {
        switch (node)
        {
            case NumberLiteral n: return n.Value;
            case StringLiteral s: return s.Value;
            case BoolLiteral b: return b.Value;
            case ColorLiteral c: return c.Argb;

            case IdentifierExpr id:
                return ResolveIdentifier(id.Name);

            case BinaryExpr bin:
                return EvalBinary(bin);

            case UnaryExpr un:
                if (un.Op == "-") return -EvalFloat(un.Operand);
                if (un.Op == "not") return !EvalBool(un.Operand);
                return 0f;

            case TernaryExpr tern:
                return EvalBool(tern.Condition) ? EvalAny(tern.IfTrue) : EvalAny(tern.IfFalse);

            case FunctionCallExpr call:
                return EvalFunctionCall(call);

            default:
                return 0f;
        }
    }

    private float EvalFloat(AstNode node)
    {
        var result = EvalAny(node);
        if (result is float f) return f;
        if (result is int i) return i;
        if (result is bool b) return b ? 1f : 0f;
        if (result is uint u) return u; // color as number
        return 0f;
    }

    private bool EvalBool(AstNode node)
    {
        var result = EvalAny(node);
        if (result is bool b) return b;
        if (result is float f) return f != 0f;
        return false;
    }

    private uint EvalColor(AstNode node)
    {
        var result = EvalAny(node);
        if (result is uint u) return u;
        if (result is float f) return (uint)f;
        return 0xFFFFFFFF;
    }

    private object EvalBinary(BinaryExpr bin)
    {
        switch (bin.Op)
        {
            case "+": return EvalFloat(bin.Left) + EvalFloat(bin.Right);
            case "-": return EvalFloat(bin.Left) - EvalFloat(bin.Right);
            case "*": return EvalFloat(bin.Left) * EvalFloat(bin.Right);
            case "/":
                float div = EvalFloat(bin.Right);
                return div == 0f ? 0f : EvalFloat(bin.Left) / div;
            case "%":
                float mod = EvalFloat(bin.Right);
                return mod == 0f ? 0f : EvalFloat(bin.Left) % mod;
            case "==": return EvalFloat(bin.Left) == EvalFloat(bin.Right);
            case "!=": return EvalFloat(bin.Left) != EvalFloat(bin.Right);
            case ">": return EvalFloat(bin.Left) > EvalFloat(bin.Right);
            case ">=": return EvalFloat(bin.Left) >= EvalFloat(bin.Right);
            case "<": return EvalFloat(bin.Left) < EvalFloat(bin.Right);
            case "<=": return EvalFloat(bin.Left) <= EvalFloat(bin.Right);
            case "and": return EvalBool(bin.Left) && EvalBool(bin.Right);
            case "or": return EvalBool(bin.Left) || EvalBool(bin.Right);
            default: return 0f;
        }
    }

    private object ResolveIdentifier(string name)
    {
        return name switch
        {
            "close" => _close[_currentBar],
            "open" => _open[_currentBar],
            "high" => _high[_currentBar],
            "low" => _low[_currentBar],
            "volume" => _volume[_currentBar],
            "bar_index" => (float)(_barCount - 1 - _currentBar),
            "time" => (float)_buffer[_currentBar].Timestamp,
            "strategy.long" => 1f,
            "strategy.short" => -1f,
            "dashed" => (object)"dashed",
            "dotted" => (object)"dotted",
            "solid" => (object)"solid",
            _ => GetSeriesValue(name)
        };
    }

    private float GetSeriesValue(string name)
    {
        if (_series.TryGetValue(name, out var arr))
            return arr[_currentBar];
        return 0f;
    }

    private float[] GetOrCreateSeries(string name)
    {
        if (!_series.TryGetValue(name, out var arr))
        {
            arr = new float[_barCount];
            _series[name] = arr;
        }
        return arr;
    }

    // ═══════════════════════════════════════════════════════════
    // Function Call Dispatch
    // ═══════════════════════════════════════════════════════════

    private object EvalFunctionCall(FunctionCallExpr call)
    {
        switch (call.Name)
        {
            case "input": return EvalInput(call);
            case "sma": return EvalSma(call);
            case "ema": return EvalEma(call);
            case "rsi": return EvalRsi(call);
            case "stdev": return EvalStdev(call);
            case "highest": return EvalHighest(call);
            case "lowest": return EvalLowest(call);
            case "crossover": return EvalCrossover(call);
            case "crossunder": return EvalCrossunder(call);
            case "plot": EvalPlot(call); return 0f;
            case "hline": EvalHline(call); return 0f;
            case "bgcolor": EvalBgcolor(call); return 0f;
            case "plotshape": EvalPlotshape(call); return 0f;
            case "alertcondition": EvalAlertcondition(call); return 0f;
            case "strategy.entry": EvalStrategyEntry(call); return 0f;
            case "strategy.close": EvalStrategyClose(call); return 0f;
            case "math.abs": return MathF.Abs(EvalFloat(call.Args[0]));
            case "math.max": return MathF.Max(EvalFloat(call.Args[0]), EvalFloat(call.Args[1]));
            case "math.min": return MathF.Min(EvalFloat(call.Args[0]), EvalFloat(call.Args[1]));
            case "math.sqrt": return MathF.Sqrt(EvalFloat(call.Args[0]));
            default:
                throw new SharpScriptException($"Unknown function '{call.Name}'", call.Line, call.Column);
        }
    }

    private float EvalInput(FunctionCallExpr call)
    {
        float defaultVal = call.Args.Count > 0 ? EvalFloat(call.Args[0]) : 0f;
        string title = call.Args.Count > 1 && call.Args[1] is StringLiteral s ? s.Value : $"input_{_output.Inputs.Count}";

        // Check if input already registered
        if (_inputValues.TryGetValue(title, out float val))
            return val;

        // Only register on first bar execution
        if (_currentBar == _barCount - 1)
        {
            _output.Inputs.Add(new ScriptInput { Name = title, Default = defaultVal, Value = defaultVal });
            _inputValues[title] = defaultVal;
        }

        return defaultVal;
    }

    private float EvalSma(FunctionCallExpr call)
    {
        var source = ResolveSeries(call.Args[0]);
        int length = (int)EvalFloat(call.Args[1]);
        return Builtins.Sma(source, _currentBar, length);
    }

    private float EvalEma(FunctionCallExpr call)
    {
        var source = ResolveSeries(call.Args[0]);
        int length = (int)EvalFloat(call.Args[1]);

        string key = $"ema_{GetSeriesKey(call.Args[0])}_{length}";
        float prevEma = _emaState.GetValueOrDefault(key, float.NaN);
        float result = Builtins.Ema(source, _currentBar, length, prevEma);
        _emaState[key] = result;
        return result;
    }

    private float EvalRsi(FunctionCallExpr call)
    {
        var source = ResolveSeries(call.Args[0]);
        int length = (int)EvalFloat(call.Args[1]);
        return Builtins.Rsi(source, _currentBar, length);
    }

    private float EvalStdev(FunctionCallExpr call)
    {
        var source = ResolveSeries(call.Args[0]);
        int length = (int)EvalFloat(call.Args[1]);
        return Builtins.Stdev(source, _currentBar, length);
    }

    private float EvalHighest(FunctionCallExpr call)
    {
        var source = ResolveSeries(call.Args[0]);
        int length = (int)EvalFloat(call.Args[1]);
        return Builtins.Highest(source, _currentBar, length);
    }

    private float EvalLowest(FunctionCallExpr call)
    {
        var source = ResolveSeries(call.Args[0]);
        int length = (int)EvalFloat(call.Args[1]);
        return Builtins.Lowest(source, _currentBar, length);
    }

    private object EvalCrossover(FunctionCallExpr call)
    {
        var a = ResolveSeries(call.Args[0]);
        var b = ResolveSeries(call.Args[1]);
        return Builtins.Crossover(a, b, _currentBar);
    }

    private object EvalCrossunder(FunctionCallExpr call)
    {
        var a = ResolveSeries(call.Args[0]);
        var b = ResolveSeries(call.Args[1]);
        return Builtins.Crossunder(a, b, _currentBar);
    }

    // ═══════════════════════════════════════════════════════════
    // Output Functions (side-effect producing)
    // ═══════════════════════════════════════════════════════════

    private void EvalPlot(FunctionCallExpr call)
    {
        float value = EvalFloat(call.Args[0]);
        string title = call.Args.Count > 1 && call.Args[1] is StringLiteral s ? s.Value : $"Plot {_output.Plots.Count + 1}";
        uint color = call.NamedArgs.TryGetValue("color", out var c) ? EvalColor(c) :
                     call.Args.Count > 2 ? EvalColor(call.Args[2]) : 0xFFFFD700;
        float lineWidth = call.NamedArgs.TryGetValue("linewidth", out var lw) ? EvalFloat(lw) :
                          call.Args.Count > 3 ? EvalFloat(call.Args[3]) : 1.5f;

        // Find or create plot series
        PlotSeries? plot = null;
        foreach (var p in _output.Plots)
        {
            if (p.Title == title) { plot = p; break; }
        }
        if (plot == null)
        {
            plot = new PlotSeries
            {
                Title = title,
                Values = new float[_barCount],
                Color = color,
                LineWidth = lineWidth
            };
            Array.Fill(plot.Values, float.NaN);
            _output.Plots.Add(plot);
        }

        plot.Values[_currentBar] = value;
        plot.Color = color; // update in case of dynamic color
    }

    private void EvalHline(FunctionCallExpr call)
    {
        // Only register on first bar
        if (_currentBar != _barCount - 1) return;

        float price = EvalFloat(call.Args[0]);
        string title = call.Args.Count > 1 && call.Args[1] is StringLiteral s ? s.Value : "";
        uint color = call.NamedArgs.TryGetValue("color", out var c) ? EvalColor(c) : 0xFF808080;

        var style = HLineStyle.Dashed;
        if (call.NamedArgs.TryGetValue("linestyle", out var ls))
        {
            var lsVal = EvalAny(ls);
            if (lsVal is string str)
            {
                style = str switch
                {
                    "dotted" => HLineStyle.Dotted,
                    "solid" => HLineStyle.Solid,
                    _ => HLineStyle.Dashed
                };
            }
        }

        _output.HLines.Add(new HLineDef { Price = price, Title = title, Color = color, Style = style });
    }

    private void EvalBgcolor(FunctionCallExpr call)
    {
        uint color = EvalColor(call.Args[0]);
        if ((color & 0xFF000000) != 0) // only add if not fully transparent
        {
            _output.Backgrounds.Add(new BgColorEntry { BarIndex = _currentBar, Color = color });
        }
    }

    private void EvalPlotshape(FunctionCallExpr call)
    {
        bool condition = EvalBool(call.Args[0]);
        if (!condition) return;

        var style = ShapeStyle.TriangleUp;
        if (call.NamedArgs.TryGetValue("style", out var st))
        {
            var sv = EvalAny(st);
            if (sv is string str) style = ParseShapeStyle(str);
        }

        uint color = call.NamedArgs.TryGetValue("color", out var c) ? EvalColor(c) : 0xFF2ECC71;
        float price = _close[_currentBar];
        if (call.NamedArgs.TryGetValue("location", out var loc))
        {
            var lv = EvalAny(loc);
            if (lv is string locStr)
            {
                if (locStr == "abovebar") price = _high[_currentBar] * 1.002f;
                else if (locStr == "belowbar") price = _low[_currentBar] * 0.998f;
            }
        }

        _output.Shapes.Add(new ShapeMark { BarIndex = _currentBar, Price = price, Style = style, Color = color });
    }

    private void EvalAlertcondition(FunctionCallExpr call)
    {
        // Only register on first bar
        if (_currentBar != _barCount - 1) return;

        bool condition = EvalBool(call.Args[0]);
        string title = call.Args.Count > 1 && call.Args[1] is StringLiteral s ? s.Value : "Alert";
        string message = call.Args.Count > 2 && call.Args[2] is StringLiteral m ? m.Value : "";

        _output.Alerts.Add(new AlertDef { Title = title, Message = message, Triggered = new bool[_barCount] });
    }

    private void EvalStrategyEntry(FunctionCallExpr call)
    {
        string id = call.Args.Count > 0 && call.Args[0] is StringLiteral s ? s.Value : "default";
        float dir = call.Args.Count > 1 ? EvalFloat(call.Args[1]) : 1f;

        _output.Signals.Add(new StrategySignal
        {
            BarIndex = _currentBar,
            Id = id,
            Direction = dir > 0 ? SignalDirection.Long : SignalDirection.Short
        });
    }

    private void EvalStrategyClose(FunctionCallExpr call)
    {
        string id = call.Args.Count > 0 && call.Args[0] is StringLiteral s ? s.Value : "default";
        _output.Signals.Add(new StrategySignal
        {
            BarIndex = _currentBar,
            Id = id,
            Direction = SignalDirection.Close
        });
    }

    // ═══════════════════════════════════════════════════════════
    // Series Resolution
    // ═══════════════════════════════════════════════════════════

    private float[] ResolveSeries(AstNode node)
    {
        if (node is IdentifierExpr id)
        {
            return id.Name switch
            {
                "close" => _close,
                "open" => _open,
                "high" => _high,
                "low" => _low,
                "volume" => _volume,
                _ => _series.TryGetValue(id.Name, out var s) ? s : _close
            };
        }

        // For complex expressions, evaluate into a temp series
        var tempKey = $"__temp_{node.GetHashCode()}";
        if (!_series.ContainsKey(tempKey))
        {
            var arr = new float[_barCount];
            for (int i = _barCount - 1; i >= 0; i--)
            {
                int savedBar = _currentBar;
                _currentBar = i;
                arr[i] = EvalFloat(node);
                _currentBar = savedBar;
            }
            _series[tempKey] = arr;
        }
        return _series[tempKey];
    }

    private string GetSeriesKey(AstNode node)
    {
        if (node is IdentifierExpr id) return id.Name;
        return $"expr_{node.GetHashCode()}";
    }

    private static ShapeStyle ParseShapeStyle(string s) => s switch
    {
        "triangleup" => ShapeStyle.TriangleUp,
        "triangledown" => ShapeStyle.TriangleDown,
        "arrowup" => ShapeStyle.ArrowUp,
        "arrowdown" => ShapeStyle.ArrowDown,
        "circle" => ShapeStyle.Circle,
        "cross" => ShapeStyle.Cross,
        "diamond" => ShapeStyle.Diamond,
        _ => ShapeStyle.TriangleUp
    };
}
