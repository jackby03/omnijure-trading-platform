using System.Threading;
using System.Threading.Tasks;

namespace Omnijure.Core.Network;

public interface IExchangeClient
{
    Task ConnectAsync(string symbol = "BTCUSDT", string interval = "1m");
    Task DisconnectAsync();
}
