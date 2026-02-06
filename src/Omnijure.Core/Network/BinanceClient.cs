
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
    private CancellationTokenSource _cts;

    public BinanceClient(RingBuffer<Candle> buffer)
    {
        _buffer = buffer;
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
            // WebSocket: symbol must be lowercase
            string streamName = $"{symbol.ToLower()}@kline_{interval}";
            string url = $"{_baseUrl}{streamName}"; // _baseUrl was "wss://.../ws/btcusdt@kline_", need to fix base url or logic
            
            // Refactoring URL logic slightly requires changing _baseUrl or just using full string here.
            // Let's hardcode the base wss://stream.binance.com:9443/ws/
            string fullUrl = $"wss://stream.binance.com:9443/ws/{streamName}";
            
            Console.WriteLine($"[Metal] Connecting to Stream ({fullUrl})...");
            await _socket.ConnectAsync(new Uri(fullUrl), CancellationToken.None);
            Console.WriteLine($"[Metal] Connected! {symbol} [{interval}]");
            
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
        // Format: {"e":"kline",..."k":{"t":1234,"o":"50000","c":"50001",...}}
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("k", out var k))
            {
                long startTime = k.GetProperty("t").GetInt64();
                bool isClosed = k.GetProperty("x").GetBoolean(); // "x": Is this kline closed? Yes/No
                
                float open = float.Parse(k.GetProperty("o").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float close = float.Parse(k.GetProperty("c").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float high = float.Parse(k.GetProperty("h").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float low = float.Parse(k.GetProperty("l").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float vol = float.Parse(k.GetProperty("v").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                
                var candle = new Candle
                {
                    Open = open, Close = close, High = high, Low = low, Volume = vol, Timestamp = startTime
                };

                // SMART UPDATE LOGIC
                // Check if the last candle in buffer matches this timestamp
                if (_buffer.Count > 0)
                {
                    ref var last = ref _buffer[0]; // 0 is Head (Latest)
                    if (last.Timestamp == startTime)
                    {
                        // Same candle, just update it in place
                        last = candle; 
                        // Console.Write("."); // Tick update
                    }
                    else if (startTime > last.Timestamp)
                    {
                        // New candle commenced
                        _buffer.Push(candle);
                        // Console.WriteLine("|"); // New Bar
                    }
                }
                else
                {
                    _buffer.Push(candle);
                }
            }
        }
        catch 
        {
            // Ignore
        }
    }
}
