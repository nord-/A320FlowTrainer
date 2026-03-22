using System.Text.RegularExpressions;
using A320FlowTrainer.Models;

namespace A320FlowTrainer.Services;

public class AudioService
{
    private readonly string _audioDir;

    public AudioService(string audioDir)
    {
        _audioDir = audioDir;
    }

    public string GetFlowFilePrefix(Flow flow)
    {
        var flowPart = Regex.Replace(flow.Name.ToLower(), @"[^a-z0-9]", "_");
        return Regex.Replace(flowPart, @"_+", "_").Trim('_');
    }

    /// <summary>
    /// Returns the relative URL path for an item's audio, or null if no file exists.
    /// </summary>
    public string? GetItemAudioUrl(Flow flow, int itemIndex, FlowItem item)
    {
        var flowPart = GetFlowFilePrefix(flow);

        var itemPart = Regex.Replace(item.Item.ToLower(), @"[^a-z0-9]", "_");
        itemPart = Regex.Replace(itemPart, @"_+", "_").Trim('_');
        if (itemPart.Length > 30) itemPart = itemPart.Substring(0, 30);

        var filename = $"{flowPart}_{itemIndex:D2}_{itemPart}";

        var wavPath = Path.Combine(_audioDir, filename + ".wav");
        var mp3Path = Path.Combine(_audioDir, filename + ".mp3");

        if (File.Exists(wavPath))
            return $"/audio/{filename}.wav";
        if (File.Exists(mp3Path))
            return $"/audio/{filename}.mp3";

        return null;
    }

    public string GetItemFallbackText(FlowItem item)
    {
        return $"{item.Item}: {item.Response}";
    }

    public string? GetFlowStartAudioUrl(Flow flow)
    {
        var prefix = GetFlowFilePrefix(flow);
        var wavPath = Path.Combine(_audioDir, $"{prefix}_start.wav");
        var mp3Path = Path.Combine(_audioDir, $"{prefix}_start.mp3");

        if (File.Exists(wavPath))
            return $"/audio/{prefix}_start.wav";
        if (File.Exists(mp3Path))
            return $"/audio/{prefix}_start.mp3";

        return null;
    }

    public string? GetFlowCompleteAudioUrl(Flow flow)
    {
        var prefix = GetFlowFilePrefix(flow);
        var wavPath = Path.Combine(_audioDir, $"{prefix}_complete.wav");
        var mp3Path = Path.Combine(_audioDir, $"{prefix}_complete.mp3");

        if (File.Exists(wavPath))
            return $"/audio/{prefix}_complete.wav";
        if (File.Exists(mp3Path))
            return $"/audio/{prefix}_complete.mp3";

        return null;
    }
}
