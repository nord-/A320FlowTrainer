# A320 Flow Trainer

Ett kommandoradsbaserat träningsprogram för Airbus A320/321 cockpit flows. Programmet läser upp varje checklist-item och lyssnar efter din bekräftelse innan det går vidare.

## Innehåll

- `A320FlowTrainer/` - C# konsolapplikation
- `flows.json` - Strukturerad data med alla flows
- `audio_files.json` - Lista på ljudfiler som behövs
- `generate_audio_piper.py` - Script för att generera ljud med Piper TTS

## Setup

### Snabbstart (rekommenderat)

Kör setup-skriptet som installerar allt automatiskt:

```powershell
.\setup.ps1
```

Skriptet gör följande:
1. Installerar .NET 10 SDK (via winget)
2. Laddar ner Vosk-modell för röststyrning
3. Laddar ner Piper röstmodell för ljudgenerering
4. Publicerar appen som standalone exe med alla beroenden

Den publicerade appen hamnar i `A320FlowTrainer\bin\Release\net10.0-windows\win-x64\publish\`.

### Manuell setup

Om du föredrar att installera manuellt:

1. **Installera .NET 10 SDK**: `winget install Microsoft.DotNet.SDK.10`
2. **Ladda ner Vosk-modell** till `A320FlowTrainer/model/`:
   [vosk-model-small-en-us-0.15](https://alphacephei.com/vosk/models/vosk-model-small-en-us-0.15.zip)
3. **Bygg och kör**: `cd A320FlowTrainer && dotnet run`

För standalone-publicering, kopiera `model/`-mappen till publish-katalogen (bredvid .exe-filen). `flows.json` och `audio/` kopieras automatiskt vid build.

### Generera om ljudfiler

Färdiga ljudfiler (.wav) finns redan i `audio/`. Du behöver bara generera om dem om du ändrar flows.

```bash
python generate_audio_piper.py --model tools/piper/en_US-joe-medium.onnx
```

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
