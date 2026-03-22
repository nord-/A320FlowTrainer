using A320FlowTrainer.Models;
using A320FlowTrainer.Services;

namespace A320FlowTrainer.WebSocket;

public enum SessionState
{
    Idle,
    WaitingForFlow,
    PlayingFlowStart,
    PlayingItem,
    WaitingForConfirm,
    FlowComplete,
    AllComplete,
    Paused
}

public class FlowSession
{
    private readonly FlowService _flowService;
    private readonly ConfirmationService _confirmationService;
    private readonly AudioService _audioService;
    private readonly SpeechRecognitionService _speechService;
    private readonly Func<object, Task> _send;

    private SessionState _state = SessionState.Idle;
    private SessionState _stateBeforePause;
    private int _currentFlowIndex;
    private int _currentRunIndex;
    private List<int> _itemsToRun = new();
    private string[] _itemStatus = Array.Empty<string>();
    private bool _testMode;
    private Random _random = new();

    public FlowSession(
        FlowService flowService,
        ConfirmationService confirmationService,
        AudioService audioService,
        SpeechRecognitionService speechService,
        Func<object, Task> send)
    {
        _flowService = flowService;
        _confirmationService = confirmationService;
        _audioService = audioService;
        _speechService = speechService;
        _send = send;

        _speechService.SpeechRecognized += OnSpeechRecognized;
    }

    public async Task HandleMessage(string type, System.Text.Json.JsonElement root)
    {
        switch (type)
        {
            case "ready":
                _testMode = root.TryGetProperty("testMode", out var tm) && tm.GetBoolean();
                await StartSession();
                break;

            case "keypress":
                var key = root.GetProperty("key").GetString() ?? "";
                await HandleKeypress(key);
                break;

            case "audioComplete":
                await HandleAudioComplete();
                break;
        }
    }

    private async Task StartSession()
    {
        var flows = _flowService.Flows;
        await _send(new
        {
            type = "init",
            flows = flows.Select(f => new
            {
                name = f.Name,
                note = f.Note,
                items = f.Items.Select(i => new { item = i.Item, response = i.Response })
            }),
            voskAvailable = _speechService.IsAvailable
        });

        _currentFlowIndex = 0;
        await ShowNextFlow();
    }

    private async Task ShowNextFlow()
    {
        var flows = _flowService.Flows;
        if (_currentFlowIndex >= flows.Count)
        {
            _state = SessionState.AllComplete;
            await _send(new { type = "allComplete" });
            _speechService.StopListening();
            return;
        }

        var flow = flows[_currentFlowIndex];
        _state = SessionState.WaitingForFlow;

        await _send(new
        {
            type = "showFlowActivation",
            flowIndex = _currentFlowIndex,
            flowName = flow.Name,
            flowNote = flow.Note,
            totalFlows = flows.Count
        });

        if (_speechService.IsAvailable)
        {
            _speechService.ResetState();
            _speechService.StartListening();
            await _send(new { type = "listeningState", listening = true });
        }
    }

    private async Task ActivateFlow()
    {
        var flow = _flowService.Flows[_currentFlowIndex];

        // Bygg itemsToRun
        if (_testMode && flow.Items.Count > 3)
        {
            _itemsToRun = Enumerable.Range(0, flow.Items.Count)
                .OrderBy(_ => _random.Next())
                .Take(3)
                .OrderBy(i => i)
                .ToList();
        }
        else
        {
            _itemsToRun = Enumerable.Range(0, flow.Items.Count).ToList();
        }

        _itemStatus = new string[flow.Items.Count];
        for (int i = 0; i < flow.Items.Count; i++)
            _itemStatus[i] = _testMode && !_itemsToRun.Contains(i) ? "skip" : "pending";

        _currentRunIndex = 0;

        await _send(new
        {
            type = "showFlow",
            flowIndex = _currentFlowIndex,
            flowName = flow.Name,
            items = flow.Items.Select(i => new { item = i.Item, response = i.Response }),
            itemStatus = _itemStatus
        });

        // Spela flow start-ljud
        _speechService.StopListening();
        await _send(new { type = "listeningState", listening = false });

        var audioUrl = _audioService.GetFlowStartAudioUrl(flow);
        _state = SessionState.PlayingFlowStart;

        await _send(new
        {
            type = "playAudio",
            audioId = $"flow_start_{_currentFlowIndex}",
            url = audioUrl,
            fallbackText = audioUrl == null ? flow.Name : (string?)null
        });
    }

    private async Task PlayCurrentItem()
    {
        if (_currentRunIndex >= _itemsToRun.Count)
        {
            await CompleteFlow();
            return;
        }

        var flow = _flowService.Flows[_currentFlowIndex];
        var itemIndex = _itemsToRun[_currentRunIndex];
        var item = flow.Items[itemIndex];

        _itemStatus[itemIndex] = "active";
        await _send(new
        {
            type = "updateItem",
            itemIndex,
            status = "active",
            activeIndex = itemIndex,
            itemStatusArray = _itemStatus
        });

        _speechService.StopListening();
        await _send(new { type = "listeningState", listening = false });

        var audioUrl = _audioService.GetItemAudioUrl(flow, itemIndex, item);
        _state = SessionState.PlayingItem;

        await _send(new
        {
            type = "playAudio",
            audioId = $"item_{_currentFlowIndex}_{itemIndex}",
            url = audioUrl,
            fallbackText = audioUrl == null ? _audioService.GetItemFallbackText(item) : (string?)null
        });
    }

