using Omnijure.Core.Features.Settings.Api;
using Omnijure.Core.Features.Settings.Model;
using Omnijure.Core.Shared.Infrastructure.EventBus;

namespace Omnijure.Core.Entities.Exchange;

public interface IExchangeClientFactory
{
    /// <summary>
    /// Creates an exchange client for the given tab. The concrete type (live vs paper)
    /// is determined by <see cref="TradingMode"/> in <see cref="ISettingsProvider"/>.
    /// </summary>
    IExchangeClient Create(string clientId, RingBuffer<Candle> buffer, OrderBook orderBook, RingBuffer<MarketTrade> trades);
}

public class BinanceClientFactory : IExchangeClientFactory
{
    private readonly IEventBus _eventBus;
    private readonly ISettingsProvider _settings;

    public BinanceClientFactory(IEventBus eventBus, ISettingsProvider settings)
    {
        _eventBus = eventBus;
        _settings = settings;
    }

    public IExchangeClient Create(string clientId, RingBuffer<Candle> buffer, OrderBook orderBook, RingBuffer<MarketTrade> trades)
    {
        // Route to the correct implementation based on the user's chosen trading mode.
        // PaperTradingEngine will be implemented in issue #7 — for now Live and Paper
        // both produce a BinanceClient (read-only, no order placement yet).
        return _settings.Current.General.TradingMode switch
        {
            TradingMode.Paper => new BinanceClient(clientId, _eventBus, buffer, orderBook, trades),
            TradingMode.Live  => new BinanceClient(clientId, _eventBus, buffer, orderBook, trades),
            _                 => new BinanceClient(clientId, _eventBus, buffer, orderBook, trades),
        };
    }
}
