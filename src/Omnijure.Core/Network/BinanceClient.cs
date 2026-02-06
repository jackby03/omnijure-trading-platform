
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
    private readonly string _url = "wss://stream.binance.com:9443/ws/btcusdt@kline_1m";
    private ClientWebSocket _socket;
    private readonly RingBuffer<Candle> _buffer;
    private readonly CancellationTokenSource _cts = new();

    public BinanceClient(RingBuffer<Candle> buffer)
    {
        _buffer = buffer;
    }

    public async Task ConnectAsync()
    {
        _socket = new ClientWebSocket();
        try 
        {
            Console.WriteLine($"[Metal] Connecting to Binance Stream ({_url})...");
            await _socket.ConnectAsync(new Uri(_url), CancellationToken.None);
            Console.WriteLine("[Metal] Connected! Streaming Live Data...");
            
            _ = ReceiveLoop();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[Metal] Connection Failed: {ex.Message}");
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[4096];
        
        while (_socket.State == WebSocketState.Open && !_cts.IsCancellationRequested)
        {
            try 
            {
                var result = await _socket.ReceiveAsync(new ArraySegment<byte>(buffer), _cts.Token);
                if (result.MessageType == WebSocketMessageType.Close) break;

                string json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                ParseAndUpdate(json);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[Metal] Stream Error: {ex.Message}");
                break;
            }
        }
    }

    private void ParseAndUpdate(string json)
    {
        // JSON Parsing (Metal Style - Fast & Dirty or use STJ)
        // Format: {"e":"kline",..."k":{"t":1234,"o":"50000","c":"50001",...}}
        
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            
            if (root.TryGetProperty("k", out var k))
            {
                float open = float.Parse(k.GetProperty("o").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float close = float.Parse(k.GetProperty("c").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float high = float.Parse(k.GetProperty("h").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float low = float.Parse(k.GetProperty("l").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                float vol = float.Parse(k.GetProperty("v").GetString() ?? "0", System.Globalization.CultureInfo.InvariantCulture);
                
                // Construct Candle
                var candle = new Candle
                {
                    Open = open,
                    Close = close,
                    High = high,
                    Low = low,
                    Volume = vol,
                    Timestamp = DateTime.Now.Ticks // Use Timestamp field
                };

                // PUSH TO RING BUFFER
                // Note: Binance Kline streams update the SAME candle many times until it closes.
                // Our RingBuffer push adds a NEW candle. 
                // For a perfect chart, we should UPDATE the head if it's the same minute, or PUSH if new.
                // For this "HFT" demo, pushing updates as new candles makes it look faster (Tick Chart style).
                // Let's stick to Push for the "Matrix" effect, or we can make it smarter later.
                
                _buffer.Push(candle);
            }
        }
        catch 
        {
            // Ignore parse errors (keep stream alive)
        }
    }
}
