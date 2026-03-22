namespace A320FlowTrainer.Services;

public class StartupLog
{
    private readonly List<LogEntry> _entries = new();

    public IReadOnlyList<LogEntry> Entries => _entries;

    public void Add(string level, string message)
    {
        _entries.Add(new LogEntry(level, message));
        Console.WriteLine($"[{level.ToUpper()}] {message}");
    }

    public record LogEntry(string Level, string Message);
}
