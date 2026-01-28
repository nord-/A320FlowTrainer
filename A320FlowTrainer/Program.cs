using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Threading;
using NAudio.CoreAudioApi;
using NAudio.Wave;
using Vosk;

namespace A320FlowTrainer
{
    class Program
    {
        static List<Flow> _flows = new();
        static string _audioDir = "audio";
        static string _modelPath = "model";
        static Model? _voskModel;
        static VoskRecognizer? _voskRecognizer;
        static WaveInEvent? _waveIn;
        static bool _useTextFallback = false;
        static string _lastRecognizedText = "";
        static readonly object _recognitionLock = new();
        static ManualResetEvent _recognitionComplete = new(false);
        static bool _isListening = false;

        static void Main(string[] args)
        {
            Console.Title = "A320 Flow Trainer";
            Console.OutputEncoding = System.Text.Encoding.UTF8;

            // Öka bufferstorleken för långa flows
            try
            {
                Console.BufferHeight = Math.Max(Console.BufferHeight, 200);
            }
            catch { /* Ignorera om det inte går */ }

            PrintHeader();
            ShowAudioDevices();

            // Ladda flows
            if (!LoadFlows("flows.json"))
            {
                Console.WriteLine("ERROR: Could not load flows.json");
                Console.WriteLine("Make sure flows.json is in the current directory.");
                Console.ReadKey();
                return;
            }

            // Initiera Vosk speech recognition
            if (!InitializeVosk())
            {
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine("\nWARNING: Speech recognition not available.");
                Console.WriteLine("Falling back to keyboard input mode.");
                Console.WriteLine("Press ENTER to confirm each item.\n");
                Console.ResetColor();
                _useTextFallback = true;
            }

            // Huvudloop
            RunFlowTrainer();

            // Cleanup
            StopListening();
            _voskRecognizer?.Dispose();
            _voskModel?.Dispose();
        }

        static void ShowAudioDevices()
        {
            try
            {
                var enumerator = new MMDeviceEnumerator();

                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine("Audio Input Devices:");
                Console.ResetColor();

                try
                {
                    var defaultDevice = enumerator.GetDefaultAudioEndpoint(DataFlow.Capture, Role.Communications);
                    Console.ForegroundColor = ConsoleColor.Green;
                    Console.WriteLine($"  ► DEFAULT: {defaultDevice.FriendlyName}");
                    Console.ResetColor();
                }
                catch
                {
                    Console.WriteLine("  (No default device)");
                }

                var devices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);
                foreach (var device in devices)
                {
                    Console.WriteLine($"    - {device.FriendlyName}");
                }
                Console.WriteLine();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Could not enumerate audio devices: {ex.Message}\n");
            }
        }

        static void PrintHeader()
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine(@"
╔═══════════════════════════════════════════════════════════╗
║           AIRBUS A320/321 FLOW TRAINER                   ║
║                                                           ║
║  Say the flow name to start. Say 'CHECKED' after items.  ║
║  Press ESC to quit, F1 for help.                         ║
╚═══════════════════════════════════════════════════════════╝
");
            Console.ResetColor();
        }

        static bool LoadFlows(string filename)
        {
            try
            {
                var json = File.ReadAllText(filename);
                var options = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
                _flows = JsonSerializer.Deserialize<List<Flow>>(json, options) ?? new();
                Console.WriteLine($"Loaded {_flows.Count} flows with {_flows.Sum(f => f.Items.Count)} total items.\n");
                return _flows.Count > 0;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading flows: {ex.Message}");
                return false;
            }
        }

        static bool InitializeVosk()
        {
            try
            {
                // Kolla att modellen finns
                if (!Directory.Exists(_modelPath))
                {
                    Console.WriteLine($"ERROR: Vosk model not found at '{_modelPath}'");
                    Console.WriteLine("Download a model from https://alphacephei.com/vosk/models");
                    return false;
                }

                Console.WriteLine("Loading Vosk speech recognition model...");
                Vosk.Vosk.SetLogLevel(-1); // Tysta Vosk-loggar

                _voskModel = new Model(_modelPath);
                _voskRecognizer = new VoskRecognizer(_voskModel, 16000.0f);
                _voskRecognizer.SetMaxAlternatives(0);
                _voskRecognizer.SetWords(true);

                // Starta mikrofon-capture
                _waveIn = new WaveInEvent
                {
                    WaveFormat = new WaveFormat(16000, 16, 1), // 16kHz, 16-bit, mono
                    BufferMilliseconds = 100
                };

                _waveIn.DataAvailable += OnAudioDataAvailable;

                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Vosk speech recognition ready!\n");
                Console.ResetColor();

                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Vosk init failed: {ex.Message}");
                return false;
            }
        }

