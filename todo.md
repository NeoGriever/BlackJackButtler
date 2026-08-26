# TODO: Debug-Visualmodus mit JavaScript-Canvas

## Ziel

Der vorhandene Main-View-Schalter `Standard | Visual` bleibt ausschließlich in
`Debug`-Builds sichtbar. Im `Release`-Build darf weder der Schalter noch die
JavaScript-Canvas-Laufzeit verfügbar sein.

Der Visualmodus rendert die Main-Ansicht aus einer externen `.js`-Datei. Das
Script zeichnet über eine kleine Canvas-2D-Fassade auf einen Offscreen-Buffer.
Erst wenn `render(payload, ctx)` vollständig beendet wurde und ausdrücklich
`true` zurückgibt, wird der bisher sichtbare Buffer atomar durch den neuen
Buffer ersetzt. Bei Fehler, Abbruch oder `false` bleibt das letzte gültige Bild
sichtbar.

## Laufzeitmodell

1. Main-UI erstellt einen unveränderlichen Snapshot-Payload mit Tisch-,
   Dealer-, Spieler-, Hand- und Phaseninformationen.
2. Eine `refreshed`-/Versionsnummer markiert den neuesten Payload. Ein neuer
   Zustand ersetzt einen noch wartenden Renderauftrag.
3. Genau ein Worker verarbeitet Renderaufträge seriell. Es dürfen niemals zwei
   `render()`-Aufrufe gleichzeitig laufen.
4. Während ein Render läuft, kann ein neuerer Zustand eine
   Abbruchanforderung für den laufenden Auftrag markieren. Der JavaScript-Host
   muss dies kooperativ prüfen und zusätzlich ein hartes Ausführungslimit
   haben; ein JavaScript-Aufruf ist nicht beliebig sicher von außen stoppbar.
5. Endet/abbricht der aktuelle Auftrag, startet direkt der neueste noch
   ausstehende Auftrag. Veraltete, erfolgreiche Ergebnisse werden nicht
   übernommen.
6. Nur ein erfolgreicher Auftrag derselben aktuellen Versionsnummer darf den
   Front-Buffer tauschen. Der ImGui-Frame zeichnet ausschließlich diesen
   unveränderlichen Front-Buffer und wird nie durch JavaScript blockiert.
7. Maus- und später Tastaturereignisse werden als rohe Events (`onMouseDown`,
   `onMouseUp`, `onMouseMove`, `onMouseWheel`) an JavaScript weitergereicht.
   Die JS-Datei entscheidet selbst über ihre UI-Logik und ruft danach die
   freigegebenen BJB-Aktionen auf.

## Erster Funktionsumfang

- Externe Debug-Scriptdatei mit Reload und klarer Fehlermeldung.
- `render(payload, ctx)` mit Rückgabewert `true` für Commit.
- Canvas-Grundfunktionen: Zustand (`save`/`restore`), Farben (`#hex`,
  `rgb`/`rgba`), `clearRect`, `fillRect`, `strokeRect`, Pfade, Linien, Arcs,
  Bézierkurven, `fill`, `stroke`, Text sowie lineare/radiale Gradients.
- Buffered Canvas: `new Canvas(width, height)`, `getContext("2d")` und
  `drawImage(bufferCanvas, ...)`; zunächst als nativer Command-Buffer.
- Rohes Pointer-Event-Dispatching mit lokalen Canvas-Koordinaten,
  Maustaste, Modifiern und Zeitstempel.
- Read-only Payload und sandboxierte JS-Laufzeit ohne direkten Datei-,
  Netzwerk- oder .NET-Zugriff.

## BJB-Aktionsschnittstelle

JavaScript erhält ausschließlich einen validierten BJB-API-Wrapper, niemals
direkten Zugriff auf `PlayerState` oder den GameEngine-Zustand. Aktionen werden
nach dem Event-/Render-Callback über dieselbe Queue, Validierung,
Snapshot-/Log-, Sync- und Save-Logik wie die normale Main-Ansicht ausgeführt.

- Spieler: Bank, Bet, Alias, VIP, Activate/Deactivate, Ghost einfügen/löschen.
- Aktionen: Draw/Hit, Stand, Split, Double Down, Triple Down.
- Dealer: Draw, Stand.
- Tisch: Runde starten und die sonstigen bereits in der Main-Ansicht
  verfügbaren Tischaktionen.

## Architekturarbeit vor der API

- Die gegenwärtig teilweise in ImGui-Button-Handlern liegende Fachlogik in
  einen gemeinsamen `TableActionService` auslagern.
- Normaler Main-View und JS-API müssen diesen Service gemeinsam verwenden,
  damit Regeln, Locks, Zahlungsdialoge und Seiteneffekte identisch bleiben.
- Asset-/Font-Cache für `drawImage` und registrierte Schriftprofile vorsehen;
  beliebige Browser-/System-Fonts gehören nicht in den ersten Scope.

## Abnahme

- Release: Kein Visual-Switch und keine JS-Laufzeit im ausgelieferten Plugin.
- Debug: Switch sichtbar, Script lädt, erfolgreicher Render ersetzt das Bild
  erst nach `true`.
- Ein langsames, fehlerhaftes oder endloses Script lässt ImGui bedienbar und
  behält den letzten gültigen Frame.
- Bei mehreren raschen State-Änderungen wird ausschließlich der neueste
  vollständige Frame sichtbar.
- Klick-Ereignisse erreichen JavaScript mit korrekten Canvas-Koordinaten;
  eine JS-ausgelöste BJB-Aktion folgt denselben Regeln wie ihr normaler
  Main-View-Button.
