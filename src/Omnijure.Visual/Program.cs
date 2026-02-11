
using Silk.NET.Windowing;
using Silk.NET.Maths;
using Silk.NET.OpenGL; // Raw GL
using SkiaSharp;
using Omnijure.Visual.Rendering;
using Omnijure.Core.DataStructures;
using Omnijure.Core.Network;

using System.Linq;
using System.Text.Json;
using Silk.NET.Input;

namespace Omnijure.Visual;

public static partial class Program
{
    private static IWindow _window;
    private static GL _gl;
    private static GRContext _grContext;
    private static SKSurface _surface;
    private static ChartRenderer _renderer;
    private static LayoutManager _layout;
    private static ToolbarRenderer _toolbar;
    private static SearchModalRenderer _searchModalRenderer;
    private static Omnijure.Mind.ScriptEngine _mind;
    
    // Data
    private static BinanceClient _binance;
    private static OrderBook _orderBook;
    private static RingBuffer<Candle> _buffer;
    private static RingBuffer<MarketTrade> _trades;
    
    // UI Elements
    private static List<UiButton> _uiButtons = new List<UiButton>();
    private static List<UiDropdown> _uiDropdowns = new List<UiDropdown>();
    private static UiDropdown _assetDropdown;
    private static UiDropdown _intervalDropdown;
    private static UiDropdown _chartTypeDropdown;
    private static UiSearchBox _searchBox;
    private static UiSearchModal _searchModal;
    
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
    
    // Viewport State
    private static bool _isResizingPrice = false;
    private static bool _autoScaleY = true;
    private static float _viewMinY;
    private static float _viewMaxY;

    // Drawing Tools State
    private static Omnijure.Visual.Drawing.DrawingToolState _drawingState = new();

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
        options.Size = new Vector2D<int>(1440, 900);
        options.Title = "Omnijure";
        options.VSync = true;
        options.FramesPerSecond = 144;
        options.UpdatesPerSecond = 144;
        options.WindowBorder = WindowBorder.Hidden;
        options.WindowState = WindowState.Normal;

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
        {
            keyboard.KeyDown += OnKeyDown;
            keyboard.KeyChar += OnKeyChar;
        }
        
        foreach (var mouse in input.Mice)
        {
            mouse.Scroll += OnScroll;
            mouse.MouseDown += OnMouseDown;
            mouse.MouseUp += OnMouseUp;
            mouse.MouseMove += OnMouseMove;
        }

        // 1. Init Raw GL
        _gl = _window.CreateOpenGL();
        
        // Set window icon from embedded SVG
        try
        {
            var assembly = System.Reflection.Assembly.GetExecutingAssembly();
            using var stream = assembly.GetManifestResourceStream("Omnijure.Visual.Assets.logo.svg");
            if (stream != null)
            {
                using var reader = new System.IO.StreamReader(stream);
                var svg = new Svg.Skia.SKSvg();
                svg.FromSvg(reader.ReadToEnd());
                if (svg.Picture != null)
                {
                    int iconSize = 32;
                    using var surface = SKSurface.Create(new SKImageInfo(iconSize, iconSize, SKColorType.Rgba8888, SKAlphaType.Premul));
                    var c = surface.Canvas;
                    c.Clear(SKColors.Transparent);
                    float svgW = svg.Picture.CullRect.Width;
                    float svgH = svg.Picture.CullRect.Height;
                    float scale = (iconSize * 0.9f) / Math.Max(svgW, svgH);
                    c.Translate((iconSize - svgW * scale) / 2f, (iconSize - svgH * scale) / 2f);
                    c.Scale(scale, scale);
                    c.DrawPicture(svg.Picture);
                    using var img = surface.Snapshot();
                    using var pixels = img.PeekPixels();
                    var rawBytes = pixels.GetPixelSpan().ToArray();
                    var rawImage = new Silk.NET.Core.RawImage(iconSize, iconSize, new System.Memory<byte>(rawBytes));
                    _window.SetWindowIcon(ref rawImage);
                }
            }
        }
        catch { /* Icon loading is non-critical */ }
        
