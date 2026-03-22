# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A320 Flow Trainer - ett träningsprogram för Airbus A320/321 cockpit flows. Spelar upp checklist-items med ljud och väntar på bekräftelse via röst eller tangentbord.

## Build & Run Commands

```bash
# Bygg C#-appen
cd A320FlowTrainer
dotnet build

# Kör appen
dotnet run

# Publicera som standalone
dotnet publish -c Release -r win-x64 --self-contained
```

## Audio Generation

```bash
# Python (full path om ej i PATH)
"C:\Users\RickardNord\AppData\Local\Programs\Python\Python312\python.exe"

# Uppdatera audio_files.json efter ändringar i flows
python parse_flows_v2.py

# Generera ljud med Piper TTS
"D:\prj\A320Flows\tools\piper\piper.exe" --model "D:\prj\A320Flows\tools\piper\en_US-joe-medium.onnx"

# Eller kör scriptet
python generate_audio_piper.py --model tools/piper/en_US-joe-medium.onnx
```

## Architecture

**Data flow:**
1. `parse_flows_v2.py` → genererar `flows.json` och `audio_files.json`
2. `generate_audio_*.py` → skapar ljudfiler i `audio/`
3. `A320FlowTrainer/` → ASP.NET backend + webbfrontend (WebSocket-baserad)

**Key files:**
- `flows.json` - strukturerad flow-data (namn, items, responses)
- `audio_files.json` - lista på ljudfiler med expanderade TTS-texter
- `A320FlowTrainer/Program.cs` - ASP.NET startup, konfigurerar Kestrel + WebSocket
- `A320FlowTrainer/Models/Flow.cs` - datamodeller (Flow, FlowItem, enums)
- `A320FlowTrainer/Services/` - FlowService, ConfirmationService, AudioService, SpeechRecognitionService
- `A320FlowTrainer/WebSocket/` - FlowWebSocketHandler, FlowSession (tillstandsmaskin), Messages
- `A320FlowTrainer/wwwroot/` - Frontend (HTML/CSS/JS)

**C# dependencies:**
- .NET 10.0 Windows (Microsoft.NET.Sdk.Web)
- Vosk (speech recognition)
- NAudio (audio capture)

**Vosk model:**
- Måste laddas ner separat till `A320FlowTrainer/model/`
- Använder `vosk-model-small-en-us-0.15` (~40MB)

## Code Style

- Använd spaces, inte tabs, för indentering
- Använd CRLF line endings (Windows-standard)
- Följ principerna DRY (Don't Repeat Yourself), KISS (Keep It Simple, Stupid) och SOLID

## Notes

- Windows-only (Vosk + NAudio kräver Windows)
- Webbgränssnitt på http://localhost:5320 (auto-öppnar browser)
- Vosk för röstigenkänning (offline, server-side)
- Fallback till browser speechSynthesis om ljudfiler saknas
- Fallback till tangentbord om Vosk-modell saknas

## Workflow Preferences

- Commita direkt utan att fråga om commit-meddelande (skriv lämpligt meddelande själv)
- Bygg alltid efter kodändringar för att verifiera
- flows.json och audio/ kopieras automatiskt till output vid build
- Använd ALDRIG "cd" eller "git -C" i Bash-kommandon - lita på working directory
