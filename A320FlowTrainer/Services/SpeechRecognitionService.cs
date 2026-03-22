using System.Text.Json;
using NAudio.CoreAudioApi;
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
    private int _deviceNumber = -1; // -1 = default

    public bool IsAvailable { get; private set; }

    public event Action<string>? SpeechRecognized;

    public SpeechRecognitionService(string modelPath)
    {
        _modelPath = modelPath;
    }

    public List<AudioDeviceInfo> GetInputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var mmDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Capture, DeviceState.Active);

            // Mappa MMDevice-namn till WaveIn-index via trunkerat namn
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                // Hitta MMDevice med matchande trunkerat namn for fullt namn
                var fullName = caps.ProductName;
                foreach (var mm in mmDevices)
                {
                    if (mm.FriendlyName.StartsWith(caps.ProductName.TrimEnd()) ||
                        mm.FriendlyName.Contains(caps.ProductName.TrimEnd()))
                    {
                        fullName = mm.FriendlyName;
                        break;
                    }
                }
                devices.Add(new AudioDeviceInfo(i, fullName));
            }
        }
        catch
        {
            // Fallback till trunkerade namn
            for (int i = 0; i < WaveInEvent.DeviceCount; i++)
            {
                var caps = WaveInEvent.GetCapabilities(i);
                devices.Add(new AudioDeviceInfo(i, caps.ProductName));
            }
        }
        return devices;
    }

    public List<AudioDeviceInfo> GetOutputDevices()
    {
        var devices = new List<AudioDeviceInfo>();
        try
        {
            var enumerator = new MMDeviceEnumerator();
            var mmDevices = enumerator.EnumerateAudioEndPoints(DataFlow.Render, DeviceState.Active);
            int id = 0;
            foreach (var device in mmDevices)
            {
                devices.Add(new AudioDeviceInfo(id++, device.FriendlyName));
            }
        }
        catch { }
        return devices;
    }

    public int CurrentDeviceNumber => _deviceNumber;

    public void SetInputDevice(int deviceNumber)
    {
        if (deviceNumber == _deviceNumber) return;

        var wasListening = _isListening;
        if (wasListening) StopListening();

        _deviceNumber = deviceNumber;

        // Skapa ny WaveInEvent med valt device
        if (_waveIn != null)
        {
            _waveIn.DataAvailable -= OnAudioDataAvailable;
            _waveIn.Dispose();
        }

        _waveIn = new WaveInEvent
        {
            DeviceNumber = deviceNumber,
            WaveFormat = new WaveFormat(16000, 16, 1),
            BufferMilliseconds = 100
        };
        _waveIn.DataAvailable += OnAudioDataAvailable;

        if (wasListening) StartListening();
    }

    public bool Initialize(StartupLog? log = null)
    {
        try
        {
            if (!Directory.Exists(_modelPath))
            {
                log?.Add("warn", "Vosk model not found - using keyboard fallback");
                return false;
            }

            log?.Add("info", "Loading Vosk speech recognition model...");
            Vosk.Vosk.SetLogLevel(-1);

            _voskModel = new Model(_modelPath);
            _voskRecognizer = new VoskRecognizer(_voskModel, 16000.0f);
            _voskRecognizer.SetMaxAlternatives(0);
            _voskRecognizer.SetWords(true);

            // Logga tillgangliga input devices
            var devices = GetInputDevices();
            if (devices.Count > 0)
            {
                log?.Add("ok", $"Audio input: {devices.Count} device(s) found");
                foreach (var d in devices)
                    log?.Add("info", $"  [{d.Id}] {d.Name}");
            }
            else
            {
                log?.Add("warn", "No audio input devices found");
            }

            _waveIn = new WaveInEvent
            {
                WaveFormat = new WaveFormat(16000, 16, 1),
                BufferMilliseconds = 100
            };

            _waveIn.DataAvailable += OnAudioDataAvailable;

            log?.Add("ok", "Vosk speech recognition ready");
            IsAvailable = true;
            return true;
        }
        catch (Exception ex)
        {
            log?.Add("error", $"Vosk init failed: {ex.Message}");
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

public record AudioDeviceInfo(int Id, string Name);
