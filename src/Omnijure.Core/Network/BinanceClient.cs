
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
    private readonly OrderBook _orderBook;
    private CancellationTokenSource _cts;

    public BinanceClient(RingBuffer<Candle> buffer, OrderBook orderBook)
    {
        _buffer = buffer;
        _orderBook = orderBook;
    }
    public async Task ConnectAsync(string symbol = "BTCUSDT", string interval = "1m")
    {
        await DisconnectAsync();
        
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
            // Use combined streams for Klines + Depth
            // wss://stream.binance.com:9443/stream?streams=<streamName1>/<streamName2>
            string klineStream = $"{symbol.ToLower()}@kline_{interval}";
            string depthStream = $"{symbol.ToLower()}@depth20@100ms";
            
            string fullUrl = $"wss://stream.binance.com:9443/stream?streams={klineStream}/{depthStream}";
            
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

    private async Task FetchHistoryAsync(string symbol, string interval)
    {
        // https://api.binance.com/api/v3/klines?symbol=BTCUSDT&interval=1m&limit=500
        using var client = new System.Net.Http.HttpClient();
        string url = $"https://api.binance.com/api/v3/klines?symbol={symbol}&interval={interval}&limit=500";
        string json = await client.GetStringAsync(url);
        
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        
        foreach (var kline in root.EnumerateArray())
        {
            // ... (Parsing same)
            long timestamp = kline[0].GetInt64();
            float open = float.Parse(kline[1].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float high = float.Parse(kline[2].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float low = float.Parse(kline[3].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float close = float.Parse(kline[4].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            float vol = float.Parse(kline[5].GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
            
            var candle = new Candle
            {
                Open = open, Close = close, High = high, Low = low, Volume = vol, Timestamp = timestamp
            };
            
            _buffer.Push(candle);
        }
        Console.WriteLine($"[Metal] Backfilled {root.GetArrayLength()} candles for {symbol}.");
    }

    public async Task DisconnectAsync()
    {
        if (_socket != null)
        {
            try 
            {
                _cts?.Cancel();
                if (_socket.State == WebSocketState.Open)
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Switching Interval", CancellationToken.None);
                _socket.Dispose();
            }
            catch {}
            finally { _socket = null; }
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
            }
            else
            {
                // Single stream fallback
                if (root.TryGetProperty("e", out var e) && e.GetString() == "kline")
                {
                    ParseKline(root);
                }
            }
        }
        catch 
        {
            // Ignore
        }
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
