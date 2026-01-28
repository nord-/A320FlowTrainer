# A320 Flow Trainer

Ett kommandoradsbaserat träningsprogram för Airbus A320/321 cockpit flows. Programmet läser upp varje checklist-item och lyssnar efter din bekräftelse innan det går vidare.

## Innehåll

- `A320FlowTrainer/` - C# konsolapplikation
- `flows.json` - Strukturerad data med alla flows
- `audio_files.json` - Lista på ljudfiler som behövs
- `generate_audio_gtts.py` - Script för att generera ljud med Google TTS
- `generate_audio_piper.py` - Script för att generera ljud med Piper TTS

## Setup

### 1. Generera ljudfiler

Välj ett av alternativen:

**Alternativ A: Google TTS (enklast)**
```bash
pip install gtts
python generate_audio_gtts.py
```
Detta skapar .mp3-filer i `audio/` mappen.

**Alternativ B: Piper TTS (bättre kvalitet)**
```bash
# Ladda ner Piper och en röstmodell (t.ex. Joe)
# https://github.com/rhasspy/piper/releases
# https://huggingface.co/rhasspy/piper-voices/tree/main/en/en_US/joe/medium

python generate_audio_piper.py --model path/to/en_US-joe-medium.onnx
```
Detta skapar .wav-filer i `audio/` mappen.

### 2. Bygg och kör C#-appen

```bash
cd A320FlowTrainer
dotnet build
dotnet run
```

Eller publicera som standalone:
```bash
dotnet publish -c Release -r win-x64 --self-contained
```

### 3. Kopiera filer

Kopiera följande till samma mapp som .exe-filen:
- `flows.json`
- `audio/` mappen med alla ljudfiler

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

- Windows (för System.Speech)
- .NET 8.0 eller senare
- Mikrofon (för röstinmatning)

## Felsökning

**"Speech recognition not available"**
- Kontrollera att du har en mikrofon ansluten
- Kör programmet som administratör
- Windows Speech Recognition måste vara installerat

**Inget ljud spelas**
- Kontrollera att `audio/` mappen finns
- Kontrollera att ljudfilerna har rätt namn
- Som fallback använder programmet Windows TTS

## Licens

Flows baserade på Aerosoft's dokumentation. Endast för simulatorträning.
