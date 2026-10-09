# Modulares FFXIV-TradeManager-System

> **Implementierungsspezifikation und C#-Referenzdesign für eine eigenständige Dalamud-Library**  
> Ziel-API: `TradeManager.Trade("SpielerName@Welt", 5_000_000)`  
> Referenz: [`kawaii/Dropbox`](https://github.com/kawaii/Dropbox) (Branch `main`, untersuchter Stand: `012f11da49d3e5b1f31599111e84631819a52940`)  
> **Status:** Quellcodeanalyse + Architekturvorschlag. Die hier entworfene Library wurde nicht gegen eine laufende FFXIV-/Dalamud-Instanz kompiliert oder live getestet. Client-abhängige Callback- und UI-Zugriffe sind als Adapter zu implementieren bzw. zu validieren.

---

## 1. Aufgabe und gewünschte Nutzung

Es soll eine **modulare, an beliebige Dalamud-Plugins anbindbare Handelsbibliothek** entstehen. Das aufrufende Plugin soll weder die FFXIV-Handelsoberfläche kennen noch Task-Schleifen, GUI-Callbacks oder Wartebedingungen selbst verwalten müssen.

```csharp
// Einmal bei der Plugin-Initialisierung:
TradeManager.Initialize(dependencies);

// Ereignisse abonnieren:
TradeManager.onTradeChange += (type, data) =>
{
    switch (type)
    {
        case 2: PluginLog.Information("Transfer gestartet"); break;
        case 3:
            PluginLog.Information(
                $"Teil-Trade {data[0]}/{data[1]}, " +
                $"bestätigt {data[2]}/{data[3]} Gil");
            break;
        case 1: PluginLog.Information("Gesamttransfer erfolgreich"); break;
        case 0: PluginLog.Warning("Transfer abgebrochen/fehlgeschlagen"); break;
    }
};

// Zu einem späteren Zeitpunkt:
TradeManager.Trade("SpielerName@Welt", 5_000_000);
```

Die API muss **beliebige positive Beträge innerhalb sinnvoller Client- und Library-Grenzen** unterstützen. Der aufzuteilende Gesamtbetrag ist nicht auf 3 Millionen begrenzt. Weil der in Dropbox implementierte **Maximalbetrag je Einzelhandel 1.000.000 Gil** beträgt, wird ein größerer Betrag in `ceil(total / 1_000_000)` Einzeltransaktionen zerlegt. Das Limit ist zentral konfigurierbar, darf aber nicht ohne Prüfung der tatsächlichen Spielgrenze angehoben werden.

**Beispiel `5_000_000`:** fünf Einzeltrades zu 1.000.000 Gil. **Beispiel `2_350_001`:** 1.000.000 + 1.000.000 + 350.001 Gil. **Beispiel `1`:** ein Trade mit 1 Gil.

Der Eventname wird aus Kompatibilitätsgründen exakt `onTradeChange` genannt, obwohl .NET-konform `TradeChanged` vorzuziehen wäre. Eine idiomatische Event-API kann parallel angeboten werden. Das vom Aufrufer vorgeschlagene `onStateChange` kann als Alias gestaltet werden.

### 1.1 Callback-Vertrag (verbindlich)

| `type` | Bezeichnung | `data` | Auslösezeitpunkt |
|---:|---|---|---|
| `0` | Cancel | `[]` | Transfer abgebrochen, endgültig fehlgeschlagen oder nach Timeout sicher beendet |
| `1` | Success | `[]` | **Alle** Einzeltrades verifiziert erfolgreich abgeschlossen |
| `2` | Start | `[]` | Transfer angenommen, validiert und gestartet |
| `3` | Progress | `[current_step_index, total_step_index, traded_amount, total_amount]` | Nach **jedem nachweislich erfolgreichen Einzeltrade** |

**Präzisierungen:**

- `current_step_index` ist **1-basiert**, nicht 0-basiert. Bei 5 Teiltransaktionen: `1`, `2`, `3`, `4`, `5`.
- `total_step_index` enthält **die Anzahl** der Teiltransaktionen (bei 5: `5`), nicht den höchsten 0-basierten Index.
- `traded_amount` bedeutet **kumulativ als erfolgreich verifiziert überwiesen**, niemals lediglich im Handelsfenster eingetragen oder angeklickt.
- `total_amount` ist der ursprüngliche gewünschte Gesamtbetrag.
- `data` für `0`, `1` und `2` ist immer eine neue oder unveränderliche **leere** Sequenz.
- `Start` kommt genau einmal, danach null bis N `Progress`-Events, anschließend genau ein terminales Event (`Success` **oder** `Cancel`).
- `Progress(N,N,total,total)` **geht** `Success` voraus. Ein angezeigter Fortschritt von 100 % ist daher von der abschließenden terminalen Statusmeldung unterscheidbar.
- Events werden kontrolliert auf dem Framework-Thread ausgelöst; sie dürfen die Handels-State-Machine nicht blockieren. Fehler einzelner Listener werden abgefangen und geloggt.

Beispiel für 2.350.001 Gil:

```text
onTradeChange(2, [])
onTradeChange(3, [1, 3, 1000000, 2350001])
onTradeChange(3, [2, 3, 2000000, 2350001])
onTradeChange(3, [3, 3, 2350001, 2350001])
onTradeChange(1, [])
```

Bei Abbruch nach Trade 2:

```text
onTradeChange(2, [])
onTradeChange(3, [1, 3, 1000000, 2350001])
onTradeChange(3, [2, 3, 2000000, 2350001])
onTradeChange(0, [])
```

Da die Minimal-API bei Cancel `[]` vorschreibt, müssen *Grund* und *Restbetrag* über einen ergänzenden Snapshot (`LastResult`, `GetSnapshot()` oder typisierte Events) abrufbar sein. Ein Abbruch darf bereits bestätigte Transaktionen nicht zurücksetzen oder automatisch erneut senden.

---

## 2. Was Dropbox tatsächlich ausführt

Die Implementierung nutzt den üblichen FFXIV-Handelsdialog, keinen direkten Geldtransfer-Endpunkt. Der Ablauf ist in vier Hauptdateien verteilt:

1. [`TradeQueueUI.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/TradeQueueUI.cs): Zerlegung in max. 1-Million-Gil-Einträge, Start der Queue.
2. [`TradeTask.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/TradeTask.cs): Spielerziel setzen, `/trade`, auf Fenster warten, Gil setzen, Freigabe steuern.
3. [`Dropbox.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/Dropbox.cs): Framework-Update, Klick auf Trade-Button, finalen `SelectYesno` bestätigen, Chatmeldungen lesen.
4. [`ClickGeneric.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/ClickGeneric.cs): ClickLib-Helfer für Trade-Button.

### 2.1 Zerlegung der Summe

Dropbox zerlegt eine Nutzereingabe mittels:

```csharp
while (gil > 1_000_000)
{
    n.Gil = 1_000_000;
    gil -= 1_000_000;
    C.TradeQueue.Add(n.JSONClone());
}
if (gil > 0)
{
    n.Gil = gil;
    C.TradeQueue.Add(n.JSONClone());
}
```

Das neue Modul sollte stattdessen eine reine Planungsfunktion verwenden, die *nicht* unmittelbar UI- oder Queue-Nebenwirkungen hat:

```csharp
public static IReadOnlyList<int> Plan(long total, int cap = 1_000_000)
{
    if (total <= 0) throw new ArgumentOutOfRangeException(nameof(total));
    if (cap <= 0 || cap > 1_000_000)
        throw new ArgumentOutOfRangeException(nameof(cap));

    var steps = checked((total + cap - 1) / cap); // overflow-sicherer: siehe unten
    if (steps > 100_000)
        throw new ArgumentOutOfRangeException(nameof(total), "Too many trades");

    var result = new List<int>(checked((int)steps));
    long remaining = total;
    while (remaining > 0)
    {
        int amount = checked((int)Math.Min(remaining, cap));
        result.Add(amount);
        remaining -= amount;
    }
    return result;
}
```

**Produktionskorrektur:** Bei sehr großen `long`-Werten kann `total + cap - 1` überlaufen. Verwende stattdessen `1 + (total - 1) / cap`:

```csharp
long stepCount = 1 + (total - 1) / cap;
```

In der endgültigen Implementierung unten wird ausschließlich diese sichere Variante genutzt.

### 2.2 Ablauf pro Einzeltrade (Original)

Die Dropbox-Queue enthält in genau dieser Reihenfolge:

```csharp
P.TaskManager.Enqueue(() => TradeTask.ConfirmAllowed = false);
P.TaskManager.Enqueue(() => UseTradeOn(entry.Player));
P.TaskManager.Enqueue(WaitUntilTradeOpen);
P.TaskManager.Enqueue(OpenGilInput);
P.TaskManager.Enqueue(() => SetNumericInput(entry.Gil));
P.TaskManager.DelayNext(15, true);
P.TaskManager.Enqueue(() => TradeTask.ConfirmAllowed = true);
P.TaskManager.Enqueue(WaitUntilTradeNotOpen);
P.TaskManager.Enqueue(() => C.TradeQueue.RemoveAll(x => x.GUID == entry.GUID));
P.TaskManager.DelayNext(15, true);
```

`UseTradeOn(player)`:

- Sucht ein `PlayerCharacter`, das `IsTargetable` ist und dessen `new Sender(pc).ToString()` dem gewünschten `Name@World` entspricht.
- Ist das Ziel noch nicht angewählt, setzt `Svc.Targets.Target = targetPlayer`; Task bleibt offen.
- Ist es angewählt, wartet auf `FrameThrottler.Throttle("TaskThrottle", 4)` und `EzThrottler.Throttle("TradeOpen", 3000)` und sendet `Chat.Instance.SendMessage("/trade")`.
- Danach beendet es diesen Task; **auf das erfolgreiche Öffnen** wartet ein separater Task über `Svc.Condition[ConditionFlag.TradeOpen]`.

`OpenGilInput()` wartet auf ein bereites `Trade`-Addon und feuert:

```csharp
Callback.Fire(addon, true, 2, Callback.ZeroAtkValue);
```

`SetNumericInput(amount)` wartet auf ein bereites `InputNumeric`-Addon und feuert:

```csharp
Callback.Fire(addon, true, amount);
```

`Framework_Update` in `Dropbox.cs` läuft parallel und klickt den freigegebenen Trade-Button:

```csharp
var tradeButton = (AtkComponentButton*)addon->UldManager.NodeList[3]->GetComponent();
if (TradeTask.IsActive) ready = TradeTask.ConfirmAllowed;

if (ready
    && EzThrottler.Check("TradeArtificialThrottle")
    && FrameThrottler.Check("TradeArtificialThrottle")
    && tradeButton->IsEnabled
    && EzThrottler.Throttle("Delay", 200)
    && EzThrottler.Throttle("ReadyTrade", 2000))
{
    new ClickGeneric("Trade", (nint)addon).ClickButton(tradeButton);
}
```

Das nächste Bestätigungsfenster wird nicht anhand seiner Reihenfolge, sondern am *lokalisierten Text* identifiziert:

```csharp
string tradeText = Svc.Data.GetExcelSheet<Addon>()
    .GetRow(102223).Text.ExtractText();

// Alle SelectYesno-Instanzen prüfen, NodeList[15] lesen;
// nur bei exakt passendem Text:
ClickSelectYesNo.Using((nint)selectYesNoAddon).Yes();
```

Die künstlichen Time-/Frame-Gates des Originals:

| Name | Originalwert | Einordnung |
|---|---:|---|
| `TaskThrottle` | 4 Frames | Drosselung für Target/Input-Tasks |
| `TradeOpen` | 3000 ms | Drosselung des `/trade`-Kommandos |
| `TradeArtificialThrottle` | `Config.Delay` = 1000 ms | bei fehlender Bereitschaft fortlaufend zurückgesetzt |
| `TradeArtificialThrottle` | 8 Frames | zusätzliches Gate |
| `Delay` | 200 ms | gemeinsamer Throttle für Confirm-Aktionen |
| `ReadyTrade` | 2000 ms | Drosselung Trade-Button |
| `SelectYes` | 2000 ms | Drosselung Ja/Nein-Dialog |
| `DelayNext(15, true)` | 15 (ECommons-Argument) | genaue Zeit-/Queue-Semantik im gebundenen ECommons-Commit prüfen |

**Wichtige Fehlerquelle:** `WaitUntilTradeNotOpen()` prüft ausschließlich `!TradeOpen` – also auch bei einem abgebrochenen Handel. Dropbox löscht danach trotzdem den Queue-Eintrag. Die neue Library darf diesen Mechanismus **nicht** als Erfolgsnachweis übernehmen.

**Weitere Fehlerquelle:** Die automatische finale Ja/Nein-Bestätigung prüft im Original nicht erneut `ConfirmAllowed`. Der neue Adapter soll nur einen zur *eigenen aktiven Session* passenden Dialog bestätigen, nachdem Empfänger und Betrag validiert wurden.

---

## 3. Library-Architektur

Die Lösung trennt strikt zwischen **öffentlichem Contract**, **Transfer-Engine** und **Dalamud-spezifischer Ein-/Ausgabe**:

```text
Host-Plugin
  ├─ TradeManager.Trade("Name@World", amount)
  ├─ TradeManager.onTradeChange += handler
  └─ TradeManager.Cancel()
                   │
                   ▼
TradeManager (Facade / Lifetime)
  ├─ Validierung / Single-Flight / öffentliche Events
  ├─ TradeSession (ID, Empfänger, Plan, bestätigt, Zustand)
  ├─ TradePlanner (reine Betragszerlegung)
  └─ TradeEngine (State Machine, Timeout, Fehlerübergänge)
                   │
                   ▼
ITradeGameAdapter (isolierte Game-I/O-Grenze)
  ├─ Dalamu​dTradeAdapter
  ├─ Spieler finden/targeten und /trade senden
  ├─ Trade/ InputNumeric / SelectYesno / Klicks
  ├─ Gil-Angebot readback, Empfänger prüfen
  └─ Chat-Events / TradeOpen / Abbruch-Signale
                   │
                   ▼
Dalamud + ECommons + ClickLib + FFXIVClientStructs
```

**Empfohlene Projektstruktur:**

```text
ModularTrade/
├── ModularTrade.csproj
├── Public/
│   ├── TradeManager.cs
│   ├── TradeEventType.cs
│   ├── TradeSnapshot.cs
│   ├── TradeOptions.cs
│   └── TradeResult.cs
├── Core/
│   ├── TradePlanner.cs
│   ├── TradeEngine.cs
│   ├── TradeSession.cs
│   └── TradeState.cs
├── Adapters/
│   ├── ITradeGameAdapter.cs
│   ├── DalamudTradeAdapter.cs
│   └── TradeUiSelectors.cs
└── Tests/
    ├── TradePlannerTests.cs
    ├── TradeEngineTests.cs
    └── FakeTradeGameAdapter.cs
```

Das Ziel ist eine **einbettbare Library** (referenziertes Projekt / DLL / Quellcode-Modul), keine eigenständige ImGui-Oberfläche. Die Host-Plugins bringen bei Bedarf selbst UI und Konfiguration mit.

### 3.1 Nebenläufigkeitsregel

Die erste Version soll **genau eine aktive Transfer-Session pro Client** zulassen. Mehrere gleichzeitige Handelsfenster sind weder nötig noch zuverlässig zu korrelieren. `Trade()` gibt bei bereits laufendem Transfer einen wohldefinierten Fehler zurück, anstatt still eine zweite Session einzureihen. Eine spätere FIFO-Queue kann auf Facade-Ebene ergänzt werden.

### 3.2 API-Aufteilung

Es gibt zwei Ebenen:

1. **Kompatibilitäts-API** gemäß Aufgabenstellung: `TradeManager.Trade(...)`, `TradeManager.onTradeChange`, Payload `long[]`.
2. **Typisierte API** für Produktionscode: `TradeChanged` mit `TradeSnapshot`, `LastResult`, `Cancel()`, optional `TradeAsync()` auf Basis eines `TaskCompletionSource`.

Ein `long[]` statt `int[]` ist erforderlich, damit kumulierte Beträge oberhalb `int.MaxValue` korrekt dargestellt werden können.

---

## 4. Öffentlicher Contract: Datentypen

Die folgenden Beispiele sind ein **zusammengehöriger Architekturentwurf** (in einzelne Dateien aufteilen). Die Host-/Dalamud-Integration muss passend zur im Projekt verwendeten API-Version vervollständigt werden.

### `TradeEventType.cs`, `TradeState.cs`

```csharp
namespace ModularTrade;

public enum TradeEventType
{
    Cancel = 0,
    Success = 1,
    Start = 2,
    Progress = 3
}

public enum TradeState
{
    Idle,
    FindingTarget,
    Targeting,
    OpeningTrade,
    WaitingForTrade,
    OpeningGilInput,
    WaitingForNumericInput,
    SettingGil,
    VerifyingOffer,
    ConfirmingTrade,
    ConfirmingFinalDialog,
    AwaitingOutcome,
    BetweenTrades,
    Completed,
    Cancelled,
    Failed
}

public enum TradeFailureReason
{
    None,
    UserCancelled,
    RemoteCancelled,
    TargetUnavailable,
    TargetChanged,
    InsufficientGil,
    UnexpectedOffer,
    UnexpectedDialog,
    Timeout,
    AdapterError,
    UnknownOutcome
}
```

### `TradeOptions.cs` und `TradeSnapshot.cs`

```csharp
namespace ModularTrade;

public sealed record TradeOptions
{
    public int MaxGilPerTrade { get; init; } = 1_000_000;
    public int MaxTradeCount { get; init; } = 10_000;
    public TimeSpan FindTargetTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan OpenTradeTimeout { get; init; } = TimeSpan.FromSeconds(30);
    public TimeSpan UiTimeout { get; init; } = TimeSpan.FromSeconds(10);
    public TimeSpan OutcomeTimeout { get; init; } = TimeSpan.FromSeconds(45);
    public TimeSpan BetweenTradesDelay { get; init; } = TimeSpan.FromMilliseconds(250);
    public TimeSpan CommandCooldown { get; init; } = TimeSpan.FromSeconds(3);
    public TimeSpan ButtonCooldown { get; init; } = TimeSpan.FromSeconds(2);
    public TimeSpan DialogCooldown { get; init; } = TimeSpan.FromSeconds(2);
}

public sealed record TradeSnapshot(
    Guid SessionId,
    string Recipient,
    TradeState State,
    int CurrentStep,
    int TotalSteps,
    long ConfirmedAmount,
    long TotalAmount,
    TradeFailureReason FailureReason = TradeFailureReason.None)
{
    public long RemainingAmount => TotalAmount - ConfirmedAmount;
}

public sealed record TradeResult(
    bool Succeeded,
    TradeSnapshot FinalSnapshot);
```

**Hinweis:** Die Timeout-/Cooldown-Werte in `TradeOptions` sind **Entwurfsdefaults**, keine nachgewiesenen Werte von Dropbox. Insbesondere darf ein Timeout bei unklarem Trade-Ergebnis nicht in einen automatischen Retry derselben Zahlung münden.

### `TradePlanner.cs` – getestete, reine Zerlegung

```csharp
namespace ModularTrade;

public static class TradePlanner
{
    public static IReadOnlyList<int> CreatePlan(
        long totalGil,
        int maxGilPerTrade = 1_000_000,
        int maxTradeCount = 10_000)
    {
        if (totalGil <= 0)
            throw new ArgumentOutOfRangeException(nameof(totalGil));

        if (maxGilPerTrade is <= 0 or > 1_000_000)
            throw new ArgumentOutOfRangeException(nameof(maxGilPerTrade));

        if (maxTradeCount <= 0)
            throw new ArgumentOutOfRangeException(nameof(maxTradeCount));

        // Keine Addition von (maxGilPerTrade - 1) => kein long-Overflow.
        long stepCount = 1 + (totalGil - 1) / maxGilPerTrade;
        if (stepCount > maxTradeCount)
            throw new ArgumentOutOfRangeException(
                nameof(totalGil),
                $"Transfer would require {stepCount} trades; " +
                $"configured maximum is {maxTradeCount}.");

        var amounts = new List<int>((int)stepCount);
        long remaining = totalGil;

        while (remaining > 0)
        {
            int next = (int)Math.Min(remaining, maxGilPerTrade);
            amounts.Add(next);
            remaining -= next;
        }
        return amounts;
    }
}
```

### `TradeSession.cs` – Daten einer laufenden Operation

```csharp
namespace ModularTrade;

internal sealed class TradeSession
{
    public Guid Id { get; } = Guid.NewGuid();
    public string Recipient { get; }
    public long TotalAmount { get; }
    public IReadOnlyList<int> Plan { get; }
    public int CompletedSteps { get; private set; }
    public long ConfirmedAmount { get; private set; }

    public int CurrentStep => Math.Min(CompletedSteps + 1, Plan.Count);
    public int CurrentTradeAmount => Plan[CompletedSteps];
    public bool IsComplete => CompletedSteps == Plan.Count;

    public TradeSession(string recipient, long total, IReadOnlyList<int> plan)
    {
        Recipient = recipient;
        TotalAmount = total;
        Plan = plan;
        if (plan.Count == 0) throw new ArgumentException("Empty plan", nameof(plan));
    }

    public void MarkCurrentTradeVerified()
    {
        if (IsComplete) throw new InvalidOperationException("Already completed");
        ConfirmedAmount = checked(ConfirmedAmount + Plan[CompletedSteps]);
        CompletedSteps++;
    }

    public TradeSnapshot Snapshot(
        TradeState state,
        TradeFailureReason reason = TradeFailureReason.None)
        => new(
            Id,
            Recipient,
            state,
            CompletedSteps,
            Plan.Count,
            ConfirmedAmount,
            TotalAmount,
            reason);
}
```

`CompletedSteps` ist die Anzahl **verifizierter** Teiltransaktionen. Bei einem laufenden Trade gilt daher `CurrentStep = CompletedSteps + 1`; beim Progress-Event liefert `CompletedSteps` den gerade abgeschlossenen 1-basierten Index.

---

## 5. Game-I/O als Adapter isolieren

Die öffentliche Library darf keine direkten Kenntnisse über `AtkUnitBase*`, NodeList-Indizes oder die aktuelle Dalamud-Version benötigen. Nur der Adapter kennt diese Details.

### `ITradeGameAdapter.cs`

```csharp
namespace ModularTrade;

public enum GameTradeOutcome
{
    None,
    Succeeded,
    Cancelled,
    Unknown
}

public interface ITradeGameAdapter : IDisposable
{
    // Alle Methoden werden auf dem Dalamud-Framework-Thread aufgerufen.
    DateTimeOffset Now { get; }
    bool IsTradeOpen { get; }
    bool IsNumericInputOpen { get; }
    bool IsTradeUiReady { get; }
    bool IsTradeButtonEnabled { get; }
    bool IsFinalTradeDialogVisible { get; }

    bool HasSufficientGil(int amount);
    bool IsCorrectRecipient(string recipient);
    bool FindAndTargetPlayer(string recipient);

    // Nicht blockierende Einzelaktionen; true heißt: Aktion angestoßen,
    // NICHT: serverseitiger Handel erfolgreich.
    bool TrySendTradeRequest();
    bool TryOpenGilInput();
    bool TrySetGil(int amount);
    bool TryReadOfferedGil(out int amount);
    bool TryClickTradeButton();
    bool TryConfirmFinalDialog();

    // Outcome nur mit aktivem Session-Kontext korrelieren.
    // Bei Mehrdeutigkeit Unknown zurückgeben, niemals Success erfinden.
    GameTradeOutcome ConsumeOutcome();
    void ResetObservedOutcome();
    void Log(string message, Exception? error = null);
}
```

### Warum `ConsumeOutcome()` erforderlich ist

Dropbox registriert `Svc.Chat.ChatMessage` und beobachtet Systemmeldungen wie `Trade complete.` oder `Trade canceled.`. Der Originalcode verwendet diese Nachrichten ausschließlich für Benachrichtigungen; seine Queue wartet nur auf `!TradeOpen`. Für einen verlässlichen Bilanzierungsfortschritt benötigt die neue Engine **beides**:

```text
TradeOpen == false  +  passendes Erfolgsereignis
                              │
                              └──> bestätigten Betrag verbuchen
```

Die bloße Meldung `Trade complete.` allein sollte ebenfalls nicht blind als Nachweis für die **aktuelle** Session gelten. Das Adapterdesign muss das Ergebnis an einen ausstehenden, eindeutigen Handelsvorgang koppeln (Fenster-/Zustandsfolge, Empfängerprüfung, Zeitfenster; idealerweise zusätzliche Transaktionsindikatoren). Falls diese Korrelation nicht sicher hergestellt werden kann, wird `UnknownOutcome` gemeldet und gestoppt. Eine neue Sitzung darf kein altes Chatereignis übernehmen.

---

## 6. Dalamud-Adapter: konkreter Ablauf und Codefragmente

> **Versionshinweis:** Die folgenden Fragmente entsprechen den im Dropbox-Repository beobachteten API-Aufrufen. Für eine neue Dalamud-Version werden die Schnittstellen im Adapter geprüft und ggf. angepasst. Dieser Abschnitt ist absichtlich kein scheinbar garantiert kompilierbarer Drop-in-Adapter: Das Zusammenführen mit Services, Unsafe-Code, Pointertypen und den konkreten Bibliotheksständen muss im Zielprojekt erfolgen.

### 6.1 Empfänger suchen, anvisieren, `/trade` ausführen

```csharp
using Dalamud.Game.ClientState.Objects.SubKinds;
using ECommons.ChatMethods;

// Im DalamudTradeAdapter:
public bool FindAndTargetPlayer(string recipient)
{
    var player = Svc.Objects
        .OfType<PlayerCharacter>()
        .FirstOrDefault(pc =>
            pc.IsTargetable &&
            new Sender(pc).ToString() == recipient);

    if (player is null)
        return false;

    if (Svc.Targets.Target?.Address != player.Address)
    {
        Svc.Targets.Target = player;
        return false; // Im nächsten Framework-Update erneut prüfen.
    }

    return true;
}

public bool TrySendTradeRequest()
{
    // Die Engine gewährleistet CommandCooldown und korrekte Zielwahl.
    Chat.Instance.SendMessage("/trade");
    return true;
}

public bool IsTradeOpen => Svc.Condition[ConditionFlag.TradeOpen];
```

Die Suche nach `Name@World` ist ein Stringvergleich und setzt voraus, dass das Format von `Sender(pc)` tatsächlich zum Aufruferformat passt. Eine optionale `NormalizeRecipient`-Routine sollte Weltname und Spielernamen separat validieren, aber **keine unzuverlässige Ähnlichkeitssuche** verwenden. Nicht auf einen bloßen Teilnamen oder ein anderes Ziel ausweichen.

### 6.2 Eingabe öffnen und Wert setzen

```csharp
using ECommons.Automation;
using FFXIVClientStructs.FFXIV.Component.GUI;

public unsafe bool TryOpenGilInput()
{
    if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) ||
        !IsAddonReady(addon))
        return false;

    Callback.Fire(addon, true, 2, Callback.ZeroAtkValue);
    return true;
}

public unsafe bool TrySetGil(int amount)
{
    if (amount is <= 0 or > 1_000_000)
        throw new ArgumentOutOfRangeException(nameof(amount));

    if (!TryGetAddonByName<AtkUnitBase>("InputNumeric", out var addon) ||
        !IsAddonReady(addon))
        return false;

    Callback.Fire(addon, true, amount);
    return true;
}
```

**Wichtig:** Das erfolgreiche Abschicken des Callbacks ist nicht die Bestätigung, dass das Spiel den Betrag übernommen hat. Unmittelbar vor dem ersten Handels-Klick muss `TryReadOfferedGil(...)` den UI-Wert zurücklesen und mit `amount` vergleichen. Die Position des **eigenen** Gil-Werts muss aus dem aktuellen Trade-Addon und der verwendeten Clientversion ermittelt werden; `NodeList[6]` in Dropbox wird für die **gegnerische** Gil-Offerte im Auto-Confirm-Pfad verwendet und darf nicht ungeprüft als eigener Betrag angenommen werden.

### 6.3 Handels-Button und Dialog

```csharp
using ClickLib.Bases;
using FFXIVClientStructs.FFXIV.Component.GUI;

internal unsafe sealed class ClickTrade : ClickBase<ClickTrade, AtkUnitBase>
{
    public ClickTrade(string name, nint addon) : base(name, addon) { }

    public void Click(AtkComponentButton* button)
        => ClickAddonButton(button, 0);
}

public unsafe bool TryClickTradeButton()
{
    if (!TryGetAddonByName<AtkUnitBase>("Trade", out var addon) ||
        !IsAddonReady(addon))
        return false;

    // Dropbox main: Trade-Button über NodeList[3].
    var button = (AtkComponentButton*)
        addon->UldManager.NodeList[3]->GetComponent();

    if (button is null || !button->IsEnabled)
        return false;

    new ClickTrade("Trade", (nint)addon).Click(button);
    return true;
}
```

Der finale Dialog muss über den **passenden lokalisierten Text** und eine bereitstehende `SelectYesno`-Instanz erkannt werden:

```csharp
// Exemplarische Client-Implementierung, auf Addon-Layout prüfen:
private unsafe AtkUnitBase* FindTradeConfirmationDialog(string tradeText)
{
    for (int i = 1; i < 100; i++)
    {
        var addon = (AtkUnitBase*)Svc.GameGui.GetAddonByName("SelectYesno", i);
        if (addon is null) return null;
        if (!IsAddonReady(addon)) continue;

        var textNode = addon->UldManager.NodeList[15]->GetAsAtkTextNode();
        string text = MemoryHelper.ReadSeString(&textNode->NodeText)
            .ExtractText();

        if (text == tradeText)
            return addon;
    }
    return null;
}

public unsafe bool TryConfirmFinalDialog()
{
    string tradeText = Svc.Data.GetExcelSheet<Addon>()
        .GetRow(102223).Text.ExtractText();

    var dialog = FindTradeConfirmationDialog(tradeText);
    if (dialog is null) return false;

    ClickSelectYesNo.Using((nint)dialog).Yes();
    return true;
}
```

Die Beispiele setzen u. a. die ursprünglichen `global using`-Direktiven von Dropbox voraus. Notwendige Namespaces für `MemoryHelper`, `Addon`, `Svc`, `Callback`, `ClickSelectYesNo` etc. sind im konkreten Adapterprojekt zu ergänzen. Bei *allen* Pointer-Zugriffen sind Nodezahl, Node-Typ, Komponentenzeiger und Addon-Lifetime gegen die Zielversion abzusichern. Das Indexschema darf nicht als dauerhafter API-Vertrag behandelt werden.

### 6.4 Statusmeldungen erkennen

```csharp
private GameTradeOutcome _outcome = GameTradeOutcome.None;

private void OnChatMessage(
    XivChatType type,
    uint senderId,
    ref SeString sender,
    ref SeString message,
    ref bool isHandled)
{
    if (type != XivChatType.SystemMessage)
        return;

    string text = message.ToString();
    if (text == "Trade complete.")
        _outcome = GameTradeOutcome.Succeeded;
    else if (text == "Trade canceled.")
        _outcome = GameTradeOutcome.Cancelled;
}

public GameTradeOutcome ConsumeOutcome()
{
    var result = _outcome;
    _outcome = GameTradeOutcome.None;
    return result;
}
```

**Dieses Fragment ist lediglich die Message-Erkennung aus Dropbox als Ausgangspunkt. Nicht unmodifiziert als produktionssichere Outcome-Korrelation übernehmen:** Es filtert nicht auf Session, erwartete Handelsphase oder Empfänger und verwendet englische Meldungen. Eine robuste Version speichert ein Ereignis mit Zeitstempel und Session-Generation, ignoriert Meldungen außerhalb von `AwaitingOutcome`, berücksichtigt die Client-Lokalisierung und liefert bei nicht beweisbarer Zuordnung `Unknown`. Besonders kritisch: Eine zwischengespeicherte Erfolgsmeldung darf niemals nach einem neuen `/trade` für die nächste Zahlung nachträglich konsumiert werden.

---

## 7. State Machine: Steuerung statt starrer Skriptabfolge

Die Engine wird auf jedem Dalamud-Framework-Update (`Svc.Framework.Update`) **genau einmal** getickt. Jeder Tick führt höchstens eine ausgehende UI-Aktion aus und prüft Zustandswechsel. Kein `Thread.Sleep`, keine gleichzeitigen FFXIV-UI-Schreibzugriffe, keine Tasks auf ThreadPool-Threads.

### 7.1 Zustandsübergänge

| Ausgangszustand | Prüfkriterium/Aktion | Folgezustand |
|---|---|---|
| `Idle` | `Trade()` erfolgreich gestartet | `FindingTarget` |
| `FindingTarget` | Spieler gefunden und Ziel gesetzt | `Targeting` |
| `Targeting` | Ziel weiterhin korrekt; `/trade` nach Cooldown gesendet | `OpeningTrade` |
| `OpeningTrade` | `IsTradeOpen` | `WaitingForTrade` |
| `WaitingForTrade` | Trade-UI bereit, Empfänger korrekt, Guthaben genügt | `OpeningGilInput` |
| `OpeningGilInput` | `TryOpenGilInput() == true` | `WaitingForNumericInput` |
| `WaitingForNumericInput` | `IsNumericInputOpen` | `SettingGil` |
| `SettingGil` | `TrySetGil(amount)` ausgeführt | `VerifyingOffer` |
| `VerifyingOffer` | `TryReadOfferedGil() == amount`, Numeric-Dialog geschlossen | `ConfirmingTrade` |
| `ConfirmingTrade` | Trade-Button verfügbar, Freigabe sicher, geklickt | `ConfirmingFinalDialog` |
| `ConfirmingFinalDialog` | richtiger finaler Trade-Dialog erkannt, bestätigt | `AwaitingOutcome` |
| `AwaitingOutcome` | `!IsTradeOpen` **und** korreliertes Success-Event | `BetweenTrades` oder `Completed` |
| `BetweenTrades` | Mindestabstand abgelaufen | `FindingTarget` (nächster Teiltrade) |
| Jeder aktive Zustand | bestätigter Cancel / definitive Unmöglichkeit / Timeout | `Cancelled` oder `Failed` |

In einzelnen Client-Versionen kann sich die Reihenfolge von Trade-Button, Gegenseitenbestätigung und `SelectYesno` anders darstellen. Die Adapterzustände müssen den real beobachteten Clientzuständen folgen. **Insbesondere nicht unbegrenzt auf ein `SelectYesno` warten, falls der Dialog im konkreten Ablauf nicht erscheint.** Ein Guard kann beim Wechsel in `AwaitingOutcome` erkennen, dass der Handel auf andere Weise abgeschlossen wurde – aber niemals ohne verifizierbare Erfolgskorrelation buchen.

### 7.2 Kern einer tick-basierten Engine

Der folgende Code zeigt das zentrale Scheduling. Er ist absichtlich in seiner Logik ausführlich, während adapter- und versionsabhängige Details ausgelagert sind.

```csharp
namespace ModularTrade;

internal sealed class TradeEngine
{
    private readonly ITradeGameAdapter _io;
    private readonly TradeOptions _options;
    private readonly Action<TradeEventType, TradeSnapshot> _notify;

    private TradeSession? _session;
    private TradeState _state = TradeState.Idle;
    private DateTimeOffset _stateEntered;
    private DateTimeOffset _nextActionAt;
    private GameTradeOutcome _observedOutcome;

    public bool IsBusy => _session is not null;
    public TradeSnapshot? CurrentSnapshot => _session?.Snapshot(_state);

    public TradeEngine(
        ITradeGameAdapter io,
        TradeOptions options,
        Action<TradeEventType, TradeSnapshot> notify)
    {
        _io = io;
        _options = options;
        _notify = notify;
    }

    public void Start(string recipient, long amount)
    {
        if (IsBusy) throw new InvalidOperationException("Trade already active");
        if (string.IsNullOrWhiteSpace(recipient))
            throw new ArgumentException("Missing recipient", nameof(recipient));

        var plan = TradePlanner.CreatePlan(
            amount, _options.MaxGilPerTrade, _options.MaxTradeCount);

        _session = new TradeSession(recipient, amount, plan);
        _observedOutcome = GameTradeOutcome.None;
        _io.ResetObservedOutcome();
        Enter(TradeState.FindingTarget);
        Emit(TradeEventType.Start);
    }

    public void Tick()
    {
        if (_session is null) return;
        if (_io.Now < _nextActionAt) return;

        try
        {
            // Ein Timer ist immer zustandsbezogen, kein pauschaler Gesamt-Timer.
            var timeout = TimeoutFor(_state);
            if (timeout.HasValue && _io.Now - _stateEntered > timeout.Value)
            {
                End(TradeEventType.Cancel, TradeState.Failed,
                    TradeFailureReason.Timeout);
                return;
            }

            int amount = _session.CurrentTradeAmount;

            switch (_state)
            {
                case TradeState.FindingTarget:
                    if (_io.FindAndTargetPlayer(_session.Recipient))
                        Enter(TradeState.Targeting);
                    break;

                case TradeState.Targeting:
                    if (!_io.IsCorrectRecipient(_session.Recipient))
                    {
                        Enter(TradeState.FindingTarget);
                        break;
                    }
                    if (_io.TrySendTradeRequest())
                    {
                        Enter(TradeState.OpeningTrade);
                        Gate(_options.CommandCooldown);
                    }
                    break;

                case TradeState.OpeningTrade:
                    if (_io.IsTradeOpen)
                        Enter(TradeState.WaitingForTrade);
                    break;

                case TradeState.WaitingForTrade:
                    if (!_io.IsTradeUiReady) break;
                    if (!_io.IsCorrectRecipient(_session.Recipient))
                    {
                        End(TradeEventType.Cancel, TradeState.Failed,
                            TradeFailureReason.TargetChanged);
                        break;
                    }
                    if (!_io.HasSufficientGil(amount))
                    {
                        End(TradeEventType.Cancel, TradeState.Failed,
                            TradeFailureReason.InsufficientGil);
                        break;
                    }
                    Enter(TradeState.OpeningGilInput);
                    break;

                case TradeState.OpeningGilInput:
                    if (_io.TryOpenGilInput())
                        Enter(TradeState.WaitingForNumericInput);
                    break;

                case TradeState.WaitingForNumericInput:
                    if (_io.IsNumericInputOpen)
                        Enter(TradeState.SettingGil);
                    break;

                case TradeState.SettingGil:
                    if (_io.TrySetGil(amount))
                        Enter(TradeState.VerifyingOffer);
                    break;

                case TradeState.VerifyingOffer:
                    if (_io.IsNumericInputOpen) break;
                    if (!_io.TryReadOfferedGil(out var actual)) break;
                    if (actual != amount)
                    {
                        End(TradeEventType.Cancel, TradeState.Failed,
                            TradeFailureReason.UnexpectedOffer);
                        break;
                    }
                    Enter(TradeState.ConfirmingTrade);
                    break;

                case TradeState.ConfirmingTrade:
                    if (!PreConfirmChecks(amount)) break;
                    if (_io.IsTradeButtonEnabled && _io.TryClickTradeButton())
                    {
                        Enter(TradeState.ConfirmingFinalDialog);
                        Gate(_options.ButtonCooldown);
                    }
                    break;

                case TradeState.ConfirmingFinalDialog:
                    if (!PreConfirmChecks(amount)) break;
                    if (_io.IsFinalTradeDialogVisible &&
                        _io.TryConfirmFinalDialog())
                    {
                        _observedOutcome = GameTradeOutcome.None;
                        Enter(TradeState.AwaitingOutcome);
                        Gate(_options.DialogCooldown);
                    }
                    break;

                case TradeState.AwaitingOutcome:
                    // Auflaufende Meldungen erfassen, nicht blind doppelt konsumieren.
                    var outcome = _io.ConsumeOutcome();
                    if (outcome != GameTradeOutcome.None)
                        _observedOutcome = outcome;

                    if (_observedOutcome == GameTradeOutcome.Cancelled)
                    {
                        End(TradeEventType.Cancel, TradeState.Cancelled,
                            TradeFailureReason.RemoteCancelled);
                        break;
                    }
                    if (_observedOutcome == GameTradeOutcome.Unknown)
                    {
                        End(TradeEventType.Cancel, TradeState.Failed,
                            TradeFailureReason.UnknownOutcome);
                        break;
                    }
                    if (!_io.IsTradeOpen &&
                        _observedOutcome == GameTradeOutcome.Succeeded)
                    {
                        _session.MarkCurrentTradeVerified();
                        Emit(TradeEventType.Progress);

                        if (_session.IsComplete)
                        {
                            End(TradeEventType.Success, TradeState.Completed,
                                TradeFailureReason.None);
                        }
                        else
                        {
                            _io.ResetObservedOutcome();
                            _observedOutcome = GameTradeOutcome.None;
                            Enter(TradeState.BetweenTrades);
                            Gate(_options.BetweenTradesDelay);
                        }
                    }
                    break;

                case TradeState.BetweenTrades:
                    Enter(TradeState.FindingTarget);
                    break;
            }
        }
        catch (Exception ex)
        {
            _io.Log("Trade engine error", ex);
            End(TradeEventType.Cancel, TradeState.Failed,
                TradeFailureReason.AdapterError);
        }
    }

    private bool PreConfirmChecks(int expected)
    {
        if (_session is null || !_io.IsTradeOpen ||
            !_io.IsCorrectRecipient(_session.Recipient) ||
            !_io.TryReadOfferedGil(out int actual) || actual != expected)
        {
            End(TradeEventType.Cancel, TradeState.Failed,
                TradeFailureReason.UnexpectedOffer);
            return false;
        }
        return true;
    }

    private TimeSpan? TimeoutFor(TradeState state) => state switch
    {
        TradeState.FindingTarget => _options.FindTargetTimeout,
        TradeState.OpeningTrade => _options.OpenTradeTimeout,
        TradeState.WaitingForTrade or
        TradeState.OpeningGilInput or
        TradeState.WaitingForNumericInput or
        TradeState.SettingGil or
        TradeState.VerifyingOffer or
        TradeState.ConfirmingTrade or
        TradeState.ConfirmingFinalDialog => _options.UiTimeout,
        TradeState.AwaitingOutcome => _options.OutcomeTimeout,
        _ => null
    };

    private void Enter(TradeState next)
    {
        _state = next;
        _stateEntered = _io.Now;
        _nextActionAt = _io.Now;
    }

    private void Gate(TimeSpan delay)
        => _nextActionAt = _io.Now + delay;

    private void Emit(TradeEventType type)
    {
        if (_session is not null)
            _notify(type, _session.Snapshot(_state));
    }

    private void End(
        TradeEventType type,
        TradeState finalState,
        TradeFailureReason reason)
    {
        if (_session is null) return;
        var finished = _session.Snapshot(finalState, reason);
        _state = finalState;
        _session = null; // Vor Callback freigeben; keine doppelte Terminierung.
        _notify(type, finished);
    }

    public void Cancel()
    {
        if (_session is null) return;
        // Siehe Abschnitt 10: aktiver Handel darf nicht als 'sicher
        // abgebrochen' bezeichnet werden, bevor die Spielseite beendet ist.
        End(TradeEventType.Cancel, TradeState.Cancelled,
            TradeFailureReason.UserCancelled);
    }
}
```

**Explizite Einschränkungen dieses Referenzkerns:**

1. `Cancel()` beendet hier zunächst nur die Library-Session. Bei einem *bereits offenen* Handelsfenster benötigt die Produktionsversion zusätzlich eine clientseitige Cancel-Aktion und muss den tatsächlichen Abschluss abwarten. Bis dahin sollte die Engine `Cancelling`/`UnknownOutcome` führen, nicht sofort einen neuen Trade zulassen.
2. Das dargestellte `ConfirmingFinalDialog` verlangt einen tatsächlich sichtbaren finalen Dialog. Implementiere, falls nötig, einen getrennten Pfad für FFXIV-Varianten ohne diesen Dialog.
3. Ein Timeout nach dem finalen Klick ist möglicherweise ein **unbekannter Zahlungsausgang**. Er darf niemals zu einem automatischen erneuten Bezahlen des Teilbetrags führen.
4. Der naive Chat-Event-Adapter im vorigen Abschnitt reicht für eine strenge Zuordnung noch nicht aus.
5. Mehrere Button-Klicks und Event-Reentranz müssen in der Endfassung explizit ausgeschlossen werden.
6. `PreConfirmChecks` sollte nicht auf noch nicht lesbare UI-Werte sofort mit einem terminalen Fehler reagieren: Zwischen `unavailable` (weiter warten) und `mismatch` (abbrechen) muss getrennt werden.
7. **Kritischer Timingfehler vermeiden:** Eine globale Gate-Sperre nach `/trade` darf nicht den Erhalt/Erkennen von `TradeOpen` unterbinden, wenn dadurch ein enger Timeout greift. Deshalb Zustandsbeobachtung und Aktionsthrottling getrennt führen. Dasselbe gilt für das Erfassen von Chat-Outcome-Meldungen.

Der Code ist eine **detaillierte Engineering-Vorlage**, kein Versprechen, dass sämtliche API-Aufrufe und Zustandsübergänge ohne versionsbezogene Integration bereits produktionsfertig sind.

---

## 8. Statische Facade: `TradeManager.Trade(...)` und Callback

Für bequemen Aufruf in Host-Plugins kann eine statische Fassade bereitgestellt werden. Sie sollte intern genau eine `TradeEngine` besitzen und `Dispose()` unterstützen. Die Engine-Instanz selbst bleibt als Nicht-Static-Klasse testbar.

### `TradeManager.cs`

```csharp
namespace ModularTrade;

public static class TradeManager
{
    private static TradeEngine? _engine;
    private static ITradeGameAdapter? _adapter;
    private static TradeSnapshot? _lastSnapshot;
    private static bool _initialized;

    // Originalwunsch: 0=Cancel, 1=Success, 2=Start, 3=Progress.
    // long[] ist für hohe Gesamtbeträge notwendig.
    public static event Action<int, long[]>? onTradeChange;

    // Optionale alternative Schreibweise:
    public static event Action<int, long[]>? onStateChange
    {
        add => onTradeChange += value;
        remove => onTradeChange -= value;
    }

    // Zusätzlicher strukturierter Event für professionelle Integration.
    public static event Action<TradeEventType, TradeSnapshot>? TradeChanged;

    public static bool IsBusy => _engine?.IsBusy ?? false;
    public static TradeSnapshot? Current => _engine?.CurrentSnapshot;
    public static TradeSnapshot? LastResult => _lastSnapshot;

    public static void Initialize(
        ITradeGameAdapter adapter,
        TradeOptions? options = null)
    {
        if (_initialized)
            throw new InvalidOperationException("Already initialized");

        _adapter = adapter ?? throw new ArgumentNullException(nameof(adapter));
        _engine = new TradeEngine(adapter, options ?? new TradeOptions(), Emit);
        _initialized = true;
    }

    public static Guid Trade(string recipient, long amount)
    {
        EnsureInitialized();
        if (_engine!.IsBusy)
            throw new InvalidOperationException("A transfer is already active");

        _engine.Start(recipient, amount);
        return _engine.CurrentSnapshot!.SessionId;
    }

    // Exakt einmal pro Framework.Update vom Host aufzurufen.
    public static void Update()
    {
        if (!_initialized) return;
        _engine!.Tick();
    }

    public static void Cancel()
    {
        EnsureInitialized();
        _engine!.Cancel();
    }

    private static void Emit(TradeEventType type, TradeSnapshot snapshot)
    {
        _lastSnapshot = snapshot;

        long[] data = type == TradeEventType.Progress
            ? new long[]
            {
                snapshot.CurrentStep,
                snapshot.TotalSteps,
                snapshot.ConfirmedAmount,
                snapshot.TotalAmount
            }
            : Array.Empty<long>();

        // Einzelne Listenerfehler dürfen keine State Machine beschädigen.
        if (onTradeChange is { } callbacks)
        {
            foreach (Action<int, long[]> handler in callbacks.GetInvocationList())
            {
                try { handler((int)type, (long[])data.Clone()); }
                catch (Exception ex) { _adapter?.Log("Trade event handler failed", ex); }
            }
        }

        if (TradeChanged is { } typedCallbacks)
        {
            foreach (Action<TradeEventType, TradeSnapshot> handler
                     in typedCallbacks.GetInvocationList())
            {
                try { handler(type, snapshot); }
                catch (Exception ex) { _adapter?.Log("Typed trade handler failed", ex); }
            }
        }
    }

    private static void EnsureInitialized()
    {
        if (!_initialized)
            throw new InvalidOperationException("Call Initialize() first");
    }

    public static void Dispose()
    {
        if (!_initialized) return;
        if (IsBusy)
            throw new InvalidOperationException(
                "Stop and settle active trade before disposing");

        _adapter?.Dispose();
        _engine = null;
        _adapter = null;
        _lastSnapshot = null;
        onTradeChange = null;
        TradeChanged = null;
        _initialized = false;
    }
}
```

`onTradeChange` wird als **`event`** und nicht als öffentlich überschreibbare Delegate-Property angeboten. Aufrufer verwenden deshalb `+=`/`-=` und überschreiben nicht versehentlich andere Abonnenten. Wer ausdrücklich eine zuweisbare `TradeManager.onTradeChange = (...)`-Syntax will, kann zusätzlich eine separate `Action<int,long[]>? OnTradeChangeCallback { get; set; }` anbieten; in der Dokumentation sollte jedoch die Event-Variante empfohlen werden.

### Event-Reentranz

Ein Callback könnte direkt wieder `TradeManager.Trade(...)` auslösen. Das sollte in der Produktionsversion nicht während des aktuellen Ticks passieren: Stattdessen können Befehle aus Callback-Handlern in eine interne Command-Queue aufgenommen und erst am Ende des Framework-Updates verarbeitet werden. Die Referenz-Facade hält diese Erweiterungsstelle offen; ein produktionsfähiges Release muss das Verhalten deterministisch festlegen und testen.

---

## 9. Integration in ein vorhandenes Dalamud-Plugin

### 9.1 Plugin-Lifecycle

```csharp
public sealed class MyPlugin : IDalamudPlugin
{
    public string Name => "MyPlugin";

    public MyPlugin(DalamudPluginInterface pluginInterface)
    {
        ECommonsMain.Init(pluginInterface, this);

        var adapter = new DalamudTradeAdapter(); // Implementiert ITradeGameAdapter
        TradeManager.Initialize(adapter, new TradeOptions
        {
            MaxGilPerTrade = 1_000_000,
            MaxTradeCount = 10_000
        });

        TradeManager.onTradeChange += OnTradeChange;
        Svc.Framework.Update += OnFrameworkUpdate;
    }

    private void OnFrameworkUpdate(object framework)
        => TradeManager.Update();

    private void OnTradeChange(int type, long[] data)
    {
        switch ((TradeEventType)type)
        {
            case TradeEventType.Start:
                PluginLog.Information("Transfer started");
                break;

            case TradeEventType.Progress:
                PluginLog.Information(
                    $"Trade {data[0]}/{data[1]} verified: " +
                    $"{data[2]:N0} of {data[3]:N0} Gil");
                break;

            case TradeEventType.Success:
                PluginLog.Information("Transfer successfully completed");
                break;

            case TradeEventType.Cancel:
                var last = TradeManager.LastResult;
                PluginLog.Warning(
                    $"Transfer stopped: {last?.FailureReason}. " +
                    $"Verified {last?.ConfirmedAmount:N0} Gil; " +
                    $"remaining {last?.RemainingAmount:N0} Gil.");
                break;
        }
    }

    public void SendGil(string recipient, long gil)
    {
        if (!TradeManager.IsBusy)
            TradeManager.Trade(recipient, gil);
    }

    public void Dispose()
    {
        Svc.Framework.Update -= OnFrameworkUpdate;
        TradeManager.onTradeChange -= OnTradeChange;

        // Nur disposen, wenn Handel beendet und clientseitig abgewickelt ist.
        // Bei Plugin-Unload mit offenem Handel erst Cancel/Shutdown durchführen.
        if (!TradeManager.IsBusy) TradeManager.Dispose();
        ECommonsMain.Dispose();
    }
}
```

**Lifetime-Anforderung:** Wird das Host-Plugin während einer offenen Transaktion entladen, genügt der obige vereinfachte `Dispose()`-Pfad nicht. Implementiere einen Shutdown-Mechanismus, der neue Bestätigungen sofort sperrt, den clientseitigen Zustand kontrolliert schließt und erst danach die Registrierung der Events/Services aufhebt. Ein Plugin-Unload ohne kontrollierten Abschluss muss als `UnknownOutcome` protokolliert werden.

### 9.2 Minimaler Aufruf durch andere Plugin-Komponenten

```csharp
public void PayReward(string player, long rewardGil)
{
    TradeManager.Trade(player, rewardGil);
}
```

Das aufrufende Plugin muss anschließend **nicht** selbst warten oder in einer Schleife pollen. Es verarbeitet nur Events:

```csharp
TradeManager.onTradeChange += (type, data) =>
{
    if (type == 3)
        UpdateProgressBar(data[2], data[3]);
    if (type == 1)
        ShowCompletedMessage();
    if (type == 0)
        ShowCancelledMessage(TradeManager.LastResult);
};
```

Die Library tickt weiterhin über das Framework-Update des Hosts. „Passiv warten“ bedeutet **ereignisgesteuerte Nutzung**, nicht dass die Library ohne Framework-Loop funktionieren könnte.

---

## 10. Fehler, Cancellation und Zahlungssicherheit

Gil-Transfers sind **nicht idempotent**. Dieselbe Transaktion versehentlich ein zweites Mal auszuführen kann zu einer echten Doppelzahlung führen. Das ist die wichtigste Betriebsregel der Library.

### 10.1 Fehlerklassen

| Situation | Behandlung | Event |
|---|---|---|
| Zielspieler nicht auffindbar | bis begrenztem Timeout warten, dann stoppen | `Cancel` |
| Falscher Spieler / Ziel gewechselt | keine weitere Bestätigung; Handel kontrolliert abbrechen | `Cancel` |
| Zu wenig Gil | vor Eingabe stoppen | `Cancel` |
| Trade-Fenster öffnet nicht | Timeout; nicht unbegrenzt `/trade` wiederholen | `Cancel` |
| Numeric-Addon erscheint nicht | Timeout; kontrolliert abbrechen | `Cancel` |
| Eingelesener Betrag ungleich geplant | keine Bestätigung; abbrechen | `Cancel` |
| Trade wurde von einer Seite abgelehnt | eindeutig als abgebrochen verbuchen | `Cancel` |
| Trade-Dialog schließt ohne verifizierten Erfolg | **Unknown Outcome**, nicht erneut zahlen | `Cancel` + Snapshot |
| Timeout nach Bestätigung | **Unknown Outcome**, nicht erneut zahlen | `Cancel` + Snapshot |
| Alle Teiltrades eindeutig erfolgreich | Transfer abschließen | `Success` |

### 10.2 Empfohlene Cancellation-Semantik

Unterscheide:

- **CancelRequested:** Abbruch angefordert, keine neuen Geld-/Bestätigungscallbacks ausführen.
- **Cancelling:** Wenn ein Handel offen ist, kontrolliert den Client-Handel beenden; bis zur Rückmeldung keine nächste Session zulassen.
- **Cancelled:** Tatsächlicher Abbruch bestätigt; keine Zahlung des gerade offenen Teilbetrags erfolgt.
- **UnknownOutcome:** Nicht sicher feststellbar, ob der gerade bearbeitete Teilbetrag übertragen wurde; sofort stoppen, keine automatischen Wiederholungen.

Ein sehr wichtiger API-Hinweis: Nach einem Unknown Outcome bedeutet `RemainingAmount = TotalAmount - ConfirmedAmount` lediglich **noch nicht verifiziert übertragen**, nicht „sicher noch zu bezahlen“. Die tatsächliche Differenz kann kleiner sein. Deshalb dem Nutzer niemals automatisch anbieten, `RemainingAmount` unverändert erneut zu überweisen.

### 10.3 Erstes Release: keine automatisch wiederholten Überweisungen

Technische Aktionen dürfen vor der Geldbestätigung unter wohldefinierten Bedingungen erneut geprüft werden (z. B. Ziel suchen, auf Addon warten). Nach einer **möglicherweise serverseitig abgeschlossenen Bestätigung** wird grundsätzlich keine Zahlung erneut eingereiht, solange der Ausgang unklar ist.

### 10.4 Session-Schutz / Race Conditions

Jeder Lauf erhält eine `Guid SessionId`. Jede Teiltransaktion erhält zusätzlich einen monotonen Index. Statusmeldungen müssen diesem Vorgang zugeordnet werden. Erforderliche Guards:

- Nur ein aktiver Transfer.
- `Start` exakt einmal, finales Event exakt einmal.
- Keine Events der vorherigen Session für neue Session konsumieren.
- Keine Doppelklicks auf Trade oder Yes-Button.
- Bei Zielwechsel sämtliche weiteren Bestätigungen sperren.
- Vor jeder Bestätigung aktuellen eigenen Gil-Wert erneut überprüfen.
- Vor jedem neuen `/trade` vorherige Session-/Dialogzustände und Outcome-Buffer zurücksetzen.
- Nicht unkontrolliert in Listenern reentrant neue Trades starten.
- `Dispose()` darf keine laufenden Callbacks auf freigegebene Pointer zulassen.
- Alle UI-Operationen auf dem Framework-Thread.

---

## 11. Optionale `TradeAsync`-API

Ein callback-basierter `Trade()`-Aufruf soll die Minimal-API bleiben. Optional ist eine Task-basierte Variante hilfreich, ohne den Framework-Thread zu blockieren:

```csharp
public static Task<TradeResult> TradeAsync(
    string recipient,
    long gil,
    CancellationToken cancellationToken = default);
```

Mögliche Nutzung:

```csharp
TradeResult result = await TradeManager.TradeAsync(
    "SpielerName@Welt", 5_000_000, cancellationToken);

if (result.Succeeded)
    PluginLog.Information("Transfer finished");
else
    PluginLog.Warning(
        $"Transfer stopped: {result.FinalSnapshot.FailureReason}");
```

Die Umsetzung nutzt `TaskCompletionSource<TradeResult>(TaskCreationOptions.RunContinuationsAsynchronously)`, bleibt aber weiterhin **auf den Framework-Tick angewiesen**. Die `CancellationToken`-Anforderung muss dieselbe kontrollierte Cancel-State-Machine verwenden wie `TradeManager.Cancel()`. Eine `Task` nur wegen Cancellation abzubrechen, während der Trade im Spiel weiterläuft, wäre gefährlich.

---

## 12. Beispielverläufe

### 12.1 Vollständiger Transfer von 5.000.000 Gil

```text
TradeManager.Trade("SpielerName@Welt", 5000000)

[Library] VALIDATE amount=5000000, player="SpielerName@Welt"
[Library] PLAN [1000000,1000000,1000000,1000000,1000000]
[Event]   type=2 data=[]

[Trade 1] target -> /trade -> TradeOpen -> InputNumeric -> 1000000
[Trade 1] verify amount -> click trade -> confirm -> success verified
[Event]   type=3 data=[1,5,1000000,5000000]

[Trade 2] target -> /trade -> TradeOpen -> InputNumeric -> 1000000
[Trade 2] verify amount -> click trade -> confirm -> success verified
[Event]   type=3 data=[2,5,2000000,5000000]

[Trade 3] target -> /trade -> TradeOpen -> InputNumeric -> 1000000
[Trade 3] verify amount -> click trade -> confirm -> success verified
[Event]   type=3 data=[3,5,3000000,5000000]

[Trade 4] target -> /trade -> TradeOpen -> InputNumeric -> 1000000
[Trade 4] verify amount -> click trade -> confirm -> success verified
[Event]   type=3 data=[4,5,4000000,5000000]

[Trade 5] target -> /trade -> TradeOpen -> InputNumeric -> 1000000
[Trade 5] verify amount -> click trade -> confirm -> success verified
[Event]   type=3 data=[5,5,5000000,5000000]
[Event]   type=1 data=[]
```

### 12.2 Unvollständiger Transfer (nach 2 Teiltrades abgebrochen)

```text
TradeManager.Trade("SpielerName@Welt", 5000000)
[Event] type=2 data=[]
[Event] type=3 data=[1,5,1000000,5000000]
[Event] type=3 data=[2,5,2000000,5000000]

[Trade 3] Trade canceled by remote player
[Event] type=0 data=[]

TradeManager.LastResult:
  State = Cancelled
  FailureReason = RemoteCancelled
  ConfirmedAmount = 2000000
  TotalAmount = 5000000
  RemainingAmount = 3000000
```

**Nur im eindeutig abgebrochenen Fall** kann man den Restbetrag als sicher nicht durch den gerade abgebrochenen Teiltrade überwiesen verstehen. Beim Status `UnknownOutcome` gilt diese Schlussfolgerung nicht.

### 12.3 Restbetrag im letzten Teiltrade

```text
Trade( "SpielerName@Welt", 2350001 )
Plan: [1000000, 1000000, 350001]

Start:     (2, [])
Progress:  (3, [1, 3, 1000000, 2350001])
Progress:  (3, [2, 3, 2000000, 2350001])
Progress:  (3, [3, 3, 2350001, 2350001])
Success:   (1, [])
```

---

## 13. Testspezifikation

Core-Tests sollten ohne FFXIV, Dalamud oder den tatsächlichen Spielclient laufen. Dafür `FakeTradeGameAdapter` verwenden: Er kann Zeit kontrolliert fortschalten, den Zustand `TradeOpen` simulieren, das Numeric-Addon erscheinen lassen, den angebotenen Betrag variieren und `GameTradeOutcome` erzeugen.

### 13.1 Planner-Tests mit xUnit

```csharp
using ModularTrade;
using Xunit;

public sealed class TradePlannerTests
{
    [Theory]
    [InlineData(1, new[] { 1 })]
    [InlineData(999999, new[] { 999999 })]
    [InlineData(1000000, new[] { 1000000 })]
    [InlineData(1000001, new[] { 1000000, 1 })]
    [InlineData(2350001, new[] { 1000000, 1000000, 350001 })]
    [InlineData(5000000, new[] { 1000000, 1000000, 1000000, 1000000, 1000000 })]
    public void SplitsIntoExpectedTrades(long amount, int[] expected)
    {
        Assert.Equal(expected, TradePlanner.CreatePlan(amount));
    }

    [Fact]
    public void RejectsZeroOrNegative()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TradePlanner.CreatePlan(0));
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TradePlanner.CreatePlan(-1));
    }

    [Fact]
    public void RejectsTooManyTradesWithoutOverflow()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => TradePlanner.CreatePlan(long.MaxValue));
    }
}
```

### 13.2 Verbindliche Engine-Tests

1. `Start` wird genau einmal ausgelöst.
2. Ohne targetbaren Empfänger findet keine `/trade`-Aktion statt.
3. Bei Zielwechsel wird nicht bestätigt.
4. Ein freigegebenes Handelsfenster ohne Gil-Eingabedialog löst keinen Betragscallback aus.
5. Gil-Callback verwendet exakt den erwarteten Teilbetrag.
6. Angebotswert `999999` bei erwartetem `1000000` führt nie zu einem Bestätigungsklick.
7. Warten auf Trade-Button ist nicht gleichbedeutend mit Erfolg.
8. `TradeOpen=false` allein erzeugt **kein** Progress-Event.
9. Nur verifizierter Erfolg löst `Progress` aus.
10. Bei 5 Millionen erfolgen exakt fünf `Progress`-Events und anschließend ein `Success`.
11. Bei Cancel im dritten Trade bleiben nur zwei Millionen bestätigt.
12. Bei Timeout nach finalem Dialog wird nichts wiederholt und `UnknownOutcome` gespeichert.
13. Ein altes Success-Event darf den neuen Trade nicht abschließen.
14. `Cancel` und `Success` dürfen sich nicht gegenseitig doppeln.
15. Handler-Ausnahmen dürfen keine zusätzlichen Handelsbestätigungen auslösen.
16. Eine zweite `Trade()`-Anforderung während einer Session wird deterministisch abgelehnt.
17. Große Summen über `int.MaxValue` werden in Progress-Payloads ohne Overflow repräsentiert.
18. Ein ungültiger Betrag (`0`, negativ oder zu viele Schritte) wird vor dem Start abgewiesen.
19. Plugin-Unload während offenem Trade verhindert weitere Auto-Confirm-Aktionen.
20. Der Adapter verarbeitet lokalisierte Trade- und Statusmeldungen korrekt.
21. Der letzte Schritt enthält genau den Restbetrag, niemals versehentlich 1.000.000 Gil.
22. Aufeinanderfolgende Sessions teilen sich keine veralteten UI- oder Chat-Eventdaten.

### 13.3 Live-Validierung nur kontrolliert

Zuerst UI-/Adaptertests ohne Echtgeldwährung des Spiels, danach manuelle Prüfung mit minimalen Gil-Beträgen und einem explizit zustimmenden zweiten Testcharakter. Zu validieren sind insbesondere eigener Gil-Textknoten, lokalisierte Success-/Cancel-Erkennung, korrekter finaler Bestätigungsdialog sowie das Verhalten bei Timeout, Fensterwechsel und Remote-Abbruch. Bei Änderungen am FFXIV-Client müssen UI-Indices erneut geprüft werden.

---

## 14. Umsetzungsplan für Codex

**Phase A – Library-Kern:**

- Neues `ModularTrade`-Projekt als referenzierbare C#-Library anlegen.
- Enums, `TradeOptions`, `TradeSnapshot`, `TradeResult`, `TradePlanner`, `TradeSession` und `ITradeGameAdapter` erstellen.
- Unit-Tests für Zerlegung, Grenzen, Fortschritt und Fehlerbedingungen implementieren.

**Phase B – Framework-State-Machine:**

- Nicht-blockierende `TradeEngine.Tick()`-Steuerung.
- Eindeutige State-Transitions, per-State-Timeouts, unabhängige Aktionsthrottles.
- Cancel-Handling mit Safe-Shutdown und `UnknownOutcome`.
- Erfolgsverbuchung ausschließlich nach korrelierter Abschlussbestätigung.

**Phase C – Dalamud-Adapter:**

- Spielerobjekte, `Sender`-Namensformat und Targeting anbinden.
- `/trade` senden; `TradeOpen` abfragen.
- Addon-Lifecycle für `Trade`, `InputNumeric`, `SelectYesno` implementieren.
- Callbacks für Gil-Eingabe und Klicks für Trade-Bestätigung anbinden.
- **Eigenen** angebotenen Gil-Wert lesen und validieren (nicht gegnerischen Gil-Wert verwechseln).
- Chatmeldungserkennung mit Session-Korrelation, Sprache und zeitlicher Reihenfolge implementieren.
- UI-Nodes und Callback-Konstanten als versionsabhängige Selector-Komponente kapseln.

**Phase D – Öffentliche Integration:**

- `TradeManager.Initialize`, `Trade`, `Update`, `Cancel`, `Dispose` implementieren.
- Gewünschtes `(type, long[] data)`-Event mit `0/1/2/3` anbieten.
- Typisierte Events/Snapshots als Ergänzung.
- Dokumentation für die Host-Plugin-Einbindung schreiben.

**Phase E – Qualitätsprüfung:**

- Fake-Adapter-Szenarien automatisiert testen.
- Reentranz, Multi-Session-Lebensdauer, Cancel und Unknown Outcomes testen.
- Dalamud-Adapter in kontrollierter Spielsitzung schrittweise verifizieren.
- Erst nach nachgewiesener Zuverlässigkeit höhere Beträge freigeben.

### Definition of Done

Das Modul ist fertig, wenn:

- Ein Host-Plugin **ohne UI-Automatisierungscode** `TradeManager.Trade("SpielerName@Welt", amount)` aufrufen kann.
- Jeder positive, innerhalb der konfigurierten Gesamt-/Schrittgrenzen liegende Betrag deterministisch aufgeteilt wird.
- `onTradeChange` die spezifizierten Events und Payloads in korrekter Reihenfolge auslöst.
- `traded_amount` ausschließlich verifiziert erfolgreiche Überweisungen zählt.
- Kein Abbruch oder unklarer Ausgang fälschlich als Erfolg gilt.
- Keine automatische Doppelzahlung bei Zeitüberschreitung möglich ist.
- Das Modul einschließlich Listener und Hooks sauber aus einem Host-Plugin wieder entfernt werden kann.

---

## 15. Source-Mapping und technische Nachprüfung

| Verantwortlichkeit | Referenzdatei |
|---|---|
| Start-/Planungsliste | [`Dropbox/TradeQueueUI.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/TradeQueueUI.cs) |
| Einzeltrade-Tasks | [`Dropbox/TradeTask.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/TradeTask.cs) |
| Framework-Update, Dialog, Chat | [`Dropbox/Dropbox.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/Dropbox.cs) |
| Button-Click-Wrapper | [`Dropbox/ClickGeneric.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/ClickGeneric.cs) |
| Globale Konfiguration | [`Dropbox/Config.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/Config.cs) |
| Queue-Modell | [`Dropbox/TradeQueueEntry.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/TradeQueueEntry.cs) |
| Gemischter Gil-/Item-Transfer (separat) | [`Dropbox/TaskAddItemsToTrade.cs`](https://github.com/kawaii/Dropbox/blob/main/Dropbox/TaskAddItemsToTrade.cs) |
| Git-Submodule | [`.gitmodules`](https://github.com/kawaii/Dropbox/blob/main/.gitmodules) |

Verwendete Submodule im untersuchten Repository-Baum:

- ECommons: `NightmareXIV/ECommons`, Gitlink `19d503acb40fb0414018a7be0d13a068ed28aabc`.
- ClickLib: `Limiana/ClickLib`, Gitlink `c9d66718396043dd7f0b3e3132e6d540b456667a`.

**Offene technische Prüfungen vor Fertigstellung:**

1. Konkrete `TaskManager.DelayNext(15, true)`-Semantik am historischen ECommons-Commit prüfen (für das vorgeschlagene Tick-Design nicht zwingend benötigt).
2. `Callback.Fire`-Verhalten an der verwendeten Client-/ECommons-Version prüfen.
3. Für den **eigenen** Gil-Eintrag den korrekten UI-Node und dessen Sichtbarkeit nachweisen.
4. Prüfen, wann FFXIV die finale Handelsbestätigung tatsächlich zeigt und wann `TradeOpen` umschaltet.
5. Klären, ob Chat-Nachrichten bei allen Clientsprachen als zuverlässige Erfolgsquelle und Session-Korrelationssignal genügen.
6. Ein sicheres Cancel des aktuell offenen Handelsfensters implementieren.

**Schnittstellenprinzip:** Das Spiel und seine UI sind eine austauschbare I/O-Implementierung. Betragsplanung, Transaktionszustand, Erfolgskriterien und Eventvertrag gehören vollständig in den getesteten Library-Kern. Das ermöglicht, dieselbe Library als Feature in mehreren Dalamud-Plugins zu nutzen, ohne die ursprüngliche Dropbox-Plugin-UI zu übernehmen.
