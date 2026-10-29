using System;
using System.IO;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Icatya.Services.Rooms;

// One persistent connection for presence/signaling. Video never enters this transport.
internal sealed class RoomConnection : IAsyncDisposable
{
    private readonly CancellationTokenSource _stop = new();
    private readonly SemaphoreSlim _send = new(1);
    private ClientWebSocket? _socket;
    private Task? _loop;
    private TaskCompletionSource? _pong;
    public event Action<JsonElement>? Message;
    public event Action<string>? Status;
    public event Action? Disconnected;
    public void Start(Uri service, RoomIdentity identity, string clientKey, Func<string> avatar) => _loop = RunAsync(service, identity, clientKey, avatar);
    private async Task RunAsync(Uri service, RoomIdentity identity, string clientKey, Func<string> avatar)
    {
        var uri = new UriBuilder(service) { Scheme = service.Scheme == "https" ? "wss" : "ws", Path = "/v1/rooms/" + identity.Code[7..].ToLowerInvariant() }.Uri;
        int delay = 1;
        while (!_stop.IsCancellationRequested)
        {
            using var socket = new ClientWebSocket();
            using var connected = CancellationTokenSource.CreateLinkedTokenSource(_stop.Token);
            Task? heartbeat = null;
            try
            {
                Status?.Invoke("Conectando à sala…");
                await socket.ConnectAsync(uri, _stop.Token).WaitAsync(TimeSpan.FromSeconds(20), _stop.Token);
                _socket = socket;
                await SendAsync(new { type = "join", nick = identity.Nick, roomName = identity.Name, clientKey, avatar = avatar() }, _stop.Token);
                heartbeat = HeartbeatAsync(connected.Token);
                delay = 1;
                var buffer = new byte[8192];
                while (socket.State == WebSocketState.Open)
                {
                    using var message = new MemoryStream();
                    WebSocketReceiveResult part;
                    do
                    {
                        part = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), _stop.Token);
                        if (part.MessageType == WebSocketMessageType.Close)
                        {
                            if (socket.CloseStatus == (WebSocketCloseStatus)4001)
                            {
                                Status?.Invoke("Esta sala foi aberta em outra janela deste Icatya. Feche a outra janela para usar esta.");
                                return;
                            }
                            throw new IOException("Sala desconectada.");
                        }
                        if (part.MessageType != WebSocketMessageType.Text || message.Length + part.Count > 32768)
                            throw new IOException("Resposta inválida do serviço de salas.");
                        message.Write(buffer, 0, part.Count);
                    } while (!part.EndOfMessage);
                    var text = Encoding.UTF8.GetString(message.GetBuffer(), 0, (int)message.Length);
                    if (text == "pong") { _pong?.TrySetResult(); continue; }
                    using var json = JsonDocument.Parse(text);
                    if (json.RootElement.TryGetProperty("type", out var type) && type.GetString() == "pong") { _pong?.TrySetResult(); continue; }
                    Message?.Invoke(json.RootElement.Clone());
                }
            }
            catch (OperationCanceledException) when (_stop.IsCancellationRequested) { break; }
            catch (Exception ex) when (ex is WebSocketException or IOException or TimeoutException or JsonException or OperationCanceledException)
            { Status?.Invoke("Serviço da sala indisponível ou sala cheia. Tentando reconectar…"); }
            finally
            {
                _socket = null;
                connected.Cancel();
                socket.Abort();
                if (heartbeat != null) try { await heartbeat; } catch (OperationCanceledException) { }
                Disconnected?.Invoke();
            }
            try { await Task.Delay(TimeSpan.FromSeconds(delay), _stop.Token); }
            catch (OperationCanceledException) { break; }
            delay = Math.Min(delay * 2, 30);
        }
    }
    private async Task HeartbeatAsync(CancellationToken cancellation)
    {
        try
        {
            using var timer = new PeriodicTimer(TimeSpan.FromSeconds(30));
            while (await timer.WaitForNextTickAsync(cancellation))
            {
                var pong = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                _pong = pong;
                await SendAsync(new { type = "heartbeat" }, cancellation);
                await pong.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellation);
            }
        }
        catch (Exception ex) when (ex is IOException or WebSocketException or TimeoutException) { _socket?.Abort(); }
    }
    public Task SendAsync(object message, CancellationToken cancellation = default) => SendTextAsync(JsonSerializer.Serialize(message), cancellation);
    private async Task SendTextAsync(string text, CancellationToken cancellation)
    {
        await _send.WaitAsync(cancellation);
        try
        {
            var socket = _socket;
            if (socket?.State != WebSocketState.Open) throw new IOException("A sala está reconectando.");
            await socket.SendAsync(new ArraySegment<byte>(Encoding.UTF8.GetBytes(text)), WebSocketMessageType.Text, true, cancellation);
        }
        finally { _send.Release(); }
    }
    public async ValueTask DisposeAsync()
    {
        _stop.Cancel();
        _socket?.Abort();
        if (_loop != null) await _loop;
        _stop.Dispose();
        // In-flight signaling tasks may still be leaving the send semaphore.
    }
}
