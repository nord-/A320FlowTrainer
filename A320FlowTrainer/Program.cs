using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Speech.Recognition;
using System.Speech.Synthesis;
using System.Text.Json;
using System.Threading;

namespace A320FlowTrainer
{
    class Program
    {
        static List<Flow> _flows = new();
        static string _audioDir = "audio";
        static SpeechRecognitionEngine? _recognizer;
        static ManualResetEvent _recognitionComplete = new(false);
        static string _lastRecognizedText = "";
        static bool _useTextFallback = false;

        static void Main(string[] args)
        {
            Console.Title = "A320 Flow Trainer";
            Console.OutputEncoding = System.Text.Encoding.UTF8;
            
            PrintHeader();
            
            // Ladda flows
            if (!LoadFlows("flows.json"))
            {
                Console.WriteLine("ERROR: Could not load flows.json");
                Console.WriteLine("Make sure flows.json is in the current directory.");
                Console.ReadKey();
                return;
            }
            
            // Initiera speech recognition
            if (!InitializeSpeechRecognition())
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
            _recognizer?.Dispose();
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

        static bool InitializeSpeechRecognition()
        {
            try
            {
                // Visa tillgängliga speech recognition-språk
                var installedRecognizers = SpeechRecognitionEngine.InstalledRecognizers();
                Console.WriteLine($"Installed speech recognizers: {installedRecognizers.Count}");
                foreach (var recognizer in installedRecognizers)
                {
                    Console.WriteLine($"  - {recognizer.Culture.Name}: {recognizer.Description}");
                }

                // Försök hitta en engelsk recognizer
                var englishRecognizer = installedRecognizers
                    .FirstOrDefault(r => r.Culture.Name.StartsWith("en"));

                if (englishRecognizer == null)
                {
                    Console.WriteLine("No English speech recognizer found.");
                    return false;
                }

                Console.WriteLine($"Using: {englishRecognizer.Culture.Name}");
                _recognizer = new SpeechRecognitionEngine(englishRecognizer);

                // Bygg grammar med alla möjliga fraser
                var choices = new Choices();
                
                // Flow-namn
                foreach (var flow in _flows)
                {
                    choices.Add(flow.Name.ToLower());
                    // Lägg till varianter utan "flows"
                    var simplified = flow.Name.ToLower()
                        .Replace(" flows", "")
                        .Replace("flows", "");
                    if (!string.IsNullOrWhiteSpace(simplified))
                        choices.Add(simplified.Trim());
                }
                
                // Bekräftelser
                choices.Add("checked");
                choices.Add("check");
                choices.Add("confirmed");
                choices.Add("set");
                choices.Add("done");
                choices.Add("yes");
                choices.Add("next");
                
                // Item-specifika svar
                choices.Add("on");
                choices.Add("off");
                choices.Add("bright");
                choices.Add("obtained");
                choices.Add("removed");
                choices.Add("closed");
                choices.Add("received");
                choices.Add("monitoring");
                choices.Add("armed");
                choices.Add("disarmed");
                choices.Add("retracted");
                choices.Add("released");
                choices.Add("stowed");
                choices.Add("idle");
                choices.Add("max");
                choices.Add("maximum");
                choices.Add("normal");
                choices.Add("standby");
                choices.Add("start");
                choices.Add("started");
                choices.Add("pressed");
                choices.Add("selected");
                choices.Add("verified");
                choices.Add("aligned");
                choices.Add("stabilized");
                choices.Add("stable");
                choices.Add("shutdown");
                
                // Kontrollkommandon
                choices.Add("skip");
                choices.Add("repeat");
                choices.Add("quit");
                choices.Add("exit");
                choices.Add("stop");
                
                var grammarBuilder = new GrammarBuilder(choices);
                grammarBuilder.Culture = englishRecognizer.Culture;
                var grammar = new Grammar(grammarBuilder);
                grammar.Name = "FlowCommands";

                _recognizer.LoadGrammar(grammar);
                _recognizer.SetInputToDefaultAudioDevice();
                
                _recognizer.SpeechRecognized += OnSpeechRecognized;
                _recognizer.SpeechRecognitionRejected += OnSpeechRejected;
                
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Speech recognition init failed: {ex.Message}");
                return false;
            }
        }

        static void OnSpeechRecognized(object? sender, SpeechRecognizedEventArgs e)
        {
            if (e.Result.Confidence > 0.5)
            {
                _lastRecognizedText = e.Result.Text.ToLower();
                _recognitionComplete.Set();
            }
        }

        static void OnSpeechRejected(object? sender, SpeechRecognitionRejectedEventArgs e)
        {
            // Ignorera - vänta på bättre input
        }

        static void RunFlowTrainer()
        {
            int currentFlowIndex = 0;
            
            while (currentFlowIndex < _flows.Count)
            {
                var flow = _flows[currentFlowIndex];
                
                // Visa nästa flow och vänta på aktivering
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
                
                // Vänta på flow-aktivering
                var activationResult = WaitForFlowActivation(flow);
                
                if (activationResult == ActivationResult.Quit)
                    break;
                
                if (activationResult == ActivationResult.Skip)
                {
                    currentFlowIndex++;
                    continue;
                }
                
                // Kör flow
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
                        Console.ForegroundColor = ConsoleColor.Green;
                        Console.WriteLine($"  ✓ Recognized: \"{input}\"");
                        Console.ResetColor();
                        return ActivationResult.Activated;
                    }
                }
                
                Thread.Sleep(100);
            }
        }

