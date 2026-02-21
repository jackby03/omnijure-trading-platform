using System.Threading;
using System.Threading.Tasks;

namespace Omnijure.Core.Entities.Exchange;

public interface IExchangeClient
{
    Task ConnectAsync(string symbol = "BTCUSDT", string interval = "1m");
    Task DisconnectAsync();
}
