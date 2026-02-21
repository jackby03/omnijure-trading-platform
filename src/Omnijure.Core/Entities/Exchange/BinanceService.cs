
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using System.Linq;

namespace Omnijure.Core.Entities.Exchange;

public class BinanceService
{
    private static readonly HttpClient _httpClient = new HttpClient();

    public static async Task<List<string>> GetAllUsdtSymbolsAsync()
    {
        try
        {
            var response = await _httpClient.GetStringAsync("https://api.binance.com/api/v3/exchangeInfo");
            using var doc = JsonDocument.Parse(response);
            var symbols = doc.RootElement.GetProperty("symbols");
            
            var list = new List<string>();
            foreach (var s in symbols.EnumerateArray())
            {
                string status = s.GetProperty("status").GetString();
                string quote = s.GetProperty("quoteAsset").GetString();
                string symbol = s.GetProperty("symbol").GetString();
                
                if (status == "TRADING" && quote == "USDT")
                {
                    list.Add(symbol);
                }
            }
            return list.OrderBy(x => x).ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Service] Failed to fetch symbols: {ex.Message}");
            return new List<string> { "BTCUSDT", "ETHUSDT", "SOLUSDT", "BNBUSDT", "XRPUSDT" };
        }
    }
}
