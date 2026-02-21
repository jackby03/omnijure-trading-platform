using Omnijure.Core.DataStructures;
using Omnijure.Core.Events;

namespace Omnijure.Core.Network;

public interface IExchangeClientFactory
{
    IExchangeClient Create(string clientId, RingBuffer<Candle> buffer, OrderBook orderBook, RingBuffer<MarketTrade> trades);
}

public class BinanceClientFactory : IExchangeClientFactory
{
    private readonly IEventBus _eventBus;

    public BinanceClientFactory(IEventBus eventBus)
    {
        _eventBus = eventBus;
    }

    public IExchangeClient Create(string clientId, RingBuffer<Candle> buffer, OrderBook orderBook, RingBuffer<MarketTrade> trades)
    {
        return new BinanceClient(clientId, _eventBus, buffer, orderBook, trades);
    }
}
