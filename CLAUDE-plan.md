# BlackJack Buttler — Großer Änderungs- & Bugfix-Plan (v1.7.0.9)

## Context

Dies ist ein umfangreiches Änderungs- und Bugfix-Paket für das BlackJack Buttler Dalamud-Plugin. Es adressiert 12 unabhängige Arbeitspakete: Statistik-Berechnung mit Bank-Abzug und House-Bank, ein kompakteres Round-Log-Format, leere Hände in DrawLogic nach einer Runde, ein Recall-Button für untätige Spieler, feinere Delay-Schritte mit Snapping, ein größeres/resizables Update-Popup, automatisches Nachziehen fehlender Default-Commands/Messages beim Start, ein neuer Standard-Preset-Pack (alt/neu wählbar beim Hard Reset), Beseitigung verbliebener deutscher UI-Texte, Aufsplitten der Preset-Option "Commands & Own Buttons" in zwei Checkboxen, stabile Preset-Identifikation via interner ID mit grüner Hervorhebung, und ein zeitlich begrenzter "Start Bank"-Button beim Aktivieren des Group Detectors.

Nach Abschluss: Version-Bump auf **v1.7.0.9** in allen vier Stellen (`.json`, `.csproj`, `README.md`, `CLAUDE.md`).

---

## 1. Stats: Bank-Abzug-Option + House Bank

- `config.cs`: `StatsSubtractPlayerBanks = true`, `StatsHouseBank = 0`
- `win.09.stats.cs:278–294` (DrawCalculation):
  - `bankSum = StatsSubtractPlayerBanks ? sum(PlayerState.Bank) : 0`
  - `diff = nowBank - startBank - bankSum - houseBank`
  - `profit = diff - tips`
  - Payout%/Stundenlohn unverändert
  - `finalOutcome = totalOutcome + houseBank` (House Bank kommt am Ende wieder drauf)
- `win.06.settings.cs` Tab **System**: Checkbox + InputLong
- `win.09.stats.cs` Display: neue Zeilen "Player banks", "House bank", "Return to manager"

## 2. Round-Logs: Kompaktes Format

- `manager.roundlog.cs`: Rewrite `BuildDealerLines` / `BuildPlayerLines`
- `FormatGil`: immer `NNN,NNN,NNN` mit Leerzeichen-Padding (z.B. `    ,   , 50`)
- Zeilenformate:
  - `[ Dealer | <cards> <score> ]`
  - `    [ Name@World          | <bankStart> | <bankAfterBet> | <cards/actions> <score> | <bankEnd> ]`
  - `    [ Name@World          | ---------------------------- Paused ---------------------------- ]`
  - `[ Name@World          | <<<<TRADE<<<< | <before> | <after> ]`
  - `[ Name@World          | >>>>TRADE>>>> | <before> | <after> ]`
  - Split-Block mit HAND 1/HAND 2
- Score-Label: `17`, `BJ`, `nBJ`, `dBJ`, `CH`, `SPLIT`, `BUST`
- Name-Feld: ca. 24 Zeichen, links-bündig
- `manager.trade.cs`: Trade-Buffer pro Runde
- `PersistentRoundEntry`: Feld `List<string> TradeLines`
- Pausierte Spieler (IsActivePlayer=false) in AddRound aufnehmen
- `win.01.main.cs`: X-Button entfernt Spieler nur aus aktiver Liste, Bank bleibt; echtes Remove erst bei Party-Leave + Bank=0

## 3. DrawLogic: Leere Hände in Sammelphase

- `win.14.drawlogic.cs` `ExecuteDrawLogic`: Wenn `GameEngine.CurrentPhase == Waiting` → Deep-Clone der PlayerList mit `Hands = []`

## 4. Recall-Button

- `config.cs`: `RecallUnlockSeconds = 20f`
- `command.executor.cs`: static `LastStateGroupName`, `LastStateTargetName`, `LastStateFiredAt` (gesetzt bei State-Gruppen StateHSDS/HSD/HS)
- `win.01.main.cs`: Button nach Hit/Stand/DD/Spl, disabled bis Timer abgelaufen, Countdown im Label
- `win.06.settings.cs` Tab **Gameplay**: Slider 5–120s

## 5. Delay-Minimum 0.01 + Snapping

- `config.cs`: `DelaySecondSnapping = true`
- `win.04.commands.cs:210–218`: `DragFloat` mit Min 0.01, v_speed=0.01, Format `"%.2fs"`
- Snap-Logik: wenn `DelaySecondSnapping` aktiv und new-Wert <0.2 von ganzem Int UND alter Wert >0.2 entfernt war → snap auf Int
- `win.10.ownbuttons.cs`: gleiche Änderung
- `win.06.settings.cs` Tab **Gameplay**: Checkbox
- Bestehende Werte unangetastet

