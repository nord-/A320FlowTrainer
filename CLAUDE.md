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
# Uppdatera audio_files.json efter ändringar i flows
python parse_flows_v2.py

# Generera ljud med Google TTS
pip install gtts
python generate_audio_gtts.py

# Eller med Piper TTS (bättre kvalitet)
python generate_audio_piper.py --model path/to/en_US-joe-medium.onnx
```

## Architecture

**Data flow:**
1. `parse_flows_v2.py` → genererar `flows.json` och `audio_files.json`
2. `generate_audio_*.py` → skapar ljudfiler i `audio/`
3. `A320FlowTrainer/Program.cs` → läser `flows.json`, spelar ljud, lyssnar på röst/tangentbord

**Key files:**
- `flows.json` - strukturerad flow-data (namn, items, responses)
- `audio_files.json` - lista på ljudfiler med expanderade TTS-texter
- `A320FlowTrainer/Program.cs` - all applikationslogik (single-file)

**C# dependencies:**
- .NET 8.0 Windows
- System.Speech (recognition + synthesis)
- System.Media.SoundPlayer (.wav playback)

## Notes

- Windows-only (kräver System.Speech)
- Fallback till Windows TTS om ljudfiler saknas
- Fallback till tangentbord om mikrofon saknas
