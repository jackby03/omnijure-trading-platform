
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

public static class Program
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
        _layout = new LayoutManager();
        _toolbar = new ToolbarRenderer();
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

        // 0. Search Box
        _searchBox = new UiSearchBox(0, 0, 250, 34);
        
        // 0.5. Search Modal
        _searchModal = new UiSearchModal();

        // 1. Asset Dropdown
        var assets = new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT", "XRPUSDT" };
        _assetDropdown = new UiDropdown(10, 5, 180, 30, "Asset", assets, (s) => SwitchContext(s, _currentTimeframe));
        _uiDropdowns.Add(_assetDropdown);

        // Fetch full list in background
        Task.Run(async () => {
            var fullList = await Omnijure.Core.Network.BinanceService.GetAllUsdtSymbolsAsync();
            if (fullList != null && fullList.Count > 0)
            {
                _assetDropdown.Items = fullList;
                _searchModal?.SetSymbols(fullList);
            }
        });

        // 2. Interval Dropdown
        var intervals = new List<string> { "1m", "5m", "15m", "1h", "4h", "1d" };
        _intervalDropdown = new UiDropdown(200, 5, 100, 30, "Interval", intervals, (tf) => SwitchContext(_currentSymbol, tf));
        _uiDropdowns.Add(_intervalDropdown);

        // 3. Chart Type Dropdown
        var chartTypes = new List<string> { "Candles", "Line", "Area" };
        _chartTypeDropdown = new UiDropdown(310, 5, 100, 30, "Chart", chartTypes, (type) => {
            if (type == "Candles") _chartType = ChartType.Candles;
            else if (type == "Line") _chartType = ChartType.Line;
            else if (type == "Area") _chartType = ChartType.Area;
        });
        _uiDropdowns.Add(_chartTypeDropdown);
    }

    private static void OnResize(Vector2D<int> size)
    {
        // Update Viewport
        _gl?.Viewport(size);

        CreateSurface(size);
    }

    private static void OnKeyChar(IKeyboard arg1, char arg2)
    {
        // Search modal has highest priority
        if (_searchModal != null && _searchModal.IsVisible)
        {
            // Stop processing if it's a control character we don't handle here
            if (char.IsControl(arg2)) return;
            
            _searchModal.AddChar(arg2);
            return;
        }
        
        // Search box has priority
        if (_searchBox != null && _searchBox.IsFocused)
        {
            if (char.IsLetterOrDigit(arg2) || char.IsWhiteSpace(arg2))
            {
                _searchBox.AddChar(arg2);
                // Filter asset dropdown based on search
                if (_assetDropdown != null)
                {
                    _assetDropdown.SearchQuery = _searchBox.Text;
                }
            }
            return;
        }
        
        // Fallback to dropdown search
        var openDd = _uiDropdowns.FirstOrDefault(d => d.IsOpen);
        if (openDd != null)
        {
            openDd.SearchQuery += arg2;
        }
    }

    private static void OnKeyDown(IKeyboard arg1, Key arg2, int arg3)
    {
        // Ctrl+K to open search modal
        if (arg1.IsKeyPressed(Key.ControlLeft) && arg2 == Key.K)
        {
            if (_searchModal != null)
            {
                _searchModal.IsVisible = true;
                _searchModal.Clear();
            }
            return;
        }
        
        // Search modal has highest priority when visible
        if (_searchModal != null && _searchModal.IsVisible)
        {
            if (arg2 == Key.Escape)
            {
                _searchModal.IsVisible = false;
                _searchModal.Clear();
            }
            else if (arg2 == Key.Backspace)
            {
                _searchModal.Backspace();
            }
            else if (arg2 == Key.Up)
            {
                _searchModal.MoveSelectionUp();
            }
            else if (arg2 == Key.Down)
            {
                _searchModal.MoveSelectionDown();
            }
            else if (arg2 == Key.Enter)
            {
                var selected = _searchModal.GetSelectedSymbol();
                if (!string.IsNullOrEmpty(selected))
                {
                    SwitchContext(selected, _currentTimeframe);
                    _searchModal.IsVisible = false;
                    _searchModal.Clear();
                }
            }
            return;
        }
        
        // Search box has priority
        if (_searchBox != null && _searchBox.IsFocused)
        {
            if (arg2 == Key.Backspace)
            {
                _searchBox.Backspace();
                if (_assetDropdown != null) _assetDropdown.SearchQuery = _searchBox.Text;
            }
            else if (arg2 == Key.Escape)
            {
                _searchBox.IsFocused = false;
                _searchBox.Clear();
                if (_assetDropdown != null) _assetDropdown.SearchQuery = "";
            }
            else if (arg2 == Key.Enter && _assetDropdown != null)
            {
                var filtered = _assetDropdown.GetFilteredItems();
                if (filtered.Count > 0)
                {
                    SwitchContext(filtered[0], _currentTimeframe);
                    _searchBox.Clear();
                    _searchBox.IsFocused = false;
                    if (_assetDropdown != null) _assetDropdown.SearchQuery = "";
                }
            }
            return;
        }
        
        var openDd = _uiDropdowns.FirstOrDefault(d => d.IsOpen);
        if (openDd != null)
        {
            if (arg2 == Key.Escape) openDd.IsOpen = false;
            else if (arg2 == Key.Backspace && openDd.SearchQuery.Length > 0)
            {
                openDd.SearchQuery = openDd.SearchQuery[..^1];
            }
            return;
        }

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

        // Reset viewport and zoom state
        _zoom = 1.0f;
        _scrollOffset = 0;
        _autoScaleY = true;
        _viewMinY = 0;
        _viewMaxY = 0;

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

            _layout.HandleMouseDown(_mousePos.X, _mousePos.Y);
            if (_layout.IsResizingLeft || _layout.IsResizingRight) return;

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
            _autoScaleY = true;
            _zoom = 1.0f;
            _scrollOffset = 0;
            foreach(var dd in _uiDropdowns) { dd.IsOpen = false; dd.SearchQuery = ""; dd.ScrollOffset = 0; }
        }
    }
    
    private static void OnMouseUp(IMouse arg1, MouseButton arg2) 
    { 
        if (arg2 == MouseButton.Left) 
        {
            _isDragging = false; 
            _isResizingPrice = false;
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
             return; // Block all other mouse move logic (crosshair etc) when modal is active
        }

        if (_layout.IsResizingLeft || _layout.IsResizingRight)
        {
            _layout.HandleMouseMove(pos.X, pos.Y, deltaX);
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
        else if (_isDragging)
        {
            // Horizontal Pan
            _scrollOffset += (int)(deltaX * 0.1f * (_zoom < 1 ? 1 : 1/_zoom)); 
            if (_scrollOffset < 0) _scrollOffset = 0;
            
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
        
        // Calculate visible candles based on actual chart width (subtracting right axis margin)
        float chartWidth = _layout.ChartRect.Width - 60; // Matches ChartRenderer.RightAxisWidth
        float baseCandleWidth = 8.0f;
        float candleWidth = baseCandleWidth * _zoom;
        if (candleWidth < 1.0f) candleWidth = 1.0f;
        int visibleCandles = (int)Math.Ceiling(chartWidth / candleWidth);
        if (visibleCandles < 2) visibleCandles = 2; 

        if (_scrollOffset > _buffer.Count - visibleCandles) _scrollOffset = _buffer.Count - visibleCandles;
        
        // Allow up to 50% screen width of future space
        int minScroll = -(visibleCandles / 2);
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
        _layout.Render(_surface.Canvas, _renderer, _buffer, decision, _scrollOffset, _zoom, _currentSymbol, _currentTimeframe, _chartType, _uiButtons, _viewMinY, _viewMaxY, _mousePos, _orderBook, _trades);
        
        // Render Toolbar (Top)
        _toolbar.UpdateMousePos(_mousePos.X, _mousePos.Y);
        _toolbar.Render(_surface.Canvas, _layout.HeaderRect, _searchBox, _uiDropdowns, _uiButtons);
        
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