        static bool IsFlowMatch(string input, Flow flow)
        {
            var flowNameLower = flow.Name.ToLower();
            var inputLower = input.ToLower();
            
            // Exakt match
            if (flowNameLower.Contains(inputLower) || inputLower.Contains(flowNameLower))
                return true;
            
            // Match utan "flows"
            var simplified = flowNameLower.Replace(" flows", "").Replace("flows", "").Trim();
            if (inputLower.Contains(simplified) || simplified.Contains(inputLower))
                return true;
            
            // Fuzzy: minst 60% av orden matchar
            var flowWords = flowNameLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var inputWords = inputLower.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            
            int matches = flowWords.Count(fw => inputWords.Any(iw => 
                fw.Contains(iw) || iw.Contains(fw)));
            
            return matches >= flowWords.Length * 0.6;
        }

        static FlowResult RunFlow(Flow flow)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"\n  ▶ Starting: {flow.Name}\n");
            Console.ResetColor();
            
            // Spela flow-start ljud
            PlayFlowStartAudio(flow);
            
            Thread.Sleep(500);
            
            for (int i = 0; i < flow.Items.Count; i++)
            {
                var item = flow.Items[i];
                
                // Visa item
                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"  {i + 1,2}. ");
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.Write(item.Item);
                Console.ForegroundColor = ConsoleColor.DarkGray;
                Console.Write(" → ");
                Console.ForegroundColor = ConsoleColor.White;
                Console.WriteLine(item.Response);
                Console.ResetColor();
                
                // Spela upp ljud
                PlayItemAudio(flow, i, item);
                
                // Vänta på bekräftelse
                var confirmResult = WaitForConfirmation(item);
                
                if (confirmResult == ConfirmResult.Quit)
                    return FlowResult.Quit;
                
                if (confirmResult == ConfirmResult.Repeat)
                {
                    i--; // Upprepa samma item
                    continue;
                }
                
                // Visa bekräftelse
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine($"       ✓ Checked\n");
                Console.ResetColor();
            }
            
            // Spela flow complete ljud
            PlayFlowCompleteAudio(flow);
            
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n  ✓ {flow.Name} - COMPLETE\n");
            Console.ResetColor();
            
            Thread.Sleep(1000);
            
            return FlowResult.Completed;
        }

        static ConfirmResult WaitForConfirmation(FlowItem item)
        {
            while (true)
            {
                if (Console.KeyAvailable)
                {
                    var key = Console.ReadKey(true);
                    if (key.Key == ConsoleKey.Escape)
                        return ConfirmResult.Quit;
                    if (key.Key == ConsoleKey.R)
                        return ConfirmResult.Repeat;
                    if (key.Key == ConsoleKey.Enter || key.Key == ConsoleKey.Spacebar)
                        return ConfirmResult.Confirmed;
                }
                
                if (!_useTextFallback)
                {
                    var input = ListenForSpeech(3000);
                    if (input != null)
                    {
                        if (IsConfirmation(input, item))
                            return ConfirmResult.Confirmed;
                        if (input.Contains("repeat"))
                            return ConfirmResult.Repeat;
                        if (input.Contains("quit") || input.Contains("exit") || input.Contains("stop"))
                            return ConfirmResult.Quit;
                    }
                }
                
                Thread.Sleep(50);
            }
        }

        static bool IsConfirmation(string input, FlowItem? item = null)
        {
            var inputLower = input.ToLower();
            
            // Universella bekräftelser - alltid OK
            var universalConfirms = new[] { "checked", "check", "confirmed", "set", "done", "yes", "next" };
            if (universalConfirms.Any(w => inputLower.Contains(w)))
                return true;
            
            if (item == null)
                return false;
            
            var responseLower = item.Response.ToLower();
            
            // För ON/OFF items - acceptera "on", "off", eller delar av item-namnet + on/off
            if (responseLower.Contains("on") && inputLower.Contains("on"))
                return true;
            if (responseLower.Contains("off") && inputLower.Contains("off"))
                return true;
            
            // För BRT/BRIGHT
            if (responseLower.Contains("brt") && (inputLower.Contains("bright") || inputLower.Contains("brt")))
                return true;
            
            // För specifika responses
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

        static string? ListenForSpeech(int timeoutMs)
        {
            if (_recognizer == null)
                return null;
            
            _recognitionComplete.Reset();
            _lastRecognizedText = "";
            
            try
            {
                _recognizer.RecognizeAsync(RecognizeMode.Single);
                
                if (_recognitionComplete.WaitOne(timeoutMs))
                {
                    return _lastRecognizedText;
                }
                
                _recognizer.RecognizeAsyncCancel();
            }
            catch
            {
                // Ignorera recognition errors
            }
            
            return null;
        }

        static void PlayItemAudio(Flow flow, int itemIndex, FlowItem item)
        {
            // Bygg filnamn (samma logik som Python-scriptet)
            var flowPart = System.Text.RegularExpressions.Regex.Replace(
                flow.Name.ToLower(), @"[^a-z0-9]", "_");
            flowPart = System.Text.RegularExpressions.Regex.Replace(flowPart, @"_+", "_").Trim('_');
            
            var itemPart = System.Text.RegularExpressions.Regex.Replace(
                item.Item.ToLower(), @"[^a-z0-9]", "_");
            itemPart = System.Text.RegularExpressions.Regex.Replace(itemPart, @"_+", "_").Trim('_');
            if (itemPart.Length > 30) itemPart = itemPart.Substring(0, 30);
            
            var filename = $"{flowPart}_{itemIndex:D2}_{itemPart}";
            
            // Försök hitta ljudfil (.wav eller .mp3)
            var wavPath = Path.Combine(_audioDir, filename + ".wav");
            var mp3Path = Path.Combine(_audioDir, filename + ".mp3");
            
            if (File.Exists(wavPath))
                PlayAudioFile(wavPath);
            else if (File.Exists(mp3Path))
                PlayAudioFile(mp3Path);
            else
            {
                // Fallback: använd system TTS
                SpeakText($"{item.Item}: {item.Response}");
            }
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
                using var player = new System.Media.SoundPlayer(path);
                player.PlaySync();
            }
            catch
            {
                // Om .wav inte fungerar, ignorera
            }
        }

        static void SpeakText(string text)
        {
            try
            {
                using var synth = new SpeechSynthesizer();
                synth.Rate = 1;
                synth.Speak(text);
            }
            catch
            {
                // Ignorera TTS errors
            }
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
