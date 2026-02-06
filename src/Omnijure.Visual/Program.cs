
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL; // Raw GL
using SkiaSharp;
using Omnijure.Visual.Rendering;
using Omnijure.Core.DataStructures;

using Silk.NET.Input;

namespace Omnijure.Visual;

public static class Program
{
    private static IWindow _window;
    private static GL _gl;
    private static GRContext _grContext;
    private static SKSurface _surface;
    private static ChartRenderer _renderer;
    private static Omnijure.Mind.ScriptEngine _mind;
    
    // Data
    private static RingBuffer<Candle> _buffer;
    private static Omnijure.Core.Network.BinanceClient _binance;
    
    // State
    private static string _currentSymbol = "BTCUSDT";
    private static string _currentTimeframe = "1m";
    private static Omnijure.Visual.Rendering.ChartType _chartType = Omnijure.Visual.Rendering.ChartType.Candles;

    // Interaction State
    private static float _zoom = 1.0f; 
    private static int _scrollOffset = 0;
    private static bool _isDragging = false;
    private static Vector2D<float> _lastMousePos;
    private static Vector2D<float> _mousePos;

    // UI State
    private static System.Collections.Generic.List<Omnijure.Visual.Rendering.UiButton> _uiButtons = new();

    public static void Main(string[] args)
    {
        // FIX: ClojureCLR crashes if environment variables have duplicate keys (case-insensitive).
        // Windows allows "Path" and "PATH" sometimes. We need to clean this up manually if we could, 
        // but Processstartinfo is read-only for current process usually. 
        // However, Clojure reads from `Environment.GetEnvironmentVariables()`.
        // We can try to proactively remove duplicates if we find any specific culprits like "Casing".
        // Or we just hope setting `CLOJURE_LOAD_PATH` is enough override.
        
        // Actually, the error "Key: Casing" implies there is literally an env var named "Casing" that conflicts? 
        // Or it's a Dictionary key collision inside Clojure's RT.
        // Let's print env vars for debug if we crash again.
        
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "Omnijure (1/2/3=Timeframe, Scroll=Zoom, Drag=Pan)";
        options.VSync = true;
        options.FramesPerSecond = 144;
        options.UpdatesPerSecond = 144;

        _window = Window.Create(options);

        _window.Load += OnLoad;
        _window.Render += OnRender;
        _window.Resize += OnResize;
        _window.Closing += OnClose;

        _window.Run();
    }

    private static void OnLoad()
    {
        // 0. Input Setup
        var input = _window.CreateInput();
        foreach (var keyboard in input.Keyboards)
            keyboard.KeyDown += OnKeyDown;
        
        foreach (var mouse in input.Mice)
        {
            mouse.Scroll += OnScroll;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
        }

        // 1. Init Raw GL
        _gl = _window.CreateOpenGL();
        
        // 2. Init Skia
        using var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);
        
        
        _renderer = new ChartRenderer();
        _buffer = new RingBuffer<Candle>(4096);
        
        // 4. REAL DATA (The Metal)
        _binance = new Omnijure.Core.Network.BinanceClient(_buffer);
        _ = _binance.ConnectAsync(_currentSymbol, _currentTimeframe);
        
        // 3. Init Mind
        try {
            Environment.SetEnvironmentVariable("CLOJURE_LOAD_PATH", AppDomain.CurrentDomain.BaseDirectory);
            _mind = new Omnijure.Mind.ScriptEngine(Path.Combine(Directory.GetCurrentDirectory(), "strategies"));
            Console.WriteLine("Clojure Runtime Initialized.");
        } catch (Exception ex) {
            Console.WriteLine($"Mind Init Failed: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }

        // Setup UI
        SetupUi();
    }

    private static void SetupUi()
    {
        _uiButtons.Clear();
        int x = 20;
        
        // Assets
        var assets = new[] { "BTCUSDT", "ETHUSDT", "SOLUSDT", "XRPUSDT" };
        foreach(var asset in assets)
        {
            // Use local variable for closure capture if needed, though C# 5+ handles foreach capture.
            // But string is immutable so it's fine.
            string a = asset; 
            _uiButtons.Add(new UiButton(x, 10, 80, 30, a, () => SwitchContext(a, _currentTimeframe)));
            x += 85;
        }

        x += 20;
        // Timeframes
        var tfs = new[] { "1m", "5m", "15m", "1h" };
        foreach(var tf in tfs)
        {
            string t = tf;
            _uiButtons.Add(new UiButton(x, 10, 40, 30, t, () => SwitchContext(_currentSymbol, t)));
            x += 45;
        }
        
        x += 20;
        // Chart Types
        _uiButtons.Add(new UiButton(x, 10, 60, 30, "Candle", () => _chartType = ChartType.Candles)); x += 65;
        _uiButtons.Add(new UiButton(x, 10, 60, 30, "Line", () => _chartType = ChartType.Line)); x += 65;
        _uiButtons.Add(new UiButton(x, 10, 60, 30, "Area", () => _chartType = ChartType.Area)); x += 65;
    }

    private static void OnResize(Vector2D<int> size)
    {
        // Update Viewport
        _gl?.Viewport(size);

        CreateSurface(size);
    }

    // Input Handlers
    private static void OnKeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        if (arg2 == Key.Space) { _scrollOffset = 0; _zoom = 1.0f; }
        
        // Timeframe Switching
        if (arg2 == Key.Number1) SwitchContext(_currentSymbol, "1m");
        if (arg2 == Key.Number2) SwitchContext(_currentSymbol, "5m");
        if (arg2 == Key.Number3) SwitchContext(_currentSymbol, "15m");
        // Asset Switching
        if (arg2 == Key.F1) SwitchContext("BTCUSDT", _currentTimeframe);
        if (arg2 == Key.F2) SwitchContext("ETHUSDT", _currentTimeframe);
        if (arg2 == Key.F3) SwitchContext("SOLUSDT", _currentTimeframe);
        if (arg2 == Key.F4) SwitchContext("XRPUSDT", _currentTimeframe);
    }

