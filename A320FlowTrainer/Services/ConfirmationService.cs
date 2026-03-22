using System.Text.RegularExpressions;
using A320FlowTrainer.Models;

namespace A320FlowTrainer.Services;

public class ConfirmationService
{
    // Ord som alltid accepteras som bekraftelse
    private static readonly string[] UniversalConfirms = {
        "checked", "check", "confirmed", "set", "done", "yes", "next", "correct", "okay", "ok",
        "shaq", "shake", "czech", "jack", "chuck", "chick", "track"  // Vosk-varianter av "check"
    };

    // Vanliga tillstand (for "AS RQRD" och liknande)
    private static readonly string[] StateWords = {
        "on", "off", "set", "selected", "auto", "manual", "normal", "norm",
        "open", "closed", "required", "not required", "bright", "dim"
    };

    // Trigger i response/item -> godkanda input-ord
    private static readonly Dictionary<string, string[]> WordMatches = new()
    {
        { "on", new[] { "on" } },
        { "off", new[] { "off" } },
        { "down", new[] { "down" } },
        { "up", new[] { "up" } },
        { "brt", new[] { "bright", "brt" } },
        { "stby", new[] { "standby" } },
        { "norm", new[] { "normal", "norm" } },
        { "obtain", new[] { "obtained", "obtain" } },
        { "removed", new[] { "removed", "remove" } },
        { "closed", new[] { "closed", "close" } },
        { "received", new[] { "received", "receive" } },
        { "monitor", new[] { "monitoring", "monitor" } },
        { "armed", new[] { "armed", "arm" } },
        { "disarm", new[] { "disarmed", "disarm" } },
        { "retract", new[] { "retracted", "retract" } },
        { "released", new[] { "released", "release" } },
        { "stowed", new[] { "stowed", "stow" } },
        { "start", new[] { "start", "started", "starting" } },
        { "press", new[] { "pressed", "press" } },
        { "select", new[] { "selected", "select" } },
        { "verify", new[] { "verified", "verify" } },
        { "aligned", new[] { "aligned", "align" } },
        { "stabilized", new[] { "stabilized", "stable" } },
        { "shutdown", new[] { "shutdown", "shut down" } },
        { "idle", new[] { "idle" } },
        { "max", new[] { "max", "maximum" } },
        { "standby", new[] { "standby" } },
        { "0", new[] { "zero", "0" } },
        { "1", new[] { "one", "1", "wun" } },
        { "2", new[] { "two", "2", "too" } },
        { "3", new[] { "three", "3", "tree" } },
        { "4", new[] { "four", "4", "fower" } },
        { "5", new[] { "five", "5", "fife" } },
        { "6", new[] { "six", "6" } },
        { "7", new[] { "seven", "7" } },
        { "8", new[] { "eight", "8", "ait" } },
        { "9", new[] { "nine", "9", "niner" } },
    };

    // Trigger i item-namn -> godkanda input-ord
    private static readonly Dictionary<string, string[]> ItemMatches = new()
    {
        { "clearance", new[] { "clear", "clearance", "cleared", "approved", "received" } },
        { "communication", new[] { "communication", "established", "contact" } },
    };

    public bool IsConfirmation(string input, FlowItem? item = null)
    {
        var inputLower = input.ToLower();
        inputLower = Regex.Replace(inputLower, @"\bof\b", "off");

        if (UniversalConfirms.Any(w => inputLower.Contains(w)))
            return true;

        if (item == null)
            return false;

        var responseLower = item.Response.ToLower();
        var itemLower = item.Item.ToLower();

        foreach (var kvp in WordMatches)
        {
            if (responseLower.Contains(kvp.Key))
            {
                if (kvp.Value.Any(v => inputLower.Contains(v)))
                    return true;
            }
        }

        foreach (var kvp in ItemMatches)
        {
            if (itemLower.Contains(kvp.Key))
            {
                if (kvp.Value.Any(v => inputLower.Contains(v)))
                    return true;
            }
        }

        if (responseLower.Contains("rqrd") || responseLower.Contains("required"))
        {
            if (StateWords.Any(s => inputLower.Contains(s)))
                return true;
        }

        return false;
    }

    public (bool isMatch, int score, string details) IsFlowMatchWithScore(string input, Flow flow)
    {
        var flowNameLower = flow.Name.ToLower();
        var inputLower = input.ToLower();

        if (flowNameLower.Contains(inputLower) || inputLower.Contains(flowNameLower))
            return (true, 100, "exact");

        var flowNoSpaces = flowNameLower.Replace(" ", "").Replace("flows", "");
        var inputNoSpaces = inputLower.Replace(" ", "");
        if (flowNoSpaces.Contains(inputNoSpaces) || inputNoSpaces.Contains(flowNoSpaces))
            return (true, 95, "no-space");

        var inputWords = inputLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var flowText = flowNameLower.Replace("flows", "");
        int matchedWords = 0;
        var matchedList = new List<string>();

        foreach (var word in inputWords)
        {
            if (word.Length < 2) continue;

            if (flowText.Contains(word))
            {
                matchedWords++;
                matchedList.Add(word);
            }
            else if (flowNoSpaces.Contains(word))
            {
                matchedWords++;
                matchedList.Add($"{word}*");
            }
        }

        int totalWords = inputWords.Count(w => w.Length >= 2);
        int score = totalWords > 0 ? (matchedWords * 100) / totalWords : 0;
        string details = matchedList.Count > 0
            ? $"{matchedWords}/{totalWords} [{string.Join(",", matchedList)}]"
            : $"0/{totalWords}";

        bool isMatch = score >= 50 && matchedWords >= 2;

        return (isMatch, score, details);
    }

    public bool IsFlowMatch(string input, Flow flow)
    {
        return IsFlowMatchWithScore(input, flow).isMatch;
    }
}
