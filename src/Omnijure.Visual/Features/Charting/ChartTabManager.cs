using Omnijure.Core.Features.Settings.Api;
using Omnijure.Core.Features.Settings.Model;
using Omnijure.Visual.Rendering;

namespace Omnijure.Visual;

public class ChartTabManager
{
    private readonly List<ChartTabState> _tabs = new();
    private int _activeIndex;
    private readonly Omnijure.Core.Entities.Exchange.IExchangeClientFactory _exchangeFactory;

    public ChartTabManager(Omnijure.Core.Entities.Exchange.IExchangeClientFactory exchangeFactory)
    {
        _exchangeFactory = exchangeFactory;
    }

    public const int TabBarHeight = 28;

    public ChartTabState ActiveTab => _tabs.Count > 0 ? _tabs[_activeIndex] : null!;
    public IReadOnlyList<ChartTabState> Tabs => _tabs;
    public int ActiveIndex => _activeIndex;
    public int Count => _tabs.Count;

    public ChartTabState AddTab(string symbol = "BTCUSDT", string timeframe = "1m")
    {
        var tab = new ChartTabState(symbol, timeframe, _exchangeFactory);
        _tabs.Add(tab);
        _activeIndex = _tabs.Count - 1;
        _ = tab.Connection.ConnectAsync(symbol, timeframe);
        return tab;
    }

    public void CloseTab(int index)
    {
        if (_tabs.Count <= 1) return; // Always keep at least 1 tab
        if (index < 0 || index >= _tabs.Count) return;

        var tab = _tabs[index];
        _ = tab.Connection.DisconnectAsync();
        _tabs.RemoveAt(index);

        // Adjust active index
        if (_activeIndex >= _tabs.Count)
            _activeIndex = _tabs.Count - 1;
        else if (_activeIndex > index)
            _activeIndex--;
    }

    public void SwitchTo(int index)
    {
        if (index < 0 || index >= _tabs.Count) return;
        _activeIndex = index;
    }

    /// <summary>
    /// Changes the symbol/timeframe of the active tab (equivalent to old SwitchContext).
    /// </summary>
    public void SwitchContext(string symbol, string timeframe)
    {
        var tab = ActiveTab;
        if (tab == null) return;

        tab.Symbol = symbol;
        tab.Timeframe = timeframe;
        tab.Buffer.Clear();

        // Reset viewport
        tab.Zoom = 1.0f;
        tab.ScrollOffset = 0;
        tab.AutoScaleY = true;
        tab.ViewMinY = 0;
        tab.ViewMaxY = 0;

        // Clear drawings
        tab.DrawingState.Objects.Clear();
        tab.DrawingState.CurrentDrawing = null;
        tab.DrawingState.ActiveTool = Omnijure.Visual.Shared.Lib.Drawing.DrawingTool.None;
    }

    /// <summary>
    /// Disconnects all WebSocket connections (for app shutdown).
    /// </summary>
    public void DisconnectAll()
    {
        foreach (var tab in _tabs)
        {
            _ = tab.Connection.DisconnectAsync();
        }
    }

    /// <summary>
    /// Export tab states for settings persistence.
    /// </summary>
    public List<ChartTabSaved> ExportTabStates()
    {
        var result = new List<ChartTabSaved>();
        foreach (var tab in _tabs)
        {
            result.Add(new ChartTabSaved
            {
                Symbol = tab.Symbol,
                Timeframe = tab.Timeframe,
                ChartType = tab.ChartType.ToString(),
                Zoom = tab.Zoom
            });
        }
        return result;
    }
}
