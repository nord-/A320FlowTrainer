using System.Diagnostics;
using A320FlowTrainer.Services;
using A320FlowTrainer.WebSocket;
using Microsoft.Extensions.FileProviders;

var builder = WebApplication.CreateBuilder(args);

// Konfigurera Kestrel
builder.WebHost.UseUrls("http://localhost:5320");

// Registrera services - anvand BaseDirectory dar build kopierar flows.json och audio/
var baseDir = AppContext.BaseDirectory;
var startupLog = new StartupLog();
var flowService = new FlowService();
var audioDir = Path.Combine(baseDir, "audio");
var modelPath = Path.Combine(baseDir, "model");

if (!flowService.LoadFlows(Path.Combine(baseDir, "flows.json"), startupLog))
{
    Console.WriteLine("ERROR: Could not load flows.json");
    return;
}

// Kolla ljudfiler
var audioFileCount = Directory.Exists(audioDir)
    ? Directory.GetFiles(audioDir, "*.wav").Length
    : 0;
startupLog.Add(audioFileCount > 0 ? "ok" : "warn",
    audioFileCount > 0
        ? $"Found {audioFileCount} audio files"
        : "No audio files found - will use browser TTS");

var confirmationService = new ConfirmationService();
var audioService = new AudioService(audioDir);
var speechService = new SpeechRecognitionService(modelPath);
speechService.Initialize(startupLog);

builder.Services.AddSingleton(startupLog);
builder.Services.AddSingleton(flowService);
builder.Services.AddSingleton(confirmationService);
builder.Services.AddSingleton(audioService);
builder.Services.AddSingleton(speechService);
builder.Services.AddSingleton<FlowWebSocketHandler>();

var app = builder.Build();

// Statiska filer fran wwwroot/
app.UseDefaultFiles();
app.UseStaticFiles();

// Servera audio-filer
if (Directory.Exists(audioDir))
{
    app.UseStaticFiles(new StaticFileOptions
    {
        FileProvider = new PhysicalFileProvider(audioDir),
        RequestPath = "/audio"
    });
}

// WebSocket
app.UseWebSockets();
app.Map("/ws", async (HttpContext context, FlowWebSocketHandler handler) =>
{
    if (context.WebSockets.IsWebSocketRequest)
    {
        var ws = await context.WebSockets.AcceptWebSocketAsync();
        await handler.HandleAsync(ws);
    }
    else
    {
        context.Response.StatusCode = 400;
    }
});

// API-endpoint for flows (valfritt, for debugging)
app.MapGet("/api/flows", (FlowService fs) => fs.Flows);

Console.WriteLine("\n  A320 Flow Trainer - Web UI");
Console.WriteLine("  http://localhost:5320\n");

// Oppna browser
try
{
    Process.Start(new ProcessStartInfo
    {
        FileName = "http://localhost:5320",
        UseShellExecute = true
    });
}
catch { }

app.Run();
