namespace Omnijure.Core.Events;

public class SymbolChangedEvent
{
    public string TargetId { get; }
    public string NewSymbol { get; }
    
    public SymbolChangedEvent(string targetId, string newSymbol)
    {
        TargetId = targetId;
        NewSymbol = newSymbol;
    }
}

public class IntervalChangedEvent
{
    public string TargetId { get; }
    public string NewInterval { get; }
    
    public IntervalChangedEvent(string targetId, string newInterval)
    {
        TargetId = targetId;
        NewInterval = newInterval;
    }
}
