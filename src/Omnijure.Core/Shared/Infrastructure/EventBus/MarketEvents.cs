namespace Omnijure.Core.Shared.Infrastructure.EventBus;

public class ContextChangedEvent
{
    public string TargetId { get; }
    public string NewSymbol { get; }
    public string NewInterval { get; }

    public ContextChangedEvent(string targetId, string newSymbol, string newInterval)
    {
        TargetId = targetId;
        NewSymbol = newSymbol;
        NewInterval = newInterval;
    }
}

public class TabSwitchedEvent
{
    public string TabId { get; }
    public string Symbol { get; }
    public string Timeframe { get; }

    public TabSwitchedEvent(string tabId, string symbol, string timeframe)
    {
        TabId = tabId;
        Symbol = symbol;
        Timeframe = timeframe;
    }
}
