
using System.Net.WebSockets;
using System.Text;

namespace Omnijure.Core.Network;

public abstract class ExchangeSocket : IDisposable
{
    protected ClientWebSocket _socket;
    protected readonly CancellationTokenSource _cts;
    protected readonly Uri _endpoint;

    public event Action<string>? OnMessageReceived;
    public event Action<string>? OnError;

    public bool IsConnected => _socket.State == WebSocketState.Open;

    protected ExchangeSocket(string url)
    {
        _endpoint = new Uri(url);
        _socket = new ClientWebSocket();
        _cts = new CancellationTokenSource();
    }

    public async Task ConnectAsync()
    {
        try
        {
            if (_socket.State == WebSocketState.Open) return;
            
            // Re-create socket if it was disposed or used
            if (_socket.State == WebSocketState.Aborted || _socket.State == WebSocketState.Closed)
            {
                _socket.Dispose();
                _socket = new ClientWebSocket();
            }

            await _socket.ConnectAsync(_endpoint, _cts.Token);
            _ = Task.Run(ReceiveLoop, _cts.Token);
        }
        catch (Exception ex)
        {
            OnError?.Invoke($"Connection Error: {ex.Message}");
        }
    }

    private async Task ReceiveLoop()
    {
        var buffer = new byte[1024 * 4];
        var segment = new ArraySegment<byte>(buffer);

        try
        {
            while (_socket.State == WebSocketState.Open && !_cts.Token.IsCancellationRequested)
            {
                WebSocketReceiveResult result;
                using var ms = new MemoryStream();
                
                do
                {
                    result = await _socket.ReceiveAsync(segment, _cts.Token);
                    ms.Write(buffer, 0, result.Count);
                } while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    await _socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing", CancellationToken.None);
                    break;
                }

                var message = Encoding.UTF8.GetString(ms.ToArray());
                ProcessMessage(message);
            }
        }
        catch (OperationCanceledException) { /* Graceful exit */ }
        catch (Exception ex)
        {
            OnError?.Invoke($"Receive Error: {ex.Message}");
        }
    }

    protected virtual void ProcessMessage(string rawJson)
    {
        // Parsing logic here or invoke event
        OnMessageReceived?.Invoke(rawJson);
    }

    public void Dispose()
    {
        _cts.Cancel();
        _socket.Dispose();
    }
}