## 6. Update-Popup

- `win.15.updatepopup.cs:29–43`:
  - `Size = (900, 760)` (3x breit, 2x hoch)
  - `NoResize` entfernen
  - `SizeCondition = FirstUseEver`
  - Pos-Offset anpassen
- `CurrentChangelog` aktualisieren (v1.7.0.9 Änderungen)

## 7. Default-Nachzug beim Start

- `manager.defaults.migration.cs`: neue Funktion `EnsureProtectedEntriesExist(Configuration)`:
  - Für jedes Default-Message-Batch/CommandGroup: wenn in User-Config fehlt → aus Defaults hinzufügen
  - Bestehende Einträge NICHT anrühren
- Aufruf am Ende von `RunMigration()`

## 8. Neuer Standard-Preset-Pack (V2)

- Neue Datei `network/manager.defaults.v2.cs` mit dem vom User gelieferten JSON-Blob
- Accessor `DefaultsManagerV2.GetContainer()`
- `manager.defaults.migration.cs`: `SeedAllDefaultsFromV2()`
- Default-Seeding für neuen User → v2
- Hard-Reset-UI in `win.02.messages.cs` + `win.04.commands.cs`: Popup mit 2 Optionen "Old" / "New (recommended)"
- Default-Nachzug (#7) nutzt v2

## 9. Deutsche UI-Texte → Englisch

- `win.04.commands.cs:127` `"Gruppe {N}"` → `"Group {N}"`
- `win.04.commands.cs:129` `"Iterativ"` / `"Zufällig"` → `"Iterative"` / `"Random"`
- `win.04.commands.cs:163` (Tooltip übersetzen)
- Grep-Pass über alle .cs für weitere deutsche Marker

## 10. Presets: Commands & Own Buttons split

- `config.cs` `PresetEntry`:
  - `ApplyStandardCommands = true` (neu)
  - `ApplyOwnButtons = true` (neu)
  - `ApplyCommands` via `[OnDeserialized]` → migrated auf beide
- `win.12.presets.cs:207–210`: zwei Checkboxen statt einer
- `ApplyPreset`: getrennt anwenden
- `RecomputePresetChangeCount`: `StandardCommandFields = [CommandGroups]`, `OwnButtonFields = [CustomCommandGroups, CustomButtonOrder]`

## 11. Preset-ID + stabile Erkennung

- `config.cs` `PresetEntry.PresetId = Guid.NewGuid().ToString("N")`
- Load-Migration: leere IDs füllen
- `Configuration.ActivePresetId = string.Empty`
- Name-basierte Zuordnung ersetzen durch ID
- `win.12.presets.cs`: isActive via ID, grüner Hintergrund bleibt (evtl. Farbe verstärken)
- Export/Import: ID mitnehmen; bei Kollision neue ID
- `win.00.all.cs`: Title-Label über ActivePresetId

## 12. Group Detector Start-Bank-Button (30s)

- `win.01.main.cs`:
  - Feld `DateTime? _groupDetectorActivatedAt`
  - Toggle-Handler: setzt oder reset
  - Nach Detector-Button:
    ```
    if (IsRecognitionActive && !StatsManager.IsRunning
        && _activatedAt.HasValue && elapsed < 30)
    {
        ImGui.SameLine();
        if (Button($"Start Bank ({secondsLeft}s)")) {
            StatsManager.StartSession();
            _activatedAt = null;
        }
    }
    ```

---

## Umsetzungsreihenfolge

1. Config-Felder in `config.cs`
2. PresetId-Migration (#11)
3. Defaults V2 + Hard-Reset-Wahl (#8)
4. Default-Nachzug (#7)
5. Deutsche Texte (#9)
6. Delay-Slider + Snapping (#5) + Settings-UI
7. Stats Bank-Abzug + House Bank (#1)
8. Round-Log-Rewrite (#2)
9. DrawLogic leere Hände (#3)
10. Recall-Button (#4)
11. Update-Popup-Größe (#6) + Changelog
12. Preset-Checkboxen-Split (#10)
13. Group-Detector-Start-Bank (#12)
14. Version-Bump auf 1.7.0.9

## Verifikation

- `dotnet build -c Debug` und `-c Release` müssen grün sein
- Manuelle Tests im Debug-Mode (7 Mock-Players)
- Regression: Runde durchspielen ohne neue Features
- `CLAUDE.md` und `CurrentChangelog` aktualisieren