    private static void SwitchContext(string symbol, string interval)
    {
        _currentSymbol = symbol;
        _currentTimeframe = interval;
        Console.WriteLine($"[Interface] Switching to {symbol} {interval}...");
        _buffer.Clear(); 
        
        // Update Title
        _window.Title = $"Omnijure - {symbol} [{interval}]";
        
        _ = _binance.ConnectAsync(symbol, interval);
    }

    private static void OnScroll(IMouse arg1, ScrollWheel arg2)
    {
        // Logarithmic / Smoother Zoom
        if (arg2.Y > 0) _zoom *= 1.1f;
        else _zoom *= 0.9f;
        
        // Clamp Zoom
        _zoom = System.Math.Clamp(_zoom, 0.05f, 10.0f);
    }

    private static void OnMouseDown(IMouse arg1, MouseButton arg2) 
    { 
        if (arg2 == MouseButton.Left) 
        {
            // UI Hit Test
            bool uiClicked = false;
            foreach(var btn in _uiButtons)
            {
                if (btn.Contains(_mousePos.X, _mousePos.Y))
                {
                    btn.Action?.Invoke();
                    uiClicked = true;
                    // For chart types, we just update state. For Context switch, we might rebuild buttons but they are static mostly.
                    break;
                }
            }
            
            if (!uiClicked) _isDragging = true; 
        }
    }
    
    private static void OnMouseUp(IMouse arg1, MouseButton arg2) { if (arg2 == MouseButton.Left) _isDragging = false; }

    private static void OnMouseMove(IMouse arg1, System.Numerics.Vector2 pos)
    {
        _mousePos = new Vector2D<float>(pos.X, pos.Y);
        
        if (_isDragging)
        {
            float deltaX = pos.X - _lastMousePos.X;
            // Improved Pan Sensitivity: Match candle width ideally
            // Simple heuristic to make it feel better:
            _scrollOffset += (int)(deltaX * 0.1f * (_zoom < 1 ? 1 : 1/_zoom)); 
            if (_scrollOffset < 0) _scrollOffset = 0;
        }
        _lastMousePos = new Vector2D<float>(pos.X, pos.Y);
        
        // Hover effects
        foreach(var btn in _uiButtons) btn.IsHovered = btn.Contains(pos.X, pos.Y);
    }

    private static void OnRender(double delta)
    {
        if (_surface == null)
        {
            if (_window.Size.X > 0 && _window.Size.Y > 0)
                CreateSurface(_window.Size);
            return;
        }

        // Skia Render
        float currentPrice = 0;
        if (_buffer.Count > 0) currentPrice = _buffer[0].Close;

        // 1. THE METAL: Calculate Indicators
        float rsi = Omnijure.Core.Math.TechnicalAnalysis.CalculateRSI(_buffer, 14);
        float rvol = Omnijure.Core.Math.TechnicalAnalysis.CalculateRVOL(_buffer, 20);
        float sma = Omnijure.Core.Math.TechnicalAnalysis.CalculateSMA(_buffer, 50);

        // 2. THE MIND: Execute Strategy
        var signals = new System.Collections.Generic.Dictionary<string, float>
        {
            { "price", currentPrice },
            { "rsi", rsi },
            { "rvol", rvol },
            { "sma", sma }
        };

        _mind?.InvokeTick(signals);
        string decision = _mind?.LastDecision ?? "OFFLINE";

        // Pass Interaction State
        _renderer.Render(_surface.Canvas, _window.Size.X, _window.Size.Y, _buffer, decision, _scrollOffset, _zoom, _currentSymbol, _currentTimeframe, _chartType, _uiButtons);
        _surface.Canvas.Flush();
    }
    
    // Abstracted for reuse
    private static void CreateSurface(Vector2D<int> size)
    {
         _surface?.Dispose();
        
        var renderTarget = new GRBackendRenderTarget(
            size.X, 
            size.Y, 
            0, 
            8, 
            new GRGlFramebufferInfo(0, 0x8058)); // GL_RGBA8
            
        // Try Rgba8888 first as it matches GL_RGBA8
        _surface = SKSurface.Create(_grContext, renderTarget, GRSurfaceOrigin.BottomLeft, SKColorType.Rgba8888);
    }

    private static void OnClose()
    {
        _surface?.Dispose();
        _grContext?.Dispose();
    }
}
