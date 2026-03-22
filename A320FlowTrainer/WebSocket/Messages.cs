using System.Text.Json.Serialization;

namespace A320FlowTrainer.WebSocket;

// Base message
public class WsMessage
{
    [JsonPropertyName("type")]
    public string Type { get; set; } = "";
}

// Client -> Server
public class ReadyMessage : WsMessage
{
    [JsonPropertyName("testMode")]
    public bool TestMode { get; set; }
}

public class KeypressMessage : WsMessage
{
    [JsonPropertyName("key")]
    public string Key { get; set; } = "";
}

public class AudioCompleteMessage : WsMessage
{
    [JsonPropertyName("audioId")]
    public string AudioId { get; set; } = "";
}

// Server -> Client (sent as anonymous objects via JsonSerializer)
