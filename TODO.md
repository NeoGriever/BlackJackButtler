# TODO: Charlie/NatBJ/DirtyBJ Notification Refactoring

## Problem
Wenn Charlie/NatBJ/DirtyBJ erkannt wird, läuft der aktuelle Command (Hit/DD) via `NotifyDiceResult()` fertig weiter, dann wird `ExecuteInternalGroup()` aufgerufen (kein Dice-Support, kein Emergency-Stop). Die Spielerrunde wirkt, als würde sie sofort enden.

## Änderungen

### 1. Defaults umbenennen — `network/manager.defaults.cs`
- `"PlayerBJ"` → `"Natural BlackJack Notify"`
- `"PlayerDirtyBJ"` → `"Dirty BlackJack Notify"`
- `"PlayerCharlie"` → `"Charlie Notify"`

### 2. DiceResultHandler — `Chat/command.executor.dicehandler.cs`
- Gruppennamen aktualisieren (Zeilen 79, 139, 146, 189-190)
- `NotifyDiceResult()` → `CancelCurrentGroup()` (Zeile 162)
- `ExecuteInternalGroup(newGroup, target.Name, cfg)` → `ExecuteGroup(newGroup, target.DisplayName, cfg)` (Zeile 186)

### 3. Automatische Migration — `network/manager.defaults.migration.cs`
- `MigrateNotifyGroupNames()`: Alte Gruppennamen in Config + Snapshot umbenennen
- Aufruf in `RunMigration()` vor `MergeNewEntries`

### 4. "Migrate Configurations"-Button — `windows/win.15.updatepopup.cs`
- Größerer Button mit Empfehlungstext
- Yes/No-Modal: "This will update/replace/create the specific commands and messages for Charlie, Natural BlackJack and Dirty BlackJack. It will NOT replace modified messages."
- Logik:
  - Command-Gruppen umbenennen (alte → neue Namen)
  - Command-Gruppen erstellen/überschreiben mit Code-Defaults
  - Message-Batches: Nur überschreiben wenn unverändert (Snapshot-Vergleich)
  - DotToken-Fix (`/tell <t>` → `/tell <.>`) auch in Messages anwenden
  - Snapshot aktualisieren + Config speichern

### 5. UI-Labels — `windows/win.04.commands.cs`
- Switch-Einträge für `"PlayerBJ"` und `"PlayerDirtyBJ"` entfernen (neue Namen fallen durch Fallback)

### 6. Emergency-Stop-Labels — `windows/win.01.main.cs`
- `_groupDisplayNames` ergänzen: `"Charlie Notify"` → `"Charlie"`, `"Natural BlackJack Notify"` → `"Nat.BJ"`, `"Dirty BlackJack Notify"` → `"Dirty BJ"`

### 7. Fenster vergrößern — `windows/win.15.updatepopup.cs`
- Size 300x300 → 300x380

### 8. Version Bump (4 Dateien)
- `BlackJackButtler.json`, `BlackJackButtler.csproj`, `README.md`, `CLAUDE.md`

### 9. Changelog — `windows/win.15.updatepopup.cs`
- `CurrentChangelog` aktualisieren

## Betroffene Message-Batches (für Migrate-Button)
- `Player Charlie Messages`
- `Player BlackJack Messages`
- `Player BlackJack Messages Shout`
- `Player Dirty BlackJack Messages`

## Reihenfolge
1. `manager.defaults.cs` — RawJson Keys
2. `manager.defaults.migration.cs` — MigrateNotifyGroupNames
3. `command.executor.dicehandler.cs` — Cancel + ExecuteGroup + Namen
4. `win.15.updatepopup.cs` — Migrate-Button + Dialog + Logik + Changelog
5. `win.04.commands.cs` — Display-Names
6. `win.01.main.cs` — Emergency-Stop-Labels
7. Version Bump
8. `dotnet build`

## Technische Details
- `CancelCurrentGroup()` setzt `_cancel=true`, `_wait=false`, bricht Delays ab
- `ExecuteGroup()` setzt `_cancel=false` beim Start (Zeile 225) → kein Flag-Leak
- Alle Gruppen im `shouldCancel`-Block profitieren automatisch (auch PlayerBust, DealerBJ)
- `MigrateTellDotToken()` scannt nur Commands, nicht Messages → DotToken-Bug bei manchen Usern