        static void StartListening()
        {
            if (_waveIn != null && !_isListening)
            {
                _waveIn.StartRecording();
                _isListening = true;
            }
        }

        static void StopListening()
        {
            if (_waveIn != null && _isListening)
            {
                _waveIn.StopRecording();
                _isListening = false;
            }
        }

        static void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
        {
            if (_voskRecognizer == null) return;

            lock (_recognitionLock)
            {
                if (_voskRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
                {
                    var result = _voskRecognizer.Result();
                    var text = ParseVoskResult(result);
                    if (!string.IsNullOrWhiteSpace(text))
                    {
                        _lastRecognizedText = text;
                        _recognitionComplete.Set();
                    }
                }
            }
        }

        static string ParseVoskResult(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("text", out var textElement))
                {
                    return textElement.GetString() ?? "";
                }
            }
            catch { }
            return "";
        }

        static string ParseVoskPartial(string json)
        {
            try
            {
                using var doc = JsonDocument.Parse(json);
                if (doc.RootElement.TryGetProperty("partial", out var textElement))
                {
                    return textElement.GetString() ?? "";
                }
            }
            catch { }
            return "";
        }

        static string? ListenForSpeech(int timeoutMs)
        {
            if (_voskRecognizer == null || _waveIn == null)
                return null;

            _recognitionComplete.Reset();
            _lastRecognizedText = "";

            StartListening();

            if (_recognitionComplete.WaitOne(timeoutMs))
            {
                return _lastRecognizedText;
            }

            return null;
        }

        static void RunFlowTrainer()
        {
            int currentFlowIndex = 0;

            while (currentFlowIndex < _flows.Count)
            {
                var flow = _flows[currentFlowIndex];

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"\n{'═'.ToString().PadRight(60, '═')}");
                Console.WriteLine($"  NEXT FLOW: {flow.Name}");
                if (!string.IsNullOrEmpty(flow.Note))
                {
                    Console.ForegroundColor = ConsoleColor.DarkYellow;
                    Console.WriteLine($"  ({flow.Note})");
                }
                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"{'═'.ToString().PadRight(60, '═')}");
                Console.ResetColor();
                Console.WriteLine("\n  Say the flow name to begin...\n");

                var activationResult = WaitForFlowActivation(flow);

                if (activationResult == ActivationResult.Quit)
                    break;

                if (activationResult == ActivationResult.Skip)
                {
                    currentFlowIndex++;
                    continue;
                }

                var flowResult = RunFlow(flow);

                if (flowResult == FlowResult.Quit)
                    break;

                currentFlowIndex++;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("\n\n  ALL FLOWS COMPLETE! Great job!\n");
            Console.ResetColor();
        }

        static ActivationResult WaitForFlowActivation(Flow flow)
        {
            StartListening();

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                        return ActivationResult.Quit;
                    if (key.Key == ConsoleKey.N || key.Key == ConsoleKey.Tab)
                        return ActivationResult.Skip;
                    if (key.Key == ConsoleKey.Enter)
                        return ActivationResult.Activated;
                }

                if (!_useTextFallback)
                {
                    var input = ListenForSpeech(2000);
                    if (input != null && IsFlowMatch(input, flow))
                    {
                        return ActivationResult.Activated;
                    }
                }

                Thread.Sleep(50);
            }
        }

        static bool IsFlowMatch(string input, Flow flow)
        {
            var flowNameLower = flow.Name.ToLower();
            var inputLower = input.ToLower();

            if (flowNameLower.Contains(inputLower) || inputLower.Contains(flowNameLower))
                return true;

            var simplified = flowNameLower.Replace(" flows", "").Replace("flows", "").Trim();
            if (inputLower.Contains(simplified) || simplified.Contains(inputLower))
                return true;

            var flowWords = flowNameLower
                .Split(' ', StringSplitOptions.RemoveEmptyEntries)
                .Where(w => w != "flows" && w.Length > 3)
                .ToList();

            foreach (var fw in flowWords)
            {
                if (inputLower.Contains(fw))
                    return true;
                if (fw.Length > 5 && inputLower.Contains(fw.Substring(0, 5)))
                    return true;
            }

            return false;
        }

        static string[] _itemStatus = Array.Empty<string>();
        static int _visibleItems;
        static int _windowStart;

        static FlowResult RunFlow(Flow flow)
        {
            Console.Clear();

            // Beräkna hur många items som får plats (lämna plats för header + status)
            _visibleItems = Math.Min(Console.WindowHeight - 5, flow.Items.Count);
            _windowStart = 0;
            _itemStatus = new string[flow.Items.Count];

            for (int i = 0; i < flow.Items.Count; i++)
                _itemStatus[i] = "   ";

            DrawFlowView(flow, -1);
            PlayFlowStartAudio(flow);

            for (int i = 0; i < flow.Items.Count; i++)
            {
                var item = flow.Items[i];

                // Scrolla fönstret om nödvändigt
                if (i >= _windowStart + _visibleItems)
                {
                    _windowStart = i - _visibleItems + 1;
                }
                else if (i < _windowStart)
                {
                    _windowStart = i;
                }

                // Markera aktiv item
                _itemStatus[i] = " ► ";
                DrawFlowView(flow, i);

                PlayItemAudio(flow, i, item);

                var (confirmResult, heardText) = WaitForConfirmationWithText(item, flow);

                if (confirmResult == ConfirmResult.Quit)
                    return FlowResult.Quit;

                if (confirmResult == ConfirmResult.Repeat)
                {
                    i--;
                    continue;
                }

                // Markera klar
                _itemStatus[i] = " ✓ ";
            }

            DrawFlowView(flow, -1, "✓ COMPLETE");
            PlayFlowCompleteAudio(flow);
            Thread.Sleep(1000);
            return FlowResult.Completed;
        }

        static void DrawFlowView(Flow flow, int activeIndex, string? statusText = null)
        {
            // Dölj cursor under ritning för att undvika flimmer
            Console.CursorVisible = false;
            Console.SetCursorPosition(0, 0);

            int width = Console.WindowWidth;

            // Header
            Console.ForegroundColor = ConsoleColor.Cyan;
            ClearAndWrite($"  ▶ {flow.Name}", width);

            // Progress
            int completed = _itemStatus.Count(s => s == " ✓ ");
            Console.ForegroundColor = ConsoleColor.DarkGray;
            ClearAndWrite($"  [{completed}/{flow.Items.Count}] " + new string('─', 50), width);
            Console.ResetColor();

            // Items i fönstret
            for (int i = 0; i < _visibleItems; i++)
            {
                int itemIndex = _windowStart + i;
                if (itemIndex < flow.Items.Count)
                {
                    PrintFlowItem(itemIndex, flow.Items[itemIndex], itemIndex == activeIndex, width);
                }
                else
                {
                    ClearAndWrite("", width);
                }
            }

            // Scroll-indikatorer
            Console.ForegroundColor = ConsoleColor.DarkGray;
            if (_windowStart > 0)
                ClearAndWrite($"  ↑ {_windowStart} more above", width);
            else
                ClearAndWrite("", width);

            int remaining = flow.Items.Count - (_windowStart + _visibleItems);
            if (remaining > 0)
                ClearAndWrite($"  ↓ {remaining} more below", width);
            else
                ClearAndWrite("", width);

            // Statusrad
            Console.ForegroundColor = statusText != null && statusText.StartsWith("✓") ? ConsoleColor.Green : ConsoleColor.DarkGray;
            ClearAndWrite($"  {statusText ?? "[Listening...]"}", width);
            Console.ResetColor();
            Console.CursorVisible = true;
        }

        static void ClearAndWrite(string text, int width)
        {
            if (text.Length >= width)
                text = text.Substring(0, width - 1);
            Console.Write(text);
            Console.WriteLine(new string(' ', Math.Max(0, width - text.Length - 1)));
        }

        static void PrintFlowItem(int index, FlowItem item, bool isActive, int width)
        {
            string status = _itemStatus[index];

            // Rensa raden först genom att skriva spaces
            string clearLine = new string(' ', width - 1);
            Console.Write("\r" + clearLine + "\r");

            // Skriv med färger
            Console.ForegroundColor = status == " ✓ " ? ConsoleColor.Green :
                                      isActive ? ConsoleColor.Yellow : ConsoleColor.DarkGray;
            Console.Write($" {status}");

            Console.ForegroundColor = isActive ? ConsoleColor.White : ConsoleColor.Gray;
            Console.Write($"{index + 1,2}. ");

            Console.ForegroundColor = isActive ? ConsoleColor.Cyan : ConsoleColor.DarkCyan;
            Console.Write(item.Item);

            Console.ForegroundColor = ConsoleColor.DarkGray;
            Console.Write(" → ");

            Console.ForegroundColor = isActive ? ConsoleColor.White : ConsoleColor.Gray;
            string response = item.Response;

            // Beräkna använd bredd: " " + status(3) + nummer(2) + ". "(2) + item + " → "(3) + response
            int usedSoFar = 1 + 3 + 2 + 2 + item.Item.Length + 3;
            int remainingWidth = width - usedSoFar - 1;
            if (response.Length > remainingWidth && remainingWidth > 3)
                response = response.Substring(0, remainingWidth - 3) + "...";

            Console.WriteLine(response);
            Console.ResetColor();
        }

        static (ConfirmResult result, string heardText) WaitForConfirmationWithText(FlowItem item, Flow flow)
        {
            StartListening();

            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                        return (ConfirmResult.Quit, "");
                    if (key.Key == ConsoleKey.R)
                        return (ConfirmResult.Repeat, "");
                    if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
                        return (ConfirmResult.Confirmed, "[key]");
                }

                if (!_useTextFallback)
                {
                    var input = ListenForSpeech(3000);
                    if (input != null)
                    {
                        // Visa vad vi hörde
                        int activeIdx = Array.IndexOf(_itemStatus, " ► ");
                        DrawFlowView(flow, activeIdx, $"Heard: \"{input}\"");

                        if (IsConfirmation(input, item))
                            return (ConfirmResult.Confirmed, input);
                        if (input.Contains("repeat"))
                            return (ConfirmResult.Repeat, input);
                        if (input.Contains("quit") || input.Contains("exit") || input.Contains("stop"))
                            return (ConfirmResult.Quit, input);
                    }
                }

                Thread.Sleep(50);
            }
        }

        static bool IsConfirmation(string input, FlowItem? item = null)
        {
            var inputLower = input.ToLower();

            // Inkludera varianter som Vosk ibland hör istället för "check"
            var universalConfirms = new[] {
                "checked", "check", "confirmed", "set", "done", "yes", "next", "correct", "okay", "ok", "bright",
                "shaq", "shake", "czech", "jack", "chuck", "chick", "track"  // Vosk-varianter av "check"
            };
            if (universalConfirms.Any(w => inputLower.Contains(w)))
                return true;

            if (item == null)
                return false;

            var responseLower = item.Response.ToLower();

            if (responseLower.Contains("on") && inputLower.Contains("on"))
                return true;
            if (responseLower.Contains("off") && (inputLower.Contains("off") || inputLower.Contains(" of") || inputLower.EndsWith(" of")))
                return true;
            if (responseLower.Contains("down") && inputLower.Contains("down"))
                return true;
            if (responseLower.Contains("up") && inputLower.Contains(" up"))
                return true;

            // För items med siffror (inkl. flygradio-uttal)
            var numberMatches = new Dictionary<string, string[]>
            {
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

            foreach (var kvp in numberMatches)
            {
                if (responseLower.Contains(kvp.Key))
                {
                    if (kvp.Value.Any(v => inputLower.Contains(v)))
                        return true;
                }
            }

            if (responseLower.Contains("verify") && (inputLower.Contains("verify") || inputLower.Contains("verified")))
                return true;

            if (responseLower.Contains("brt") && (inputLower.Contains("bright") || inputLower.Contains("brt")))
                return true;

            var responseMatches = new Dictionary<string, string[]>
            {
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
                { "idle", new[] { "idle" } },
                { "max", new[] { "max", "maximum" } },
                { "norm", new[] { "normal", "norm" } },
                { "standby", new[] { "standby" } },
                { "stby", new[] { "standby" } },
                { "start", new[] { "start", "started", "starting" } },
                { "press", new[] { "pressed", "press" } },
                { "select", new[] { "selected", "select" } },
                { "verify", new[] { "verified", "verify" } },
                { "aligned", new[] { "aligned", "align" } },
                { "stabilized", new[] { "stabilized", "stable" } },
                { "shutdown", new[] { "shutdown", "shut down" } },
            };

            foreach (var kvp in responseMatches)
            {
                if (responseLower.Contains(kvp.Key))
                {
                    if (kvp.Value.Any(v => inputLower.Contains(v)))
                        return true;
                }
            }

            return false;
        }

        static void PlayItemAudio(Flow flow, int itemIndex, FlowItem item)
        {
            var flowPart = System.Text.RegularExpressions.Regex.Replace(
                flow.Name.ToLower(), @"[^a-z0-9]", "_");
            flowPart = System.Text.RegularExpressions.Regex.Replace(flowPart, @"_+", "_").Trim('_');

            var itemPart = System.Text.RegularExpressions.Regex.Replace(
                item.Item.ToLower(), @"[^a-z0-9]", "_");
            itemPart = System.Text.RegularExpressions.Regex.Replace(itemPart, @"_+", "_").Trim('_');
            if (itemPart.Length > 30) itemPart = itemPart.Substring(0, 30);

            var filename = $"{flowPart}_{itemIndex:D2}_{itemPart}";

            var wavPath = Path.Combine(_audioDir, filename + ".wav");
            var mp3Path = Path.Combine(_audioDir, filename + ".mp3");

            if (File.Exists(wavPath))
                PlayAudioFile(wavPath);
            else if (File.Exists(mp3Path))
                PlayAudioFile(mp3Path);
            else
                SpeakText($"{item.Item}: {item.Response}");
        }

        static string GetFlowFilePrefix(Flow flow)
        {
            var flowPart = System.Text.RegularExpressions.Regex.Replace(
                flow.Name.ToLower(), @"[^a-z0-9]", "_");
            return System.Text.RegularExpressions.Regex.Replace(flowPart, @"_+", "_").Trim('_');
        }

        static void PlayFlowStartAudio(Flow flow)
        {
            var prefix = GetFlowFilePrefix(flow);
            var wavPath = Path.Combine(_audioDir, $"{prefix}_start.wav");
            var mp3Path = Path.Combine(_audioDir, $"{prefix}_start.mp3");

            if (File.Exists(wavPath))
                PlayAudioFile(wavPath);
            else if (File.Exists(mp3Path))
                PlayAudioFile(mp3Path);
            else
                SpeakText(flow.Name);
        }

        static void PlayFlowCompleteAudio(Flow flow)
        {
            var prefix = GetFlowFilePrefix(flow);
            var wavPath = Path.Combine(_audioDir, $"{prefix}_complete.wav");
            var mp3Path = Path.Combine(_audioDir, $"{prefix}_complete.mp3");

            if (File.Exists(wavPath))
                PlayAudioFile(wavPath);
            else if (File.Exists(mp3Path))
                PlayAudioFile(mp3Path);
            else
                SpeakText($"{flow.Name} complete");
        }

        static void PlayAudioFile(string path)
        {
            try
            {
                StopListening();
                Thread.Sleep(100); // Låt mikrofonen tystna

                using var player = new System.Media.SoundPlayer(path);
                player.PlaySync();

                Thread.Sleep(200); // Vänta så att ekot dör ut
            }
            catch { }
        }

        static void SpeakText(string text)
        {
            try
            {
                StopListening();
                Thread.Sleep(100);

                using var synth = new SpeechSynthesizer();
                synth.Rate = 1;
                synth.Speak(text);

                Thread.Sleep(200);
            }
            catch { }
        }
    }

    enum ActivationResult { Activated, Skip, Quit }
    enum FlowResult { Completed, Quit }
    enum ConfirmResult { Confirmed, Repeat, Quit }

    class Flow
    {
        public string Name { get; set; } = "";
        public string TriggerPhrase { get; set; } = "";
        public string? Note { get; set; }
        public List<FlowItem> Items { get; set; } = new();
    }

    class FlowItem
    {
        public string Item { get; set; } = "";
        public string Response { get; set; } = "";
        public string? Section { get; set; }
    }
}
