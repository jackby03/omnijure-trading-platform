using Omnijure.Core.DataStructures;

namespace Omnijure.Core.Network;

public interface IExchangeClientFactory
{
    IExchangeClient Create(RingBuffer<Candle> buffer, OrderBook orderBook, RingBuffer<MarketTrade> trades);
}

public class BinanceClientFactory : IExchangeClientFactory
{
    public IExchangeClient Create(RingBuffer<Candle> buffer, OrderBook orderBook, RingBuffer<MarketTrade> trades)
    {
        return new BinanceClient(buffer, orderBook, trades);
    }
}