        // Center window on screen
        try
        {
            var monitor = _window.Monitor;
            if (monitor.VideoMode.Resolution.HasValue)
            {
                var res = monitor.VideoMode.Resolution.Value;
                _window.Position = new Vector2D<int>(
                    (res.X - _window.Size.X) / 2,
                    (res.Y - _window.Size.Y) / 2);
            }
        }
        catch { /* Monitor info not available */ }
        
        // 2. Init Skia
        using var glInterface = GRGlInterface.Create();
        _grContext = GRContext.CreateGl(glInterface);
        
        
        
        _renderer = new ChartRenderer();
        _layout = new LayoutManager();
        _toolbar = new ToolbarRenderer();
        _toolbar.SetWindowActions(
            onClose: () => _window.Close(),
            onMinimize: () => _window.WindowState = WindowState.Minimized,
            onMaximize: () => _window.WindowState = _window.WindowState == WindowState.Maximized 
                ? WindowState.Normal : WindowState.Maximized,
            onWindowMove: (x, y) => _window.Position = new Vector2D<int>(x, y),
            windowHandle: _window.Native!.Win32!.Value.Hwnd
        );
        _searchModalRenderer = new SearchModalRenderer();
        _buffer = new RingBuffer<Candle>(4096);
        _trades = new RingBuffer<MarketTrade>(1024);
        
        // 4. REAL DATA (The Metal)
        _orderBook = new OrderBook();
        _binance = new Omnijure.Core.Network.BinanceClient(_buffer, _orderBook, _trades);
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