    private async Task StartListeningForConfirmation()
    {
        _state = SessionState.WaitingForConfirm;

        if (_speechService.IsAvailable)
        {
            await Task.Delay(200); // Vanta pa att ekot dor ut
            _speechService.StartListening();
            await _send(new { type = "listeningState", listening = true });
        }
    }

    private async Task ConfirmCurrentItem()
    {
        var itemIndex = _itemsToRun[_currentRunIndex];
        _itemStatus[itemIndex] = "done";

        await _send(new
        {
            type = "updateItem",
            itemIndex,
            status = "done",
            activeIndex = -1,
            itemStatusArray = _itemStatus
        });

        _currentRunIndex++;
        await PlayCurrentItem();
    }

    private async Task RepeatCurrentItem()
    {
        await PlayCurrentItem();
    }

    private async Task CompleteFlow()
    {
        var flow = _flowService.Flows[_currentFlowIndex];

        _speechService.StopListening();
        await _send(new { type = "listeningState", listening = false });

        var audioUrl = _audioService.GetFlowCompleteAudioUrl(flow);
        _state = SessionState.FlowComplete;

        await _send(new
        {
            type = "playAudio",
            audioId = $"flow_complete_{_currentFlowIndex}",
            url = audioUrl,
            fallbackText = audioUrl == null ? $"{flow.Name} complete" : (string?)null
        });

        await _send(new { type = "flowComplete", flowIndex = _currentFlowIndex });
    }

    private async Task HandleKeypress(string key)
    {
        if (key == "escape")
        {
            _speechService.StopListening();
            _state = SessionState.AllComplete;
            await _send(new { type = "allComplete" });
            return;
        }

        if (key == "space")
        {
            await TogglePause();
            return;
        }

        switch (_state)
        {
            case SessionState.WaitingForFlow:
                if (key == "enter")
                    await ActivateFlow();
                else if (key == "n" || key == "tab")
                {
                    _currentFlowIndex++;
                    await ShowNextFlow();
                }
                break;

            case SessionState.WaitingForConfirm:
                if (key == "enter")
                    await ConfirmCurrentItem();
                else if (key == "r")
                    await RepeatCurrentItem();
                break;
        }
    }

    private async Task HandleAudioComplete()
    {
        switch (_state)
        {
            case SessionState.PlayingFlowStart:
                await PlayCurrentItem();
                break;

            case SessionState.PlayingItem:
                await StartListeningForConfirmation();
                break;

            case SessionState.FlowComplete:
                _currentFlowIndex++;
                _speechService.ResetState();
                await ShowNextFlow();
                break;
        }
    }

    private async Task TogglePause()
    {
        if (_state == SessionState.Paused)
        {
            _state = _stateBeforePause;
            await _send(new { type = "paused", paused = false });

            if (_state == SessionState.WaitingForFlow || _state == SessionState.WaitingForConfirm)
            {
                if (_speechService.IsAvailable)
                {
                    _speechService.StartListening();
                    await _send(new { type = "listeningState", listening = true });
                }
            }
        }
        else if (_state != SessionState.Idle && _state != SessionState.AllComplete)
        {
            _stateBeforePause = _state;
            _state = SessionState.Paused;
            _speechService.StopListening();
            await _send(new { type = "paused", paused = true });
            await _send(new { type = "listeningState", listening = false });
        }
    }

    private async void OnSpeechRecognized(string text)
    {
        if (_state == SessionState.Paused) return;

        try
        {
            if (_state == SessionState.WaitingForFlow)
            {
                var flow = _flowService.Flows[_currentFlowIndex];
                var (isMatch, score, details) = _confirmationService.IsFlowMatchWithScore(text, flow);

                await _send(new { type = "speechHeard", text, score, matched = isMatch, details });

                if (isMatch)
                    await ActivateFlow();
            }
            else if (_state == SessionState.WaitingForConfirm)
            {
                var flow = _flowService.Flows[_currentFlowIndex];
                var itemIndex = _itemsToRun[_currentRunIndex];
                var item = flow.Items[itemIndex];

                var isConfirm = _confirmationService.IsConfirmation(text, item);

                await _send(new { type = "speechHeard", text, score = isConfirm ? 100 : 0, matched = isConfirm });

                if (isConfirm)
                    await ConfirmCurrentItem();
                else if (text.Contains("repeat"))
                    await RepeatCurrentItem();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error handling speech: {ex.Message}");
        }
    }

    public void Dispose()
    {
        _speechService.SpeechRecognized -= OnSpeechRecognized;
    }
}
