using System.Text.Json;
using NAudio.Wave;
using Vosk;

namespace A320FlowTrainer.Services;

public class SpeechRecognitionService : IDisposable
{
    private readonly string _modelPath;
    private Model? _voskModel;
    private VoskRecognizer? _voskRecognizer;
    private WaveInEvent? _waveIn;
    private bool _isListening;
    private volatile bool _isShuttingDown;
    private readonly object _recognitionLock = new();

    public bool IsAvailable { get; private set; }

    public event Action<string>? SpeechRecognized;

    public SpeechRecognitionService(string modelPath)
    {
        _modelPath = modelPath;
    }

    public bool Initialize()
    {
        try
        {
            if (!Directory.Exists(_modelPath))
            {
                Console.WriteLine($"Vosk model not found at '{_modelPath}'");
                return false;
            }

            Console.WriteLine("Loading Vosk speech recognition model...");
            Vosk.Vosk.SetLogLevel(-1);

            _voskModel = new Model(_modelPath);
            _voskRecognizer = new VoskRecognizer(_voskModel, 16000.0f);
            _voskRecognizer.SetMaxAlternatives(0);
            _voskRecognizer.SetWords(true);

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;

            Console.WriteLine("Vosk speech recognition ready!");
            IsAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Vosk init failed: {ex.Message}");
            return false;
        }
    }

    public void StartListening()
    {
        if (_waveIn != null && !_isListening)
        {
            _waveIn.StartRecording();
            _isListening = true;
        }
    }

    public void StopListening()
    {
        if (_waveIn != null && _isListening)
        {
            try
            {
                _waveIn.StopRecording();
            }
            catch { }
            _isListening = false;
        }
    }

    public void ResetState()
    {
        if (_voskRecognizer == null) return;

        lock (_recognitionLock)
        {
            if (_voskRecognizer != null)
            {
                _voskRecognizer.FinalResult();
            }
        }
    }

    private void OnAudioDataAvailable(object? sender, WaveInEventArgs e)
    {
        if (_isShuttingDown || _voskRecognizer == null) return;

        lock (_recognitionLock)
        {
            if (_isShuttingDown || _voskRecognizer == null) return;

            if (_voskRecognizer.AcceptWaveform(e.Buffer, e.BytesRecorded))
            {
                var result = _voskRecognizer.Result();
                var text = ParseVoskResult(result);
                if (!string.IsNullOrWhiteSpace(text))
                {
                    SpeechRecognized?.Invoke(text);
                }
            }
        }
    }

    private static string ParseVoskResult(string json)
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

    public void Dispose()
    {
        _isShuttingDown = true;
        StopListening();
        Thread.Sleep(200);

        lock (_recognitionLock)
        {
            _voskRecognizer?.Dispose();
            _voskRecognizer = null;
        }
        _voskModel?.Dispose();
        _waveIn?.Dispose();
    }
}