        // Background stats fetch (24h Ticker)
        _ = Task.Run(async () => {
            using var httpClient = new System.Net.Http.HttpClient();
            while (true)
            {
                try {
                    string currentSymbol = _currentSymbol; 
                    var response = await httpClient.GetStringAsync($"https://api.binance.com/api/v3/ticker/24hr?symbol={currentSymbol}");
                    using var doc = JsonDocument.Parse(response);
                    float price = float.Parse(doc.RootElement.GetProperty("lastPrice").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                    float change = float.Parse(doc.RootElement.GetProperty("priceChangePercent").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                    
                    if (_assetDropdown != null)
                    {
                        _assetDropdown.CurrentPrice = price;
                        _assetDropdown.PercentChange = change;
                    }
                } catch { }
                await Task.Delay(5000);
            }
        });
    }

    private static void SetupUi()
    {
        _uiButtons.Clear();
        _uiDropdowns.Clear();

        // 0. Search Box (rect se actualiza en ToolbarRenderer.Render)
        _searchBox = new UiSearchBox(0, 0, 0, 0);
        
        // 0.5. Search Modal
        _searchModal = new UiSearchModal();

        // 1. Asset Dropdown (data only - not clickeable, uses search modal)
        var assets = new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT", "XRPUSDT" };
        _assetDropdown = new UiDropdown(0, 0, 0, 0, "Asset", assets, (s) => SwitchContext(s, _currentTimeframe));
        // NOT added to _uiDropdowns - asset selection uses search modal

        // Fetch full list in background and preload crypto icons
        Task.Run(async () => {
            var fullList = await Omnijure.Core.Network.BinanceService.GetAllUsdtSymbolsAsync();
            if (fullList != null && fullList.Count > 0)
            {
                _assetDropdown.Items = fullList;
                _searchModal?.SetSymbols(fullList);

                // Preload icons for the most popular cryptos
                Omnijure.Visual.Rendering.CryptoIconProvider.PreloadIcons(
                    fullList.Take(30)
                );
            }
        });

        // 2. Interval Dropdown (clickeable)
        var intervals = new List<string> { "1m", "5m", "15m", "1h", "4h", "1d" };
        _intervalDropdown = new UiDropdown(0, 0, 0, 0, "Interval", intervals, (tf) => SwitchContext(_currentSymbol, tf));
        _uiDropdowns.Add(_intervalDropdown);

        // 3. Chart Type Dropdown (data only - rendered in toolbar, not as separate dropdown)
        var chartTypes = new List<string> { "Candles", "Line", "Area" };
        _chartTypeDropdown = new UiDropdown(0, 0, 0, 0, "Chart", chartTypes, (type) => {
            if (type == "Candles") _chartType = ChartType.Candles;
            else if (type == "Line") _chartType = ChartType.Line;
            else if (type == "Area") _chartType = ChartType.Area;
        });
    }

    // Input handlers moved to Program.Input.cs

    private static void SwitchContext(string symbol, string interval)
    {
        _currentSymbol = symbol;
        _currentTimeframe = interval;
        Console.WriteLine($"[Interface] Switching to {symbol} {interval}...");
        _buffer.Clear();

        // Reset viewport and zoom state
        _zoom = 1.0f;
        _scrollOffset = 0;
        _autoScaleY = true;
        _viewMinY = 0;
        _viewMaxY = 0;

        // Clear all drawings when switching symbols
        _drawingState.Objects.Clear();
        _drawingState.CurrentDrawing = null;
        _drawingState.ActiveTool = Omnijure.Visual.Drawing.DrawingTool.None;

        // Update Title
        _window.Title = $"Omnijure - {symbol} [{interval}]";

        // Update dropdowns selected items (don't recreate UI)
        if (_assetDropdown != null)
        {
            _assetDropdown.SelectedItem = symbol;
        }
        if (_intervalDropdown != null)
        {
            _intervalDropdown.SelectedItem = interval;
        }
        if (_searchBox != null)
        {
            _searchBox.Placeholder = symbol;
        }

        _ = _binance.ConnectAsync(symbol, interval);
    }

    private static void OnScroll(IMouse arg1, ScrollWheel arg2)
    {
        // Search modal has highest priority
        if (_searchModal != null && _searchModal.IsVisible)
        {
            float delta = -arg2.Y * 2; // Scroll sensitivity
            int totalResults = _searchModal.GetTotalResultCount();
            if (totalResults > _searchModal.MaxVisibleResults)
            {
                _searchModal.ScrollOffset = Math.Max(0, Math.Min(totalResults - _searchModal.MaxVisibleResults, _searchModal.ScrollOffset + (int)delta));
            }
            return;
        }
        
        var openDd = _uiDropdowns.FirstOrDefault(d => d.IsOpen);
        if (openDd != null)
        {
            float delta = -arg2.Y; 
            var filtered = openDd.GetFilteredItems();
            openDd.ScrollOffset = Math.Max(0, Math.Min(filtered.Count - openDd.MaxVisibleItems, openDd.ScrollOffset + delta));
            return;
        }

        if (arg2.Y > 0) _zoom *= 1.1f;
        else _zoom *= 0.9f;
        _zoom = Math.Clamp(_zoom, 0.05f, 50.0f);
    }

    private static void OnMouseDown(IMouse arg1, MouseButton arg2) 
    { 
        if (arg2 == MouseButton.Left) 
        {
            // Check if modal is visible and handle clicks
            if (_searchModal != null && _searchModal.IsVisible)
            {
                // Calculate modal bounds
                float modalWidth = Math.Min(600, _window.Size.X - 80);
                float modalHeight = Math.Min(700, _window.Size.Y - 100);
                float modalX = (_window.Size.X - modalWidth) / 2;
                float modalY = (_window.Size.Y - modalHeight) / 2 - 50;
                
                // Check if click is inside modal
                if (_mousePos.X >= modalX && _mousePos.X <= modalX + modalWidth &&
                    _mousePos.Y >= modalY && _mousePos.Y <= modalY + modalHeight)
                {
                    // Calculate search box area
                    float searchBoxY = modalY + 84; 
                    if (_mousePos.Y >= searchBoxY && _mousePos.Y <= searchBoxY + 44)
                    {
                        // Clicked inside search box area
                        // If they click on the right side, maybe clear?
                        if (_mousePos.X > modalX + modalWidth - 60)
                        {
                            _searchModal.Clear();
                        }
                        return; // Consume
                    }

                    // Calculate tab click area
                    float tabStartY = modalY + 142; // y after search box spacer
                    if (_mousePos.Y >= tabStartY && _mousePos.Y <= tabStartY + 30)
                    {
                        float tabX = modalX + 24;
                        string[] categories = Enum.GetNames(typeof(AssetCategory));
                        using var font = new SKFont(SKTypeface.FromFamilyName("Segoe UI"), 11);
                        for (int i = 0; i < categories.Length; i++)
                        {
                            string catLabel = categories[i];
                            float labelWidth = font.MeasureText(catLabel) + 20;
                            if (_mousePos.X >= tabX && _mousePos.X <= tabX + labelWidth)
                            {
                                _searchModal.SelectedCategory = (AssetCategory)i;
                                _searchModal.UpdateFilteredResults();
                                return;
                            }
                            tabX += labelWidth + 10;
                        }
                        return; // Consume click in tab row area
                    }

                    // Calculate item click area (starts after all header elements)
                    float itemStartY = modalY + 142 + 38 + 18 + 18; // Updated for tabs
                    float itemHeight = 48;
                    
                    if (_mousePos.Y >= itemStartY)
                    {
                        int clickedIndex = (int)((_mousePos.Y - itemStartY) / (itemHeight + 2));
                        int globalIndex = _searchModal.ScrollOffset + clickedIndex;
                        
                        if (globalIndex >= 0 && globalIndex < _searchModal.GetTotalResultCount())
                        {
                            _searchModal.SelectedIndex = globalIndex;
                            var selected = _searchModal.GetSelectedSymbol();
                            if (!string.IsNullOrEmpty(selected))
                            {
                                SwitchContext(selected, _currentTimeframe);
                                _searchModal.IsVisible = false;
                                _searchModal.Clear();
                            }
                        }
                    }
                    return; // Consume click inside modal
                }
                else
                {
                    // Click outside modal - close it
                    _searchModal.IsVisible = false;
                    _searchModal.Clear();
                    return;
                }
            }
            
            // Check search box first - open modal instead of focusing
            if (_searchBox != null && _searchBox.Contains(_mousePos.X, _mousePos.Y))
            {
                if (_searchModal != null)
                {
                    _searchModal.IsVisible = true;
                    _searchModal.Clear();
                }
                return;
            }
            
            // Window control buttons (close, maximize, minimize, drag)
            if (_toolbar.HandleMouseDown(_mousePos.X, _mousePos.Y, _window.Position.X, _window.Position.Y))
                return;
            
            UiDropdown clickedDd = null;
            foreach(var dd in _uiDropdowns)
            {
                if (dd.IsOpen)
                {
                    var filtered = dd.GetFilteredItems();
                    for (int i = 0; i < filtered.Count; i++)
                    {
                        if (dd.ContainsItem(_mousePos.X, _mousePos.Y, i))
                        {
                            dd.SelectedItem = filtered[i];
                            dd.OnSelected?.Invoke(dd.SelectedItem);
                            dd.IsOpen = false;
                            dd.SearchQuery = "";
                            dd.ScrollOffset = 0;
                            return;
                        }
                    }
                }
                
                if (dd.Contains(_mousePos.X, _mousePos.Y))
                    clickedDd = dd;
            }

            if (clickedDd != null)
            {
                clickedDd.IsOpen = !clickedDd.IsOpen;
                if (clickedDd.IsOpen) { clickedDd.SearchQuery = ""; clickedDd.ScrollOffset = 0; }
                foreach(var other in _uiDropdowns) if (other != clickedDd) other.IsOpen = false;
                return;
            }

            // Close all if clicked away
            foreach(var dd in _uiDropdowns) 
            {
                if (dd.IsOpen) { dd.IsOpen = false; dd.SearchQuery = ""; dd.ScrollOffset = 0; }
            }

            foreach(var btn in _uiButtons)
            {
                if (btn.Contains(_mousePos.X, _mousePos.Y))
                {
                    btn.Action?.Invoke();
                    return;
                }
            }

            // Check for left toolbar (drawing tools) click
            var clickedTool = _layout.HandleToolbarClick(_mousePos.X, _mousePos.Y);
            if (clickedTool.HasValue)
            {
                _drawingState.ActiveTool = clickedTool.Value;
                return;
            }

            _layout.HandleMouseDown(_mousePos.X, _mousePos.Y);
            if (_layout.IsResizingLeft || _layout.IsResizingRight) return;

            // Handle drawing tool interaction on chart
            if (_layout.ChartRect.Contains(_mousePos.X, _mousePos.Y) && _drawingState.ActiveTool != Omnijure.Visual.Drawing.DrawingTool.None)
            {
                HandleDrawingToolClick();
                return;
            }

            if (_layout.ChartRect.Contains(_mousePos.X, _mousePos.Y) && _mousePos.X > _layout.ChartRect.Right - 70)
            {
                _isResizingPrice = true;
                _autoScaleY = false;
            }
            else if (_layout.ChartRect.Contains(_mousePos.X, _mousePos.Y))
            {
                _isDragging = true;
            }
        }
        else if (arg2 == MouseButton.Right)
        {
            // Cancel current drawing if any
            if (_drawingState.CurrentDrawing != null)
            {
                _drawingState.CurrentDrawing = null;
                _drawingState.ActiveTool = Omnijure.Visual.Drawing.DrawingTool.None;
            }
            else
            {
                _autoScaleY = true;
                _zoom = 1.0f;
                _scrollOffset = 0;
            }
            foreach(var dd in _uiDropdowns) { dd.IsOpen = false; dd.SearchQuery = ""; dd.ScrollOffset = 0; }
        }
    }
    
    private static void OnMouseUp(IMouse arg1, MouseButton arg2) 
    { 
        if (arg2 == MouseButton.Left) 
        {
            _isDragging = false; 
            _isResizingPrice = false;
            _toolbar.HandleMouseUp();
            _layout.HandleMouseUp();
        }
    }

    private static void OnMouseMove(IMouse arg1, System.Numerics.Vector2 pos)
    {
        float deltaX = pos.X - _lastMousePos.X; 
        float deltaY = pos.Y - _lastMousePos.Y;
        
        _mousePos = new Vector2D<float>(pos.X, pos.Y);
        
        // Disable UI hover if search modal is visible
        bool modalActive = _searchModal != null && (_searchModal.IsVisible || _searchModal.AnimationProgress > 0);
        
        // 0. UI Hover
        foreach(var dd in _uiDropdowns) 
            dd.IsHovered = !modalActive && dd.Contains(_mousePos.X, _mousePos.Y);
            
        foreach(var btn in _uiButtons) 
            btn.IsHovered = !modalActive && btn.Contains(_mousePos.X, _mousePos.Y);

        if (modalActive)
        {
             _lastMousePos = _mousePos;
             return;
        }

        // Window drag
        if (_toolbar.HandleMouseMove())
        {
            _lastMousePos = _mousePos;
            return;
        }

        // Manejar movimiento de paneles (drag & drop)
        _layout.HandleMouseMove(pos.X, pos.Y, deltaX, _window.Size.X, _window.Size.Y);
        
        // Si está arrastrando o redimensionando panel, bloquear otras interacciones
        if (_layout.IsDraggingPanel || _layout.IsResizingPanel)
        {
            _lastMousePos = _mousePos;
            return;
        }
        
        // If a dropdown is open, block chart dragging
        if (_uiDropdowns.Any(d => d.IsOpen))
        {
             _lastMousePos = _mousePos;
             return;
        }

        if (_isResizingPrice)
        {
            float sensitivity = 0.005f;
            float factor = 1.0f + (deltaY * sensitivity);
            
            float mid = (_viewMinY + _viewMaxY) / 2.0f;
            float range = (_viewMaxY - _viewMinY);
            float newRange = range * factor;
            
            if (newRange < 0.00001f) newRange = 0.00001f;
            
            _viewMinY = mid - newRange / 2.0f;
            _viewMaxY = mid + newRange / 2.0f;
        }
        else if (_drawingState.CurrentDrawing != null && _layout.ChartRect.Contains(_mousePos.X, _mousePos.Y))
        {
            // Update current drawing (e.g., trend line endpoint) as mouse moves
            float chartLocalX = _mousePos.X - _layout.ChartRect.Left;
            float chartLocalY = _mousePos.Y - _layout.ChartRect.Top;

            const int RightAxisWidth = 60;
            const int BottomAxisHeight = 30;
            const int VolumeHeight = 80;
            int chartW = (int)_layout.ChartRect.Width - RightAxisWidth;
            int mainChartH = (int)_layout.ChartRect.Height - BottomAxisHeight - VolumeHeight;

            if (chartLocalX >= 0 && chartLocalX <= chartW && chartLocalY >= 0 && chartLocalY <= mainChartH)
            {
                float baseCandleWidth = 8.0f;
                float candleWidth = baseCandleWidth * _zoom;
                if (candleWidth < 1.0f) candleWidth = 1.0f;
                int visibleCandles = (int)System.Math.Ceiling(chartW / candleWidth);
                if (visibleCandles < 2) visibleCandles = 2;

                int screenIndex = (int)((visibleCandles - 1) - (chartLocalX - candleWidth / 2) / candleWidth);
                int candleIndex = screenIndex + _scrollOffset;

                float normalized = (mainChartH - chartLocalY) / mainChartH;
                float price = _viewMinY + (normalized * (_viewMaxY - _viewMinY));

                if (_drawingState.CurrentDrawing is Omnijure.Visual.Drawing.TrendLineObject trendLine)
                {
                    trendLine.End = (candleIndex, price);
                }
            }
        }
        else if (_isDragging)
        {
            // Horizontal Pan (TradingView-style: allow free scrolling into future)
            _scrollOffset += (int)(deltaX * 0.1f * (_zoom < 1 ? 1 : 1/_zoom));
            
            // Vertical Pan
            if (System.Math.Abs(deltaY) > 0.5f)
            {
                _autoScaleY = false;
                // Map pixel delta to price
                // We need current range to know scale
                float range = _viewMaxY - _viewMinY;
                float pxHeight = _window.Size.Y;
                if (pxHeight > 0)
                {
                    float pricePerPx = range / pxHeight;
                    float priceDelta = deltaY * pricePerPx;
                    
                    _viewMinY += priceDelta;
                    _viewMaxY += priceDelta;
                }
            }
        }
        _lastMousePos = new Vector2D<float>(pos.X, pos.Y);
        
        // Hover effects
        foreach(var btn in _uiButtons) btn.IsHovered = btn.Contains(pos.X, pos.Y);
    }

    private static void HandleDrawingToolClick()
    {
        // Convert screen coordinates to chart-local coordinates
        float chartLocalX = _mousePos.X - _layout.ChartRect.Left;
        float chartLocalY = _mousePos.Y - _layout.ChartRect.Top;

        // Chart dimensions (matching ChartRenderer's layout)
        const int RightAxisWidth = 60;
        const int BottomAxisHeight = 30;
        const int VolumeHeight = 80;
        int chartW = (int)_layout.ChartRect.Width - RightAxisWidth;
        int mainChartH = (int)_layout.ChartRect.Height - BottomAxisHeight - VolumeHeight;

        // Only handle clicks in the main chart area (not volume panel or axes)
        if (chartLocalX < 0 || chartLocalX > chartW || chartLocalY < 0 || chartLocalY > mainChartH)
            return;

        // Calculate candle width and visible candles (matching ChartRenderer logic)
        float baseCandleWidth = 8.0f;
        float candleWidth = baseCandleWidth * _zoom;
        if (candleWidth < 1.0f) candleWidth = 1.0f;
        int visibleCandles = (int)System.Math.Ceiling(chartW / candleWidth);
        if (visibleCandles < 2) visibleCandles = 2;

        // Convert screen X to candle index
        int screenIndex = (int)((visibleCandles - 1) - (chartLocalX - candleWidth / 2) / candleWidth);
        int candleIndex = screenIndex + _scrollOffset;

        // Convert screen Y to price
        float normalized = (mainChartH - chartLocalY) / mainChartH;
        float price = _viewMinY + (normalized * (_viewMaxY - _viewMinY));

        // Handle different drawing tools
        switch (_drawingState.ActiveTool)
        {
            case Omnijure.Visual.Drawing.DrawingTool.HorizontalLine:
                // Single click creates horizontal line
                var hLine = new Omnijure.Visual.Drawing.HorizontalLineObject(price)
                {
                    Label = null
                };
                _drawingState.Objects.Add(hLine);
                _drawingState.ActiveTool = Omnijure.Visual.Drawing.DrawingTool.None; // Return to cursor
                break;

            case Omnijure.Visual.Drawing.DrawingTool.TrendLine:
                // Two-click process: start and end
                if (_drawingState.CurrentDrawing == null)
                {
                    // First click - create new trend line
                    var tLine = new Omnijure.Visual.Drawing.TrendLineObject
                    {
                        Start = (candleIndex, price),
                        End = (candleIndex, price) // Will be updated on second click
                    };
                    _drawingState.CurrentDrawing = tLine;
                }
                else if (_drawingState.CurrentDrawing is Omnijure.Visual.Drawing.TrendLineObject trendLine)
                {
                    // Second click - finalize trend line
                    trendLine.End = (candleIndex, price);
                    _drawingState.Objects.Add(trendLine);
                    _drawingState.CurrentDrawing = null;
                    _drawingState.ActiveTool = Omnijure.Visual.Drawing.DrawingTool.None; // Return to cursor
                }
                break;

            case Omnijure.Visual.Drawing.DrawingTool.None:
            default:
                // No active tool - could implement selection here
                break;
        }
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


        // 3. LAYOUT & VIEWPORT
        _layout.UpdateLayout(_window.Size.X, _window.Size.Y);
        _layout.UpdateChartTitle(_currentSymbol, _currentTimeframe, currentPrice);
        
        // Calculate visible candles based on actual chart width (subtracting right axis margin)
        float chartWidth = _layout.ChartRect.Width - 60; // Matches ChartRenderer.RightAxisWidth
        float baseCandleWidth = 8.0f;
        float candleWidth = baseCandleWidth * _zoom;
        if (candleWidth < 1.0f) candleWidth = 1.0f;
        int visibleCandles = (int)Math.Ceiling(chartWidth / candleWidth);
        if (visibleCandles < 2) visibleCandles = 2; 

        // Limit scrolling past historical data
        if (_scrollOffset > _buffer.Count - visibleCandles)
            _scrollOffset = _buffer.Count - visibleCandles;

        // TradingView-style: Allow unlimited scrolling into future (up to 10 screens worth)
        int minScroll = -(visibleCandles * 10);
        if (_scrollOffset < minScroll) _scrollOffset = minScroll;

        // Calculate Auto-Min/Max
        float calcMax = float.MinValue;
        float calcMin = float.MaxValue;
        
        if (_buffer.Count > 0)
        {
            for (int i = 0; i < visibleCandles; i++)
            {
                int idx = i + _scrollOffset;
                if (idx < 0) continue; // Future
                if (idx >= _buffer.Count) break;
                
                ref var c = ref _buffer[idx];
                if (c.High > calcMax) calcMax = c.High;
                if (c.Low < calcMin) calcMin = c.Low;
            }
        } else {
            calcMax = 100; calcMin = 0;
        }
        
        if (calcMax <= calcMin) { calcMax = calcMin + 1; }

        // Apply Viewport Logic
        if (_autoScaleY)
        {
            _viewMinY = calcMin;
            _viewMaxY = calcMax;
        }

        // Future Scrolling: Allow going into negative index on the left (which means future on the right)
        // ScrollOffset=0 is Latest Candle at Right Edge.
        
        // Pass to Layout
        _layout.Render(_surface.Canvas, _renderer, _buffer, decision, _scrollOffset, _zoom, _currentSymbol, _currentTimeframe, _chartType, _uiButtons, _viewMinY, _viewMaxY, _mousePos, _orderBook, _trades, _drawingState, _window.Size.X, _window.Size.Y);
        
        // Render Toolbar (Top)
        _toolbar.UpdateMousePos(_mousePos.X, _mousePos.Y);
        _toolbar.Render(_surface.Canvas, _layout.HeaderRect, _searchBox, _assetDropdown, _uiDropdowns, _uiButtons);
        
        // Render Search Modal (if visible or animating)
        if (_searchModal != null)
        {
            // Smooth animation
            if (_searchModal.IsVisible && _searchModal.AnimationProgress < 1)
            {
                _searchModal.AnimationProgress = Math.Min(1, _searchModal.AnimationProgress + 0.15f);
            }
            else if (!_searchModal.IsVisible && _searchModal.AnimationProgress > 0)
            {
                _searchModal.AnimationProgress = Math.Max(0, _searchModal.AnimationProgress - 0.15f);
            }
            
            if (_searchModal.AnimationProgress > 0)
            {
                _searchModalRenderer.Render(_surface.Canvas, _window.Size.X, _window.Size.Y, _searchModal);
            }
        }
        
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
