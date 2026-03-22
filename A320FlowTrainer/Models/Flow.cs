using System.Text.Json.Serialization;

namespace A320FlowTrainer.Models;

public enum ActivationResult { Activated, Skip, Quit }
public enum FlowResult { Completed, Quit }
public enum ConfirmResult { Confirmed, Repeat, Quit }

public class Flow
{
    public string Name { get; set; } = "";

    [JsonPropertyName("trigger_phrase")]
    public string TriggerPhrase { get; set; } = "";

    public string? Note { get; set; }
    public List<FlowItem> Items { get; set; } = new();
}

public class FlowItem
{
    public string Item { get; set; } = "";
    public string Response { get; set; } = "";
    public string? Section { get; set; }
}
