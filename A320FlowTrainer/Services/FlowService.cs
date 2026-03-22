using System.Text.Json;
using A320FlowTrainer.Models;

namespace A320FlowTrainer.Services;

public class FlowService
{
    private List<Flow> _flows = new();

    public List<Flow> Flows => _flows;

    public bool LoadFlows(string filename)
    {
        try
        {
            var json = File.ReadAllText(filename);
            var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
            _flows = JsonSerializer.Deserialize<List<Flow>>(json, options) ?? new();
            Console.WriteLine($"Loaded {_flows.Count} flows with {_flows.Sum(f => f.Items.Count)} total items.");
            return _flows.Count > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading flows: {ex.Message}");
            return false;
        }
    }
}
