using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using A320FlowTrainer.Services;

namespace A320FlowTrainer.WebSocket;

public class FlowWebSocketHandler
{
    private readonly FlowService _flowService;
    private readonly ConfirmationService _confirmationService;
    private readonly AudioService _audioService;
    private readonly SpeechRecognitionService _speechService;

    public FlowWebSocketHandler(
        FlowService flowService,
        ConfirmationService confirmationService,
        AudioService audioService,
        SpeechRecognitionService speechService)
    {
        _flowService = flowService;
        _confirmationService = confirmationService;
        _audioService = audioService;
        _speechService = speechService;
    }

    public async Task HandleAsync(System.Net.WebSockets.WebSocket webSocket)
    {
        var sendLock = new SemaphoreSlim(1, 1);

        async Task Send(object message)
        {
            if (webSocket.State != WebSocketState.Open) return;

            var json = JsonSerializer.Serialize(message);
            var bytes = Encoding.UTF8.GetBytes(json);

            await sendLock.WaitAsync();
            try
            {
                if (webSocket.State == WebSocketState.Open)
                {
                    await webSocket.SendAsync(
                        new ArraySegment<byte>(bytes),
                        WebSocketMessageType.Text,
                        true,
                        CancellationToken.None);
                }
            }
            finally
            {
                sendLock.Release();
            }
        }

        var session = new FlowSession(
            _flowService, _confirmationService, _audioService, _speechService, Send);

        try
        {
            var buffer = new byte[4096];
            while (webSocket.State == WebSocketState.Open)
            {
                var result = await webSocket.ReceiveAsync(
                    new ArraySegment<byte>(buffer), CancellationToken.None);

                if (result.MessageType == WebSocketMessageType.Close)
                    break;

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    var json = Encoding.UTF8.GetString(buffer, 0, result.Count);
                    try
                    {
                        using var doc = JsonDocument.Parse(json);
                        var type = doc.RootElement.GetProperty("type").GetString() ?? "";
                        await session.HandleMessage(type, doc.RootElement);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"Error parsing WS message: {ex.Message}");
                    }
                }
            }
        }
        catch (WebSocketException)
        {
            // Client disconnected
        }
        finally
        {
            session.Dispose();
            if (webSocket.State == WebSocketState.Open)
            {
                await webSocket.CloseAsync(
                    WebSocketCloseStatus.NormalClosure, "Done", CancellationToken.None);
            }
        }
    }
}
