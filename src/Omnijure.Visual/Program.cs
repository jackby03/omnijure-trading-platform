
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
    
    // Interaction State
    private static float _zoom = 1.0f; 
    private static int _scrollOffset = 0;
    private static bool _isDragging = false;
    private static Vector2D<float> _lastMousePos;

    public static void Main(string[] args)
    {
        var options = WindowOptions.Default;
        options.Size = new Vector2D<int>(1280, 720);
        options.Title = "Omnijure Trading Platform (NativeAOT)";
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
        var binance = new Omnijure.Core.Network.BinanceClient(_buffer);
        _ = binance.ConnectAsync();
        
        // 3. Init Mind
        try {
            // FIX: Ensure Clojure can find itself
            Environment.SetEnvironmentVariable("CLOJURE_LOAD_PATH", AppDomain.CurrentDomain.BaseDirectory);
            
            _mind = new Omnijure.Mind.ScriptEngine(Path.Combine(Directory.GetCurrentDirectory(), "strategies"));
            Console.WriteLine("Clojure Runtime Initialized.");
        } catch (Exception ex) {
            Console.WriteLine($"Mind Init Failed: {ex.Message}");
            if (ex.InnerException != null) Console.WriteLine($"Inner: {ex.InnerException.Message}");
        }
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
    }

    private static void OnScroll(IMouse arg1, ScrollWheel arg2)
    {
        _zoom += arg2.Y * 0.1f;
        _zoom = System.Math.Clamp(_zoom, 0.1f, 5.0f);
    }

    private static void OnMouseDown(IMouse arg1, MouseButton arg2) { if (arg2 == MouseButton.Left) _isDragging = true; }
    private static void OnMouseUp(IMouse arg1, MouseButton arg2) { if (arg2 == MouseButton.Left) _isDragging = false; }

    private static void OnMouseMove(IMouse arg1, System.Numerics.Vector2 pos)
    {
        // Note: Silk.NET input uses System.Numerics.Vector2
        if (_isDragging)
        {
            float deltaX = pos.X - _lastMousePos.X;
            _scrollOffset += (int)(deltaX * 0.5f);
            if (_scrollOffset < 0) _scrollOffset = 0;
        }
        _lastMousePos = new Vector2D<float>(pos.X, pos.Y);
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
        _renderer.Render(_surface.Canvas, _window.Size.X, _window.Size.Y, _buffer, decision, _scrollOffset, _zoom);
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
