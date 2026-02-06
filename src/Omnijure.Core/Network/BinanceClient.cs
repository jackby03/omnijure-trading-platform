
using System;
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;
using Omnijure.Core.DataStructures;

namespace Omnijure.Core.Network;

public class BinanceClient
{
    private string _currentInterval = "1m";
    private readonly string _baseUrl = "wss://stream.binance.com:9443/ws/";
    private ClientWebSocket _socket;
    private readonly RingBuffer<Candle> _buffer;
    private readonly RingBuffer<MarketTrade> _trades;
    private readonly OrderBook _orderBook;
    private CancellationTokenSource _cts;
    private readonly SemaphoreSlim _connectionLock = new SemaphoreSlim(1, 1);

    public BinanceClient(RingBuffer<Candle> buffer, OrderBook orderBook, RingBuffer<MarketTrade> trades)
    {
        _buffer = buffer;
        _orderBook = orderBook;
        _trades = trades;
    }
    public async Task ConnectAsync(string symbol = "BTCUSDT", string interval = "1m")
    {
        await _connectionLock.WaitAsync();
        try 
        {
            await DisconnectAsyncInternal();
            
            // Normalize
            symbol = symbol.ToUpper();
            _currentInterval = interval;
            
            // 1. Backfill History (REST)
            try 
            {
                Console.WriteLine($"[Metal] Fetching History for {symbol} {interval}...");
                await FetchHistoryAsync(symbol, interval);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Metal] History Fetch Failed: {ex.Message}");
            }

            // 2. Connect Realtime Stream
            _cts = new CancellationTokenSource();
            _socket = new ClientWebSocket();
            
            try 
            {
                // Use combined streams for Klines + Depth + Trades
                string klineStream = $"{symbol.ToLower()}@kline_{interval}";
                string depthStream = $"{symbol.ToLower()}@depth20@100ms";
                string tradeStream = $"{symbol.ToLower()}@trade";
                
                string fullUrl = $"wss://stream.binance.com:9443/stream?streams={klineStream}/{depthStream}/{tradeStream}";
                
                Console.WriteLine($"[Metal] Connecting to Combined Stream ({fullUrl})...");
                await _socket.ConnectAsync(new Uri(fullUrl), CancellationToken.None);
                Console.WriteLine($"[Metal] Connected to Combined Streams for {symbol}!");
                
                _ = ReceiveLoop();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Metal] Connection Failed: {ex.Message}");
            }
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task FetchHistoryAsync(string symbol, string interval)
    {
        using var client = new System.Net.Http.HttpClient();
        string url = $"https://api.binance.com/api/v3/klines?symbol={symbol}&interval={interval}&limit=500";
        string json = await client.GetStringAsync(url);
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        foreach (var kline in root.EnumerateArray())
        {
            long timestamp = kline[0].GetInt64();
            float open = float.Parse(kline[1].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float high = float.Parse(kline[2].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float low = float.Parse(kline[3].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float close = float.Parse(kline[4].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float vol = float.Parse(kline[5].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            
            var candle = new Candle { Open = open, Close = close, High = high, Low = low, Volume = vol, Timestamp = timestamp };
            _buffer.Push(candle);
        }
        Console.WriteLine($"[Metal] Backfilled {root.GetArrayLength()} candles for {symbol}.");
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await DisconnectAsyncInternal();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private async Task DisconnectAsyncInternal()
    {
        if (_socket != null)
        {
            try 
            {
                _cts?.Cancel();
                if (_socket.State == WebSocketState.Open || _socket.State == WebSocketState.CloseReceived)
                {
                    using var timeoutCts = new CancellationTokenSource(2000);
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Switching", timeoutCts.Token);
                }
            }
            catch {}
            finally 
            {
                _socket.Dispose();
                _socket = null; 
                _cts?.Dispose();
                _cts = null;
            }
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];
        
        while (_socket != null && _socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            try 
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;

                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ParseAndUpdate(json);
            }
            catch 
            {
                break;
            }
        }
    }

    private void ParseAndUpdate(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            // Combined stream format: {"stream": "...", "data": {...}}
            if (root.TryGetProperty("data", out var data))
            {
                string stream = root.GetProperty("stream").GetString() ?? "";
                
                if (stream.Contains("@kline"))
                {
                    ParseKline(data);
                }
                else if (stream.Contains("@depth"))
                {
                    ParseDepth(data);
                }
                else if (stream.Contains("@trade"))
                {
                    ParseTrade(data);
                }
            }
            else
            {
                // Single stream fallback
                if (root.TryGetProperty("e", out var e))
                {
                    string type = e.GetString() ?? "";
                    if (type == "kline") ParseKline(root);
                    else if (type == "trade") ParseTrade(root);
                }
            }
        }
        catch 
        {
            // Ignore
        }
    }

    private void ParseTrade(JsonElement data)
    {
        // Format for @trade: {"e":"trade","E":...,"s":"BTCUSDT","t":...,"p":"...","q":"...","b":...,"a":...,"T":...,"m":true,"M":true}
        // "p": price, "q": quantity, "T": trade time, "m": is buyer maker
        float price = float.Parse(data.GetProperty("p").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        float qty = float.Parse(data.GetProperty("q").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
        long time = data.GetProperty("T").GetInt64();
        bool isBuyerMaker = data.GetProperty("m").GetBoolean();

        _trades.Push(new MarketTrade {
            Price = price,
            Quantity = qty,
            Timestamp = time,
            IsBuyerMaker = isBuyerMaker
        });
    }

    private void ParseKline(JsonElement kline)
    {
        if (kline.TryGetProperty("k", out var k))
        {
            long startTime = k.GetProperty("t").GetInt64();
            float open = float.Parse(k.GetProperty("o").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float close = float.Parse(k.GetProperty("c").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float high = float.Parse(k.GetProperty("h").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float low = float.Parse(k.GetProperty("l").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float vol = float.Parse(k.GetProperty("v").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            
            var candle = new Candle
            {
                Open = open, Close = close, High = high, Low = low, Volume = vol, Timestamp = startTime
            };

            if (_buffer.Count > 0)
            {
                ref var last = ref _buffer[0];
                if (last.Timestamp == startTime) last = candle;
                else if (startTime > last.Timestamp) _buffer.Push(candle);
            }
            else
            {
                _buffer.Push(candle);
            }
        }
    }

    private void ParseDepth(JsonElement depth)
    {
        // Format for @depth20: {"lastUpdateId":..., "bids": [["price","qty"],...], "asks": [...]}
        var bids = new List<OrderBookEntry>();
        var asks = new List<OrderBookEntry>();

        if (depth.TryGetProperty("bids", out var bList))
        {
            foreach (var b in bList.EnumerateArray())
            {
                bids.Add(new OrderBookEntry {
                    Price = float.Parse(b[0].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    Quantity = float.Parse(b[1].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture)
                });
            }
        }

        if (depth.TryGetProperty("asks", out var aList))
        {
            foreach (var a in aList.EnumerateArray())
            {
                asks.Add(new OrderBookEntry {
                    Price = float.Parse(a[0].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture),
                    Quantity = float.Parse(a[1].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture)
                });
            }
        }

        _orderBook.Update(bids, asks);
    }
}
