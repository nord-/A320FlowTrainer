# A320 Flow Trainer

Ett kommandoradsbaserat träningsprogram för Airbus A320/321 cockpit flows. Programmet läser upp varje checklist-item och lyssnar efter din bekräftelse innan det går vidare.

## Innehåll

- `A320FlowTrainer/` - C# konsolapplikation
- `flows.json` - Strukturerad data med alla flows
- `audio_files.json` - Lista på ljudfiler som behövs
- `generate_audio_piper.py` - Script för att generera ljud med Piper TTS

## Setup

### 1. Installera .NET 10

```powershell
winget install Microsoft.DotNet.SDK.10
```

Eller ladda ner manuellt från [dotnet.microsoft.com](https://dotnet.microsoft.com/download/dotnet/10.0).

### 2. Ljudfiler

Färdiga ljudfiler (.wav) finns redan i `audio/` mappen i repot. Du behöver bara generera om dem om du ändrar flows.

För att generera om ljudfiler behövs [Piper TTS](https://github.com/rhasspy/piper) (offline, hög kvalitet):

1. Ladda ner Piper från [GitHub Releases](https://github.com/rhasspy/piper/releases)
2. Ladda ner en röstmodell (`.onnx` + `.onnx.json`), t.ex. Joe:
   [en_US/joe/medium](https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/joe/medium)
3. Generera ljudfiler:

```bash
python generate_audio_piper.py --model path/to/en_US-joe-medium.onnx
```

Flaggan `--piper` kan användas om `piper`-exen inte ligger i PATH:
```bash
python generate_audio_piper.py --model path/to/en_US-joe-medium.onnx --piper path/to/piper.exe
```

### 3. Ladda ner Vosk-modell (för röststyrning)

```powershell
cd A320FlowTrainer
mkdir model
cd model
Invoke-WebRequest -Uri https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip -OutFile vosk-model-small-en-us-0.15.zip
Expand-Archive vosk-model-small-en-us-0.15.zip -DestinationPath .
Move-Item vosk-model-small-en-us-0.15\* .
Remove-Item vosk-model-small-en-us-0.15 -Recurse
Remove-Item vosk-model-small-en-us-0.15.zip
```

### 4. Bygg och kör C#-appen

```bash
cd A320FlowTrainer
dotnet build
dotnet run
```

Eller publicera som standalone:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### 5. Kopiera filer

Kopiera följande till samma mapp som .exe-filen:
- `flows.json`
- `audio/` mappen med alla ljudfiler
- `model/` mappen med Vosk-modellen

## Användning

1. Starta programmet
2. Programmet visar nästa flow (t.ex. "COCKPIT PREPARATION FLOWS")
3. Säg flow-namnet för att starta det
4. För varje item:
   - Programmet visar item och förväntat svar
   - Spelar upp ljudfil
   - Väntar på att du säger "checked" (eller trycker Enter)
5. När alla items är klara säger programmet "Complete"
6. Fortsätt med nästa flow

### Tangentbordskommandon

| Tangent | Funktion |
|---------|----------|
| Enter/Space | Bekräfta item |
| R | Repetera aktuellt item |
| Tab/N | Hoppa till nästa flow |
| Esc | Avsluta |

### Röstkommandon

| Kommando | Funktion |
|----------|----------|
| "checked" / "check" / "confirmed" | Bekräfta item |
| "repeat" | Repetera aktuellt item |
| "quit" / "exit" / "stop" | Avsluta |

## Flows inkluderade

1. COCKPIT PREPARATION FLOWS (35 items)
2. FMGS SETUP (14 items)
3. BEFORE START FLOWS (8 items)
4. PUSHBACK & ENGINE START FLOWS (8 items)
5. AFTER START FLOWS (12 items)
6. TAXI FLOWS (5 items)
7. BEFORE TAKE OFF FLOWS (7 items)
8. AFTER TAKE OFF FLOWS (5 items)
9. 10000 FT CLIMB FLOWS (3 items)
10. PREDESCENT FLOWS (6 items)
11. 10000 FT DESCENT FLOWS (6 items)
12. LANDING FLOWS (4 items)
13. AFTER LANDING FLOWS (8 items)
14. PARKING FLOWS (7 items)
15. SECURING FLOWS (10 items)

## Anpassa

### Ändra flows
Redigera `flows.json` för att lägga till/ändra/ta bort items.

### Generera nya ljudfiler
Efter ändringar i flows, kör `parse_flows_v2.py` för att uppdatera `audio_files.json`, sedan generera ljud på nytt.

## Krav

- Windows (för System.Speech TTS)
- .NET 10.0 eller senare
- Mikrofon (för röstinmatning)
- Vosk-modell (se steg 3 ovan)

## Felsökning

**"Vosk model not found"**
- Kontrollera att `model/` mappen finns med Vosk-modellen
- Se steg 3 i Setup ovan

**"Speech recognition not available"**
- Kontrollera att du har en mikrofon ansluten
- Kontrollera att rätt mikrofon är vald som default i Windows

**Inget ljud spelas**
- Kontrollera att `audio/` mappen finns
- Kontrollera att ljudfilerna har rätt namn
- Som fallback använder programmet Windows TTS

## Licens

Flows baserade på Aerosoft's dokumentation. Endast för simulatorträning.
