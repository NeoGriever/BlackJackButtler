# Fahrplan: Ansichtsvariante 3 und globale UI-Korrekturen

## Status und Zweck

**Status: Implementierung und Debug-Build abgeschlossen, Ingame-Abnahme offen.** Dieses Dokument wurde während der Arbeit fortgeschrieben; `[x]` bedeutet statisch umgesetzt und per Debug-Build geprüft. Die manuelle Ingame-Prüfung steht weiterhin aus.

Dieses Dokument ist die verbindliche Arbeitsgrundlage für die nächste Umsetzungsetappe des Dalamud-Plugins **BlackJack Buttler**. Es fasst den gewünschten Funktionsumfang, die technische Aufteilung, Migrationsregeln und die Abnahmekriterien zusammen, damit eine spätere Chat-Session ohne Kontextverlust weiterarbeiten kann.

Die Umsetzung wurde nach der Freigabe durchgeführt. Bei einer Fortsetzung sind die im Verifikationsplan aufgeführten Ingame-Tests auszuführen.

## Umsetzungstatus

- [x] Konfigurationsbasis, Laufzeit-Gates und V3-Dispatcher *(Debug-Build bestanden)*
- [x] Regex-, Message- und Own-Button-Seiten *(Debug-Build bestanden)*
- [x] Variante-3-Settings: Automation, Rules, Betting und Time & Delay *(Debug-Build bestanden)*
- [x] Globale Trading-Player-Autoaktivierung *(Debug-Build bestanden)*
- [x] Globale Withdraw-Regex, Teil-Auszahlungsbuffer und Version 1.9.0.0 *(Debug-Build bestanden)*
- [x] Nearby Players, Sound, Stats und Main-Ansicht *(Debug-Build bestanden)*
- [x] ImGui-Switch-Stabilisierung und Nearby-Größenschutz *(Debug-Build bestanden; gezielter Ingame-Retest offen)*
- [-] Changelog, Build und manuelle Abnahme *(Changelog und Debug-Build umgesetzt; Ingame-Abnahme offen)*

## Verbindliche Scope-Regel

- Eine Änderung mit dem Marker **`[all]`** gilt in allen Ansichtsvarianten (derzeit 1 und 2, künftig auch 3).
- Jede nicht mit **`[all]`** markierte Änderung gilt **nur in Ansichtsvariante 3**.
- Variante 1 und 2 behalten ihre jeweilige Anordnung und ihren Funktionsumfang, sofern eine globale Änderung nicht zwingend eine gemeinsame Datenmigration oder einen gemeinsamen Laufzeitpfad benötigt.
- Die Visual-Seite bleibt in Variante 3 unverändert (Punkt 1.13). Globale Änderungen an gemeinsamen Helfern dürfen ihr Verhalten nicht unbeabsichtigt verändern.

Bei gemischten Punkten ist die Kennzeichnung der einzelnen Unterpunkte maßgeblich. Beispielsweise bleiben die neuen `On`/`Off`-Schalter grundsätzlich Teil der Variante 3; die ausdrücklich als `[all]` markierten Rules-Schalter gelten dagegen global.

## Verifizierter Ausgangspunkt vor der Umsetzung

- `Configuration.MainViewVersion` kennt aktuell nur die Werte 1 und 2. Der Dispatcher leitet Variante 2 zu `DrawMainPageV2` und `DrawSettingsPageV2`; Variante 3 existiert noch nicht.
- Nachrichten, Regex und eigene Buttons liegen jeweils bereits in zentralen Konfigurationslisten. Standard-Einträge lassen sich anhand der von `DefaultsManager` gelieferten Namen erkennen.
- Eigene Buttons verwenden derzeit `CustomCommandGroups` plus eine separate, namensbasierte `CustomButtonOrder`; Breaks werden nur als String `"---"` in dieser Reihenfolge geführt.
- Die Einstellungen der Variante 2 liegen schwerpunktmäßig in `windows/win.06.settings.v2.cs`; Variante 1 nutzt die älteren Settings-Tabs. Gemeinsame GUI-Helfer liegen in `windows/BJBGui.cs`.
- Es gibt kein im Projekt gefundenes automatisiertes Testprojekt. Die Basissicherung erfolgt daher über Build, Konfigurations-/Migrationsprüfungen und gezielte manuelle ImGui-Tests.

## Architektur- und Migrationsleitplanken

### Neue Ansichtsvariante

1. `MainViewVersion` wird um einen expliziten Wert **3** erweitert; die Auswahl muss weiterhin Variante 1, 2 und 3 anbieten.
2. Für Variante 3 werden eigenständige Renderpfade angelegt, mindestens für Main-/Settings-spezifische Darstellung. Gemeinsame Geschäftslogik, Datenmodelle und Save-Verhalten werden nicht dupliziert.
3. Nicht-globale Layoutänderungen dürfen nicht per verstecktem Sonderfall in Variante 1 oder 2 einfließen.
4. Der Wechsel auf Variante 3 darf keine bestehenden Konfigurationswerte zurücksetzen.

### Gemeinsame UI-Bausteine

Vor den Einzelseiten werden kleine, wiederverwendbare Helfer ergänzt bzw. vereinheitlicht:

- Zwei-Zustands-Auswahl `Label [On] [Off]` als zentraler, verbundener Switch: Beide Hälften haben keine innere Lücke oder Rahmenkante; nur die äußeren Ecken sind abgerundet. Genau die gewählte Hälfte – auch `Off` – erhält die einheitliche Highlight-Farbe.
- Schrittzahlfelder verwenden zentral ein zusammenhängendes `− | Eingabe | +`-Objekt: schmale äußere Buttons mit ausschließlich äußeren Rundungen, ein quadratisches rahmenloses Eingabefeld und keine inneren Abstände. Das Zahlensegment bleibt mindestens 20 px breit. Die orange hervorgehobene Auswahl nutzt durchgängig schwarzen Text.
- Ein eigener, hervorgehobener Ein/Aus-Button für fachliche Toggle-Optionen wie `Soft`.
- Einheitliche Zeilenbreiten für Schalter, Combos und Button-Listen.
- Editierbares formatiertes Gil-Feld ohne Leerzeichen-Padding im Eingabewert (Details in Paket E).
- Editierbare Label- bzw. Doppelklick-Felder mit sauberem Fokus, vollständiger Textauswahl und `Enter`/`OK`-Abschluss.

Jeder Helfer erhält eindeutige ImGui-IDs. Änderungen an Texteingaben werden ohne Listen-Neuaufbau im selben Frame gespeichert; dadurch bleiben Fokus, Cursor und offene Header stabil.

### Persistenz und Rückwärtskompatibilität

- Neue Konfigurationsfelder haben sichere Defaults, so dass alte JSON-Konfigurationen ohne Verlust geladen werden.
- Bei Datenmodellwechseln bleiben alte gespeicherte Daten erhalten und werden einmalig in die neue Struktur übernommen. Sobald neue Strukturdaten vorhanden sind, haben sie Vorrang; die alte Repräsentation bleibt nur für Import-/Export-Kompatibilität synchronisiert.
- Daten, die bisher als nackte Strings gespeichert werden (z. B. Sound-Dateien oder die Button-Reihenfolge), werden über eine einmalige, idempotente Migration in strukturierte Einträge überführt, falls das neue Verhalten Zusatzdaten benötigt.
- Bestehende Standard- und Custom-Einträge werden nicht anhand der sichtbaren Reihenfolge, sondern anhand stabiler Default-Snapshots bzw. Namen klassifiziert.
- Alte Breaks (`"---"`) werden in das neue Break-Modell überführt. Bei Kollisionen oder verwaisten Einträgen bleibt die bestehende Reihenfolge erhalten und die Einträge werden weiterhin sichtbar bearbeitbar gemacht.
- Bei jeder Migration: vorhandene Werte übernehmen, nicht duplizieren, danach sofort speichern und erst dann die UI darauf aufbauen.

## Umsetzungspakete

### A. Grundgerüst für Layout 3 — [x]

**Geltung: nur Variante 3, außer den unten ausdrücklich globalen Komponenten.**

1. Erweiterung der Auswahl in General auf drei Ansichtsvarianten; bestehende Wahl von Variante 1/2 bleibt funktional.
2. Neuer `DrawSettingsPageV3`-Pfad mit Tabs für General, Automation, Rules, Betting, Time & Delay, Nearby Players, Visual und die weiteren heute angebotenen Settings-Seiten. Nicht angeforderte Seiten behalten ihren bisherigen Inhalt beziehungsweise werden nur in die neue Navigation eingebettet.
3. Eigene Main-Variante 3 nur dort, wo globale Main-Änderungen eine Ansicht benötigen; keine stillschweigende visuelle Neugestaltung der nicht angeforderten Main-Komponenten.
4. Global veränderte Seiten (Messages, Regexes, Own Buttons, Stats und Nearby-Main) nutzen denselben fachlichen Code für alle Varianten, aber lassen nicht-globale Layoutdetails in V1/V2 unverändert.

**Abnahme:** V1, V2 und V3 lassen sich auswählen, neu starten und wechseln, ohne Settings, Presets oder Datenlisten zu verlieren.

**Umgesetzt:** Der Main-/Settings-Dispatcher kennt Variante 3; die Auswahl steht in den bisherigen General-Seiten zur Verfügung. V3 hat eine eigene Settings-Ansicht und nutzt für die nicht neu angeforderten Main-Inhalte bewusst den gemeinsamen V2-Renderpfad. Die Initialmigration wird beim Laden ausgeführt und speichert nur übernommene, neue Felder.

### B. Regexes in Standard und Custom aufteilen — [x]

**Geltung: `[all]`.**

1. Die Regex-Seite erhält zwei Tabs: **Standard Regex Entries** und **Custom Regex Entries**, analog zur bestehenden Commands-Aufteilung.
2. Standard-Tab zeigt ausschließlich Einträge, die zu den Standard-Regexnamen/-snapshots gehören; Custom-Tab ausschließlich alle anderen Einträge.
3. Filter, Expandieren, Editierbarkeit, Reset und Cache-Invalidierung bleiben erhalten und wirken jeweils auf die korrekte Kategorie.
4. Die bisherige Einstellung zum Bearbeiten von Standard-Regex bleibt fachlich wirksam; die Tab-Aufteilung ersetzt sie nicht.
5. Reihenfolge-Buttons betreffen nur Custom-Regexes und dürfen Standard-Einträge nicht überspringen oder versehentlich verschieben.

**Abnahme:** Nach Laden einer alten Konfiguration erscheint jeder Regex genau einmal im richtigen Tab; Trigger-Ausführung und Reihenfolge der Custom-Regexes bleiben unverändert.

### C. Messages: Standard/Custom-Tabs und Multiline-Editor — [x]

**Geltung: `[all]`.**

#### C1. Kategorien

1. Die Messages-Seite erhält zwei Tabs: **Standard Message Batches** und **Custom Message Batches**.
2. Die bestehende Standardbatch-Erkennung wird für diese Trennung genutzt; weder Reihenfolge noch Auswahlmodus (`Random`, `First`, `Iterative`) dürfen sich durch den UI-Umbau ändern.
3. Neuerstellte Batches sind Custom-Batches. Standardbatches bleiben an ihrer bisherigen Reset-/Default-Logik angeschlossen.

#### C2. Multiline-Editor je Batch

1. Jeder Batch kann zwischen Listenansicht und Textblockansicht wechseln. Der Umschaltknopf wird unterhalb der Eintragsliste als **`List Edit Mode`** platziert; die genaue Beschriftungsrichtung ist vor Implementierung zu bestätigen (siehe offene Punkte).
2. In der Textblockansicht werden die Nachrichten als ein mehrzeiliges Textfeld dargestellt: eine Message pro physischer Zeile.
3. **Save** übernimmt die aktuellen Zeilen in die Einzel-Message-Liste, passt zugehörige AD-Flags verlustfrei an und speichert.
4. **Back** verhält sich exakt wie folgt:
   - kein Unterschied zur zuletzt gespeicherten Textrepräsentation: direkt zurück zur Listenansicht;
   - Änderungen vorhanden: englischer Dialog mit **Yes**, **No**, **Cancel**;
   - **Yes**: Zeilen parsen, speichern, zurück zur Listenansicht;
   - **No**: Änderungen verwerfen, zurück zur Listenansicht;
   - **Cancel**: nichts speichern und in der Textblockansicht bleiben.
5. Das Prüfen auf Änderungen erfolgt gegen einen beim Öffnen erzeugten Snapshot, nicht gegen den möglicherweise bereits gerenderten Listenzustand.
6. Parse-Fehler werden vor dem Speichern sichtbar gemacht und dürfen weder den Batch noch AD-Flags teilweise verändern.

**Abnahme:** Save, No und Cancel liefern jeweils exakt den oben beschriebenen Endzustand. List- und Textmodus können wiederholt gewechselt werden, ohne Message-Reihenfolge, leere Einträge oder AD-Flag-Zuordnungen zu verlieren.

### D. Own Buttons: zusammengeführte Liste und Breaks — [x]

**Geltung: `[all]`.**

#### D1. Zielaufbau

Der bisherige Hilfetext, der obere Add-Group-Bereich sowie die getrennten Abschnitte **Button Order** und **Unassigned** entfallen. Die gesamte Reihenfolge ist direkt die Liste der Command-Groups:

```text
Own Buttons
------------------------------------------------
[Filter]
[^] [v] [X] [O] [> Shout]
[^] [v] [X] [O] [--Break--]
[^] [v] [X] [O] [> Betrange]
[new group name                              ] [Add Group] [Add Break]
```

1. Das Filterfeld trägt nur den Platzhalter **`Filter`**.
2. `^` und `v` verändern die Reihenfolge der sichtbaren Listeneinträge, einschließlich Breaks.
3. `X` ersetzt `Delete Group`, setzt das Halten von `CTRL` voraus und löscht den betreffenden Eintrag.
4. `O` schaltet die Sichtbarkeit. Sichtbare und unsichtbare Einträge bleiben vollständig editierbar, auch wenn sie nicht in der Button-Bar erscheinen.
5. Der sichtbare Header aktiver Einträge erhält einen leichten, etwa 10-prozentigen pastellblauen Farbversatz. Inaktive/unsichtbare Header werden explizit deutlich dunkler grau dargestellt. Die endgültigen Farben müssen lesbar bleiben und auch bei Custom Styles keinen verwirrenden Zustand erzeugen.
6. **Add Break** fügt `--Break--` als technisch gleichartigen Listen-/Header-Eintrag ein, jedoch leer und nicht aufklappbar. Es trägt keinen `>`-Pfeil und kann wie jede andere Zeile verschoben, gelöscht und in der Sichtbarkeit geschaltet werden.
7. **Add Group** steht direkt unter der Liste; Namensvalidierung verhindert weiterhin leere, doppelte oder mit Standard-Commands kollidierende Namen.

#### D2. Aufgeklappte Command-Group

Für normale Gruppen ist die innere Darstellung:

```text
[^] [v] [X] [O] [v Group name]
  [+] [Group name                        ]
  Label: [Button name                    ]
  > Style Overrides
  | command list as before |
  [+ Add Command Step]
```

1. Der frühere `Active`-Checkbox-Text entfällt; nur der aktive, unbeschriftete Button bleibt vor dem Gruppen-Namen.
2. Daneben steht der editierbare Gruppenname. Gültige Änderungen werden sofort gespeichert und aktualisieren alle Referenzen, ohne dass der Header erneut aufgebaut oder geschlossen wird.
3. Darunter steht `Label:` mit einem Eingabefeld. Ein leerer Wert zeigt den Gruppennamen als Platzhalter und bedeutet weiterhin, dass der Button den Gruppennamen verwendet.
4. Label- und Namensänderungen sind unmittelbar persistiert, fokusstabil und ohne ungewolltes Redraw der Liste.
5. Style Overrides, Command-Liste und `+ Add Command Step` bleiben inhaltlich bestehen.
6. Das frühere `Delete Group` im aufklappbaren Inhalt entfällt vollständig.

#### D3. Datenmodell und Laufzeit

1. Die Reihenfolge wird auf strukturierte Einträge mit stabiler Gruppenreferenz und Break-Typ migriert; nicht allein auf Namen. Dies verhindert Probleme beim Umbenennen und bei gleichnamigen/verwaisten Einträgen.
2. Die Button-Bar interpretiert Break-Zeilen weiterhin als Zeilenumbruch und normale Einträge anhand ihrer Sichtbarkeit/Aktivität. Ein Break ohne Commands darf nie ausgeführt oder als Command Reference angeboten werden.
3. Ein deaktivierter Eintrag ist weiterhin editierbar. Das fachliche Aktiv-Verhalten für Ausführung bleibt unverändert, sofern die bestehende Semantik das vorsieht.

**Abnahme:** Die angezeigte Reihenfolge entspricht exakt der Button-Bar. Umbenennen, Verschieben, Löschen mit/ohne CTRL, Einfügen eines Breaks und Sichtbarkeitsschalten funktionieren ohne Datenverlust und ohne schließende Header.

### E. Kontroll- und Gil-Eingabekorrekturen — [x]

#### E1. Checkboxen als `On`/`Off`

**Geltung: nur Variante 3, außer den in Paket G ausdrücklich global markierten Rules-Controls.**

1. Alle Checkbox-basierten Settings werden als `Label [On] [Off]` dargestellt, mit Label davor.
2. Aktive/gewählte Buttons verwenden die bestehende General-V2-Aktivfarbe; der inaktive Zustand bleibt eindeutig neutral.
3. Seiten ohne Variante-3-Redesign erhalten nur diese globale Darstellungsänderung, keine Reorganisation.

#### E2. Gil-Display und editierbare Gil-Werte

**Geltung: nur Variante 3.**

1. Die Bezeichnung **`Gil Visual`** wird zu **`Gil Display`**.
2. Formatiert dargestellte Gil-Werte werden als rechtsbündige Anzeige ohne künstliche Leerzeichen im eigentlichen Text gerendert.
3. Klick auf eine Anzeige wechselt sie in ein Integer-Eingabefeld, fokussiert es automatisch und selektiert den gesamten Inhalt.
4. `Enter` bestätigt, entfernt den Fokus und schaltet wieder auf die eingestellte Gil-Display-Formatierung zurück.
5. Als technische Umsetzung ist ein Label-zu-Input-Wechsel zulässig und wegen des klaren Fokusverhaltens bevorzugt.

#### E3. Einheitliche Breiten

**Geltung: nur Variante 3.**

Schalter und buttonbasierte Auswahllisten erhalten innerhalb einer Settings-Zeile einheitliche, wiederkehrende Breiten. Labels bleiben ausgerichtet; lange Bezeichner dürfen nicht zu überlappenden Controls führen.

### F. Variante-3-Settings: General und Automation — [x]

#### F1. General — [x]

**Geltung: nur Variante 3, zusätzlich zu E1.**

1. Navigationseintrag **`Top Tabs (experimental)`** wird zu **`Top Tabs`**.
2. Die drei Auswahlwerte heißen **`Side`**, **`Burger`** und **`Tabs`**.
3. Tooltip und technische Werte bleiben eindeutig auf Sidebar, Burger-Menu und Top-Tabs abbildbar.

#### F2. Automation — [x]

**Geltung: nur Variante 3.**

Zielstruktur:

```text
Enable:
  [On] [Off] Message Reaction     [Insert regular Regex entries]
  [On] [Off] Dealer Draw
  [On] [Off] Player Draw
  [On] [Off] Auto Activate Trading Players
  [On] [Off] Continue after      [30] Seconds
  Command after Bet-Change       [BankTell]
  Command on insufficient Bank   [BankTell]
  Command Speed                  [........ 10.00s] [Reset]
  Recall Unlock                  [........ 20.00s] [Reset]
```

1. **Message Reaction** ist der gemeinsame Schalter für Reaktionen auf Chat-Nachrichten mittels der unten genannten Regex-Commands. Er ersetzt nicht die individuelle Aktivierung einzelner Regex-Einträge.
2. **Dealer Draw** steuert das automatisierte Ziehen nach Dealer-Regeln.
3. **Player Draw** steuert das initiale Austeilen an Spieler, unabhängig von Message Reaction.
4. **Continue after** nutzt einen frei editierbaren Wert von **10 bis 300 Sekunden**. Das Eingabefeld ist auch bei `Off` bedienbar.
5. **Command after Bet-Change** und **Command on insufficient Bank** ziehen aus Betting nach Automation um. Die bestehende Command-Auswahl bleibt erhalten.
6. **Command Speed** und **Recall Unlock** ziehen aus Time & Delay nach Automation um, jeweils mit dem angeforderten Reset und dem bestehenden, fachlich passenden Wertebereich.
7. Der bisherige Rotationsbereich wird aus Automation entfernt und an einer später festzulegenden Stelle untergebracht (siehe offene Punkte).

#### F3. Insert regular Regex entries — [x]

Der Button fügt die nachfolgenden Standard-Einträge idempotent hinzu. Bestehende benutzerdefinierte Einträge werden nicht überschrieben; bei bereits vorhandenem Eintrag gleichen Namens bzw. gleicher Action wird vor dem Erzeugen eine klare, dokumentierte Duplikatregel angewandt.

| Name | Enabled | Quellen | Mode / Action | Patterns |
| --- | --- | --- | --- | --- |
| Draw | ja | Party, Alliance; nicht Tell/Say/System | Trigger / Auto Hit | `^hit\!*$`, `^hit me\!*$`, `^h\!*$` |
| Stand | ja | Party, Alliance; nicht Tell/Say/System | Trigger / Auto Stand | `^stand\!*$`, `^stando\!*$`, `^standy\!*$`, `^stay\!*$`, `^s\!*$` |
| Double Down | ja | Party, Alliance; nicht Tell/Say/System | Trigger / Auto DD | `^dd\!*$`, `^double down\!*$` |
| Split | ja | Party, Alliance; nicht Tell/Say/System | Trigger / Auto Split | `^split\!*$` |
| Ready | nein | Party, Alliance, Tell, Say; nicht System | Trigger / NextRound | `^ready\!*$`, `^i\'m ready\!*$`, `^r\!*$` |
| Set Bet | nein | Party, Alliance, Tell; nicht Say/System | Trigger / SetBet | `^bet\s+((?:\d[\d.,]*\s*[km]?)\|max\|all\|full\|min)$` |
| Bank Tell | nein | Party, Alliance, Tell, Say; nicht System | Trigger / BankTell | `^bank\?*$`, `^what\'*s my bank\?*$`, `^what is my bank\?*$` |
| Withdraw | nein | Party, Alliance; nicht Tell/Say/System | Trigger / Withdraw | `^withdraw\s+((?:\d[\d.,]*\s*[km]?)\|all\|everything)$` |

Alle Einträge sind nicht case-sensitive. „Party/Alliance“ bildet auf die bereits vorhandene Party-Quelle plus die bestehende Alliance-Routing-Logik ab.

**Abnahme:** Bei leerer Konfiguration erzeugt der Button genau diese acht Gruppen mit den genannten Werten. Ein zweiter Klick dupliziert sie nicht. Message Reaction, Dealer Draw, Player Draw und Continue verhalten sich unabhängig voneinander wie beschrieben.

**Umgesetzt:** Die allgemeine Message-Reaction-Abfrage gate’t ausschließlich die benannten Gameplay-Custom-Regex-Aktionen und lässt System-, Trade- und Variable-Regexes sowie den individuellen Aktivstatus der Gameplay-Einträge unangetastet. Trade- und Variable-Regexes sind für ihren Funktionspfad immer aktiv. Dealer Draw steuert direkt das automatische Dealer-Ziehen. Command Speed bleibt ein Multiplikator. Der Insert-Button erzeugt die sieben Gruppen idempotent.

#### F4. Auto Activate Trading Players — [x]

**Geltung: `[all]`.**

1. Die Automation-Einstellung **`Auto Activate Trading Players`** ist standardmäßig aktiv und in allen drei Ansichtsvarianten verfügbar. Variante 3 zeigt sie im angeforderten `On`/`Off`-Stil; Variante 1 und 2 führen dieselbe globale Einstellung als Checkbox.
2. Der Status **`new`** ist ausschließlich Laufzeitdaten und wird weder in der Konfiguration noch in einer Session gespeichert. Die erste autoritative Gruppen-/Allianzaufnahme nach Pluginstart bzw. -reload bildet nur die Ausgangsbasis: bereits anwesende Mitglieder werden nicht als neu markiert.
3. Bei jeder späteren autoritativen Gruppenänderung erhält ein tatsächlich neu hinzugekommenes, nicht lokales Mitglied den `new`-Status. Beim Verlassen wird der Status verworfen; ein späteres, beobachtetes Wiedereintreten setzt ihn damit erneut.
4. Erkennt der bestehende Trade-Regexpfad einen eingehenden oder ausgehenden, noch nicht abgeschlossenen Handel mit einem solchen Mitglied, wird dieses Mitglied sofort wie durch den vorhandenen `>`-Button aktiviert. Der Status wird dabei immer verbraucht, auch wenn das Mitglied bereits aktiv war. Handelsabschluss, Gil-Buchung und alle übrigen Trade-Abläufe bleiben unverändert.
5. Ein manuelles `X` entfernt den `new`-Status sofort. Weitere Handelsanfragen desselben Mitglieds aktivieren es daher nicht erneut, bis dessen Verlassen und erneutes Beitreten durch eine autoritative Gruppensynchronisation erkannt wurde.
6. Die Option wird in Settings-Import/-Export sowie Settings-Presets mitgeführt. Fehlende Werte alter Konfigurationen übernehmen den sicheren Standard `true`.

**Abnahme:** Mit aktivem Gruppendetektor zunächst eine bestehende Gruppe als Basis synchronisieren. Ein danach beigetretenes Mitglied löst mit dem ersten erkannten Handel genau eine Aktivierung aus; ein zweiter Handel nicht. `X` vor dem ersten Handel verhindert die Aktivierung. Nach beobachtetem Verlassen und Wiedereintritt ist genau eine erneute Aktivierung möglich. Bereits bei Pluginstart vorhandene Mitglieder dürfen nie allein durch einen Handel aktiviert werden.

**Umgesetzt:** `SyncParty` hält einen nicht persistierten, autoritativen Membership-Snapshot und markiert nur nach der ersten Basisaufnahme neue Mitglieder. `TradeManager.SetPartner` löst unmittelbar vor Abschluss des Handels die einmalige Aktivierung aus. Der bestehende Aktivierungspfad wird wiederverwendet, damit Logs, Mid-Round-Verhalten, Session-Sicherung und Companion-Sync identisch zum `>`-Button bleiben.

#### F5. Withdraw-Regex und mengenbasierte Auszahlung — [x]

**Geltung: `[all]`.**

1. Die Trigger-Aktionsauswahl enthält **`Withdraw`**. Der reguläre Automation-Insert erzeugt idempotent den inaktiven, nicht case-sensitiven Party-Trigger **`Withdraw`** mit dem Pattern `^withdraw\s+((?:\d[\d.,]*\s*[km]?)|all|everything)$`. Tell, Say und System sind nicht als Quellen erlaubt.
2. `Withdraw` zählt zu den Gameplay-Message-Reactions und wird bei deaktiviertem Message Reaction nicht ausgeführt. Sein eigener Aktivstatus bleibt davon unabhängig.
3. Numerische Beträge sowie `k`-/`m`-Suffixe werden verarbeitet; `all` und `everything` verwenden den gesamten aktuellen Bankbetrag. Null, negative oder sonst ungültige Werte werden ohne Nebenwirkung ignoriert.
4. Übersteigt der angeforderte Betrag die Bank, wird keine Auszahlung gestartet und exakt `You don't have that much on your bank!` in den Partychat gesendet.
5. Das Payout-Management erhält einen separaten Restbetrag für die angeforderte Auszahlung. Jeder Trade darf weiterhin höchstens eine Million Gil enthalten, wird aber nach erfolgreicher Buchung gegen den erwarteten Chunk validiert und vom Restbetrag abgezogen. Die Auszahlung endet somit auch bei verbleibendem Bankguthaben exakt nach der angeforderten Menge.
6. Die bestehende Vollauszahlung über `Payout` und die Pay-Out-Schaltfläche verwenden dieselbe Implementierung mit dem gesamten Bankbetrag.

**Abnahme:** Mit 2.500.000 Gil Bank `withdraw 1.500m` ausführen und zwei Trades über 1.000.000 und 500.000 erwarten; danach müssen 1.000.000 Gil auf der Bank verbleiben. `withdraw all` und `withdraw everything` zahlen den gesamten Bankbetrag aus. Ein zu hoher Betrag erzeugt genau die Fehlermeldung, `withdraw 0` und ein negativer Custom-Trigger bleiben ohne Aktion. Bei deaktiviertem Message Reaction darf kein Withdraw ausgelöst werden.

**Umgesetzt:** Der Payout-Buffer hält `_remainingAmount` getrennt vom Bankwert, prüft jeden abgeschlossenen Trade auf die exakte Chunk-Höhe und beendet die Auszahlung bei Restbetrag null. Version, Pluginmanifest und Pluginmaster führen `1.9.0.0`.

### G. Rules — [x]

#### G1. Globale Änderungen

**Geltung: `[all]`.**

1. `Soft` wird ein eigener, beschrifteter Toggle-Button: aktiv hervorgehoben, inaktiv grau.
2. `First Deal, then play`, `Player self-rolling` (Umbenennung von `Player rolling for themselves`) und `Hide card suits` werden ebenfalls eigene hervorgehobene Toggle-Buttons.
3. Die übrigen Checkboxen werden in globale `Label [On] [Off]`-Zeilen umgewandelt.
4. Zahlenfelder erhalten einheitliche Breiten; das `Cards`-Feld wird halb so breit wie das zugehörige `Payout`-Feld.
5. `Small Result Messages` heißt **`Short Result Messages`**.
6. Die Example-Output-Vorschau zeigt vier durch `---` getrennte Konstellationen:

```text
Winners: Alice Winner, Bob Winner
Pushed: Cara Push, Dorian Push
Lost: Eve Lost, Finn Lost
Busted: Gina Bust, Hugo Bust
---
Winners: ~
Pushed: Cara Push, Dorian Push
Lost: ~
Busted: Gina Bust, Hugo Bust
---
Winners: Alice Winner
Pushed: Cara Push
Lost: ~
Busted: Gina Bust, Hugo Bust
---
Winners: Gina Bust, Hugo Bust
Pushed: ~
Lost: ~
Busted: ~
```

Die Vorschau verwendet den echten Formatter und nicht eine zweite, abweichende String-Implementierung. `~` steht ausschließlich für leere Datensätze.

#### G2. Nur Variante 3

- Tie-Rule-Bezeichnungen werden zu **`Push`**, **`Player Nat BJ wins`**, **`Dealer Nat BJ wins`** und **`NatBJ > DirtyBJ`**.
- Die Dealer-Zeile lautet **`Dealer stands on [− | Wert | +] [Soft]`**; `Soft` folgt dem Zahlenfeld direkt, ohne den Text `Draw until`.
- Nicht ausdrücklich globale Layout- und Reihenfolgeänderungen bleiben auf die Rules-Ansicht von V3 begrenzt.

**Abnahme:** Die Umbenennungen und Controls erscheinen entsprechend ihrer Scope-Regel; der Ergebnisformatter liefert für alle vier Beispieldatensätze die korrekte Vorschau.

### H. Betting mit Presets — [x]

**Geltung: nur Variante 3.**

1. `Kind` bietet nur **Minimum**, **Normal** und **VIP**.
2. Voreinstellung beim leeren/neuen Satz:

| Active | Kind | VIP-Rang | Name | Amount |
| --- | --- | ---: | --- | ---: |
| ja | Minimum | – | Min | 50,000 |
| ja | Normal | – | Max | 500,000 |
| nein | VIP | 1 | VIP | 1,000,000 |
| nein | VIP | 2 | Lifetime | 2,000,000 |

3. `Minimum` und `Normal` können keinen VIP-Rang führen. Die UI deaktiviert/normalisiert das Rangfeld entsprechend.
4. Auto-Bet-Detection gehört nicht mehr in Betting; die zugehörigen Command-Optionen werden in Automation dargestellt.
5. Oberhalb der Liste entfällt der bisherige Hilfetext.
6. `+ Add VIP` und `+ Add Min-Bet` werden durch **`Add Entry`** ersetzt.
7. Der Minimum-Eintrag ist nicht löschbar.
8. Unter bzw. neben der Editierliste gibt es Presets. Eine Kurzform lautet zum Beispiel: `Min: 50000, Max: 500000, VIP: 1000000, Lifetime: 2000000`.
9. **Save as preset** speichert den aktuellen Satz; `X` löscht das einzelne Preset; `*` setzt dessen Farbe.
10. Der erste Klick auf ein Preset zeigt für zwei Sekunden **`Click again to load this preset`** direkt darunter. Ein zweiter Klick auf denselben Eintrag innerhalb dieses Fensters lädt ihn. Klick auf ein anderes Preset startet dessen eigenes Fenster; nach zwei Sekunden verschwindet der Hinweis.

**Abnahme:** Presets speichern, färben, löschen und laden zuverlässig. Der erste Klick verändert keine Betting-Werte. Minimum kann weder gelöscht noch als VIP konfiguriert werden.

**Umgesetzt:** Die Betlimits wurden um den expliziten Typ `Normal` ergänzt. Alte `VIP`-Einträge mit Rang `0` migrieren idempotent zu diesem Typ; neue strukturierte Entries und Betting-Presets haben bei Import, Presets und Laufzeit Vorrang, die alten Min/Max/VIP-Felder bleiben synchron. Der neue V3-Editor enthält `Add Entry`, das nicht löschbare Minimum, Farbwahl, Umbenennung und das zweistufige Laden von Presets. Seine Tabelle verwendet 96 px für Active, 100 px für Kind, 116 px für VIP, 200 px für Name, einen dynamischen Amount-Restbereich sowie eine unbeschriftete 17-px-Löschspalte; das sichtbare Kind-Dropdown und dessen Auswahl übernehmen jeweils die ursprüngliche ImGui-Breite plus exakt 36 px.

### I. Time & Delay sowie entfernte Message Settings — [x]

#### I1. Time & Delay — [x]

**Geltung: `[all]`.**

1. Der rohe UTC-Offset wird durch eine größere, einfache Button-Auswahl benannter Zeitzonen/Städte ersetzt (beispielsweise Berlin). Nur ein Button ist aktiv.
2. Jeder Button mappt deterministisch auf den bisherigen Offsetwert; die Auswahl speichert auch `UtcOffsetConfigured` korrekt.
3. Command Speed und Recall Unlock werden aus dieser Seite entfernt, da sie in Automation liegen.

**Umgesetzt:** Die feste Städte-/Offsetliste einschließlich Halb- und Viertelstunden ist in allen Ansichtsvarianten verfügbar. Alte ganzzahlige Offsets werden bei der ersten Darstellung auf den ersten passenden Städtenamen migriert; neue Werte speichern Minuten, Städtenamen und einen separaten Summer/Winter-Time-Status. Gibt es keinen passenden Städtenamen, erscheint ein editierbarer `Custom UTC…`-Button. Neue Werte haben beim Laden, Import und in Presets Vorrang vor `UtcOffsetHours`.

#### I2. Message Settings entfernen — [x]

**Geltung: `[all]`.**

1. Der Settings-Tab **Message Settings** entfällt.
2. Die darin konfigurierbare Einstellung wird fest als aktiv behandelt.
3. Das Feld **Seconds snapping delay input field** entfällt ebenfalls; Snapping gilt dauerhaft als aktiv.
4. Bestehende gespeicherte Flags dürfen beim Laden ignoriert werden, ohne ältere Konfigurationen unlesbar zu machen.

**Umgesetzt:** Der Tab ist aus Variante 2 und 3 entfernt. Anti-Double und Sekunden-Snapping werden beim Laden, nach einem Import und nach dem Anwenden eines Presets auf aktiv gesetzt; die Ausführung behandelt sie unabhängig von historischen Flags als aktiv. Die alten Felder und Presetdaten bleiben ausschließlich aus Kompatibilitätsgründen lesbar.

**Abnahme:** Es gibt keinen Message-Settings-Tab mehr; keine UI kann Snapping oder die entfernte Einstellung deaktivieren; command delays verwenden dauerhaft die definierte Snapping-Logik.

### J. Nearby Players und Range Settings — [x]

#### J1. Variante-3-spezifische Nearby-Settings

**Geltung: nur Variante 3.**

1. `Enable Nearby Players Feature` heißt **`Enabled`** und nutzt den `On`/`Off`-Schalter.
2. `No Auto Dequeue` entfällt und wird fachlich als immer aktiv behandelt.
3. `Always show range circle` wird aus dem Nearby-Tab in das Range-Settings-Fenster verschoben.
4. `Nearby Player Custom Command Button` wird ebenfalls ins Range-Settings-Fenster verschoben.

#### J2. Globale Nearby-/Sound-Änderungen

**Geltung: `[all]`.**

1. `Nearby Player Columns` heißt **`Columns`**; die doppelte Bezeichnung nach dem Zahlenfeld entfällt.
2. `Enable Sound on player enter range` heißt **`Player entering area sound trigger`**.
3. Cooldown akzeptiert mindestens **0.02**. Doppelklick öffnet eine Zahleneingabe mit **OK**; Werte außerhalb des festgelegten Minimums/Maximums werden beim Übernehmen begrenzt.
4. Die Sound-Dateiliste zeigt den vollständigen Pfad und pro Zeile `X`, Aktiv-Status, individuelle Lautstärke und Dateiname, zum Beispiel:

```text
[X] [+] [   100 |] Filename.mp3
[X] [ ] [   100 |] Inactive_Filename.mp3
```

5. Der Aktiv-Status wird persistent pro Datei gespeichert. `Select file ...` ersetzt `+ Add Sound`.
6. Jede Datei hat eine eigene Lautstärke. Zur Laufzeit gilt: **effektive Lautstärke = globale Lautstärke × Dateilautstärke**. Beispiel: 50 % global und 30 % Datei ergeben 15 % Wiedergabelautstärke.
7. Bestehende `List<string>`-Sounddateien werden in aktivierte Einträge mit Standard-Dateilautstärke 100 % migriert.

**Abnahme:** Ein deaktivierter Sound wird nicht ausgewählt/abgespielt; zwei aktive Dateien mit verschiedenen Einzelvolumes werden korrekt skaliert; alte Listen werden vollständig migriert und zeigen volle Pfade.

**Umgesetzt:** Sounddateien migrieren in strukturierte Einträge mit `Enabled` und `Volume`; neue Einträge haben Vorrang, die alte Pfadliste wird kompatibel synchronisiert. Die Wiedergabe filtert deaktivierte/fehlende Dateien und multipliziert Gesamt- mit Dateilautstärke. Der Cooldown ist auf `0.02–30.00s` begrenzt und bietet nach Doppelklick die verlangte OK-Eingabe. Range-Kreis und Custom-Command befinden sich im Range-Fenster; Auto-Dequeue ist dauerhaft abgeschaltet. Das Columns-Zahlenfeld verwendet eine versteckte ImGui-ID, so dass `Columns` nur als Zeilenlabel erscheint.

### K. Stats — [x]

**Geltung: `[all]`.**

1. Die Tip-Buttons erscheinen in dieser Reihenfolge: **1k**, **5k**, **10k**, **50k**, **100k**, **250k**, **500k**, **1m**, **Custom**.
2. Die Zahl in `Tips: 0` ist per Doppelklick editierbar; die Eingabe zeigt ein Feld mit **OK**. Speichern aktualisiert die Session-Statistik.
3. Das Wage-Feld wird schmaler. Neben dem Wert steht bei nicht fixem Lohn eine Dropdown-Auswahl: **Gil/Minute**, **Gil/15 Min**, **Gil/30 Min**, **Gil/Hour**, **Gil/2 Hours**.
4. Die Statistikberechnung normalisiert die Ergebnisrate auf das ausgewählte Zeitintervall. Fixed Wage bleibt ein direkter Betrag ohne Intervall-Dropdown.
5. Darstellung:

```text
[+] Fixed Wage [                 123,456,789,000]
[ ] Fixed Wage [  123,456,789,000] [Gil / Hour]
```

**Abnahme:** Alle Tip-Buttons addieren bzw. subtrahieren (mit bestehender Modifier-Semantik) den korrekten Betrag; der bearbeitete Tip-Wert persistiert; jede Wage-Intervalloption rechnet mit dem richtigen Faktor.

**Umgesetzt:** Die Tip-Reihenfolge und Doppelklick-Bearbeitung sind umgesetzt. Ein neuer persistenter `WageIntervalMode` startet kompatibel mit `Gil/Hour`; die Berechnung rundet den jeweils ausgewählten Minuten-/15-Minuten-/30-Minuten-/Stunden-/Zwei-Stunden-Takt nach der vorhandenen Clip-Regel.

### L. Main-Ansicht: Nearby-Sichtbarkeit und Debug — [x]

**Geltung: `[all]`.**

1. Ist das Nearby-Player-Feature deaktiviert, verschwinden in der Main-Ansicht auch **CFG**, **Nby** und **STI**.
2. Der Text **`NEARBY PLAYERS`** selbst wird zum Hide/Show-Button für die Nearby-Liste.
3. `Activate Debug Mode` wird unter den Steuerungsbuttons der Main-Ansicht nicht mehr dargestellt.

**Abnahme:** Deaktivieren des Features blendet alle drei genannten Controls aus. Ein Klick auf den Titel blendet ausschließlich die Liste ein/aus und verändert nicht die Nearbys-Konfiguration.

**Umgesetzt:** V2/V3 blenden `Nby`, `CFG` und `STI` bei deaktiviertem Nearby-Feature aus. Der Titel steuert nur die Sitzungsansicht der Liste; die Konfiguration bleibt unverändert. Der Debug-Aktivierungsbutton ist aus den Main-Headern entfernt. Die Rotation ist als gemeinsamer Grad-Button mit editierbarem aktuellem und schreibgeschütztem gespeichertem Slider in den Main-Headern verfügbar.

## Voraussichtliche betroffene Dateien

Die genaue Aufteilung wird erst bei Implementierungsbeginn anhand des dann aktuellen Standes bestätigt. Der erwartete Kern ist:

- `BlackJackButtler/config.cs`: neue V3-/Preset-/Sound-/Button-/Wage-Daten, sichere Defaults und Migrationseinstiege.
- `BlackJackButtler/windows/win.00.all.cs`: Navigation und Variantendispatch.
- `BlackJackButtler/windows/win.01.main.cs`, `win.01.main.v2.cs`, `win.01.main.nearby.cs`: V3-Main, globale Nearby-Controls und Hide/Show.
- `BlackJackButtler/windows/win.02.messages.cs`, `win.03.regex.cs`, `win.04.commands.cs`, `win.10.ownbuttons.cs`: globale Content-Manager-Änderungen.
- `BlackJackButtler/windows/win.06.settings.cs`, `win.06.settings.v2.cs` sowie eine neue V3-Settings-Datei: Settings-Varianten, gemeinsame Controls und neue V3-Layouts.
- `BlackJackButtler/windows/BJBGui.cs`, `BJBOnOffSwitch.cs`, `BJBStepInput.cs`: fokussichere editorische, On/Off- und Schrittzahl-Helfer.
- `BlackJackButtler/windows/win.09.stats.cs` plus betroffene Statistikmanager: Tip-Editing und Intervallberechnung.
- `BlackJackButtler/network/manager.defaults*.cs`, `regex/regex.models.cs`, `regex/regex.engine.cs`: Standard-Regex-Seeding, Laufzeitgating und Migration.
- `BlackJackButtler/network/manager.nearbyalert.cs`: Auswahl und Lautstärke aktiver Soundeinträge.

## Empfohlene Implementierungsreihenfolge

1. Konfigurationsschema, neue/enumerierte V3-Werte und idempotente Migrationen vorbereiten.
2. Variantendispacher und leere V3-Settings-Struktur hinzufügen; Wechsel zwischen V1/V2/V3 absichern.
3. Gemeinsame UI-Helfer (On/Off, editierbare Zahl-/Gil-Anzeigen, einheitliche Breiten) implementieren.
4. Globale Content-Manager nacheinander: Regex-Tabs, Message-Tabs plus Texteditor, Own Buttons.
5. Globale Main-/Nearby-/Sound- und Message-Settings-Änderungen umsetzen.
6. V3-Automation inklusive idempotentem Regex-Insert und Laufzeitgating implementieren.
7. V3-Rules, Betting-Presets und Time & Delay implementieren.
8. Globale Stats-Änderungen und Result-Preview abschließen.
9. Migrationen mit alten Konfigurationen, Build und gezielte manuelle Abnahme durchführen; erst danach Metadaten/Changelog aktualisieren, falls ein Release vereinbart wird.

## Verifikationsplan

### Statische und Build-Prüfungen

1. `dotnet build BlackJackButtler/BlackJackButtler.csproj --no-restore` ausführen, sofern gültige Restore-Artefakte vorhanden sind.
2. Sicherstellen, dass alle neuen Konfigurationstypen serialisierbar sind und alte JSON-Dateien ohne Ausnahme laden.
3. Sicherstellen, dass keine entfernte UI-Option weiterhin einen deaktivierbaren Laufzeitpfad steuert.

### Gezielte manuelle Tests im Dalamud-UI

1. Jede Variante 1/2/3 öffnen, wechseln und Plugin neu laden; Daten und Presets vergleichen.
2. Regex- und Message-Tabs mit Standard-, Custom- und gemischten alten Daten testen.
3. Für den Multiline-Editor alle drei Back-Dialogwege sowie leere und mehrzeilige Einträge testen.
4. Eigene Buttons: neue Gruppe, Rename im offenen Header, Label, Shift/Reihenfolge, Break, CTRL-Löschen, Sichtbarkeit und Button-Bar ausführen.
5. Automation: jeden On/Off-Schalter, Grenzen für Continue, Reset-Buttons und den Regex-Insert zweimal testen.
6. Rules: Vorschau gegen die vier vorgeschriebenen Datensätze vergleichen.
7. Betting: Basisentries, nicht löschbares Minimum, Preset-Doppelklickfenster und Preset-Farbe testen.
8. Nearby: Soundmigration, volle Pfade, aktiv/inaktiv, Einzel- und Gesamtlautstärke, kompakten Einzeldatei-Slider, Cooldown-Doppelklick und Test-Ausgabe (vorhandene sowie fehlende Datei) sowie versteckte Main-Controls testen. Danach `Enabled` wiederholt von **Off** nach **On** und zurück schalten, auch bei kleinem Hauptfenster; Text darf nicht transparent werden und es darf keine ImGui-Assertion oder ein Client-Absturz auftreten.
9. Stats: Tip-Reihenfolge, Tip-Doppelklick und alle fünf Wage-Intervalle gegen nachvollziehbare Zeitspannen testen.
10. Trading-Player-Autoaktivierung: Baseline nach Reload, späterer Beitritt, erster/zweiter Handel, manuelles `X` sowie Leave/Rejoin exakt gemäß F4 testen.
11. Withdraw: Aktiv-/Inaktiv-Gate, Message-Reaction-Gate, numerische Mengen, `all`/`everything`, zu hohe/ungültige Werte sowie mehrere 1-Mio.-Chunks gemäß F5 testen.

## Offene Punkte für die Prüfung dieses Fahrplans

1. **Beschriftung des Multiline-Umschalters:** Die Anforderung nennt für den Umschaltknopf `List Edit Mode`, obwohl von der Listen- in die Textblockansicht gewechselt wird. Soll dieser Text exakt in der Listenansicht stehen, oder soll er den jeweils **Zielmodus** benennen (dann wäre in der Liste `Text Edit Mode` und im Textblock `List Edit Mode`)?
    Antwort des Nutzers: "List Edit Mode" soll quasi der aktuelle Standard modus sein. Der Button soll "List Edit Mode" anzeigen, wenn der Text-Edit-Mode aktiv ist, um wieder in den List-Edit-Mode zu wechseln. Und vice versa.
2. **Leere Zeilen im Multiline-Editor:** Der sichere, verlustfreie Standard wäre, jede physische Zeile einschließlich leerer Zeilen als Message zu behalten. Falls Leerzeilen beim Speichern verworfen werden sollen, muss dies hier explizit festgelegt werden.
    Antwort des Nutzers: Leerzeilen werden verworfen
3. **Button `[O]`:** Er wird als Sichtbarkeit beschrieben; der Fahrplan führt ihn daher auch für Breaks. Soll ein Break bei unsichtbarem Zustand in der Button-Bar keinen Zeilenumbruch erzeugen? Dies ist die naheliegende Umsetzung, sollte aber bestätigt werden.
    Antwort des Nutzers: Bestätigt. Nicht sichtbare breaks erzeugen keine breaks.
4. **Rotation:** Sie soll nicht mehr in Automation stehen, ihr Zielort wurde nicht genannt. Vorschlag: unter `Time & Delay` in Variante 3, ohne Funktionsänderung. Falls sie ganz entfallen soll, muss die gewünschte Persistenz/Kompatibilität festgelegt werden.
    Antwort des Nutzers: Rotation sollte in den Main-Bereich versetzt werden. Dieser sollte ein eigener Button sein neben CFG und TBL und so. Dieser sollte schlicht die ganzzahl-rotation anzeigen. Klickt man drauf, erhält man einen slider mit der aktuellen Rotation sowie ein darunter platzierter gleich großer Slider, der die gespeicherte Rotation anzeigt. Der zweite slider soll nicht bearbeitbar sein. Setzt man die Rotation und drückt auf OK, wird die gespeicherte Rotation entsprechend überschrieben mit der definierten Rotation. Abbrechen schließt quasi nur diese kleine Ansicht, ohne was zu ändern.
5. **Zeitstädte und Sommerzeit:** Die gewünschte Liste wird als feste Offset-Auswahl umgesetzt. Soll sie nur einen aktuellen UTC-Offset speichern (wie heute), oder soll etwa `Berlin` automatisch Sommer-/Winterzeit berücksichtigen? Letzteres erfordert eine Zeitzonen-ID statt nur `UtcOffsetHours`.
    Antwort des Nutzers: Sommer/Winterzeit (Summer/Winter-Time) soll als separater On/Off-Button unterhalb angezeigt werden, der dann die ausgewählte Zeitzone einfach um den Sommer/Winterzeit-Versatz abändert.
6. **Betting-Preset-Name:** Der Auftrag nennt `Save as preset`, aber keinen Namensdialog. Vorschlag: nach Klick ein englisches Namens-Popup mit eindeutigem Defaultnamen und Bestätigung öffnen.
    Antwort des Nutzers: Nutze ein Standard-Namen als standard: "New Preset". Dabei soll vorher eine schlichte schleife prüfen, ob die durchnummierung ("New Preset", New Preset 2", "New Preset 3" ...) belegt ist und entsprechend die nächst freie Nummer, beginnend ab 2, wählen, insofern "New Preset" ohne Nummer auch belegt ist. Presets können umbenannt werden.
7. **Nicht als `[all]` markierte Umbenennungen in Rules:** Dieser Plan behandelt sie gemäß Scope-Regel als V3-only. Falls sämtliche Rules-Textumbenennungen global gelten sollen, bitte als Korrektur markieren.
    Antwort des Nutzers: Die Text-Umbenennungen, die nicht als [all] definiert sind, bestehen nur in V3.

## Nicht Bestandteil dieser Etappe

- Keine Änderung der allgemeinen Visual-Seite außer unvermeidbaren, globalen Hilfsfunktionen.
- Die Version wurde auf ausdrücklichen Auftrag auf `1.9.0.0` gesetzt. Release-Packaging bleibt bis zur Ingame-Abnahme und einem separaten Releaseauftrag ausgenommen. Der angeforderte Ingame-Changelog ist bereits umgesetzt.
- Keine Änderung der Blackjack-Kernregeln, Regex-Ausführungssemantik oder Button-Bar-Funktionalität, soweit sie nicht explizit für die oben aufgeführten UI-/Automationsanforderungen nötig ist.

Nutzer-Zusatz:
- Die changelog.md muss basierend auf diesen Änderungen umfangreich und detailliert, aber nicht redundant aufgebaut sein. Diese soll in kategorien der Änderungen selbst gruppiert werden, sodass es übersichtlicher ist. Die Reihenfolge der Einträge muss dabei einem konsistenten rythmus folgen. Dass halt die Kategorien innerhalb stets der selben reihenfolge der bereiche folgen. Quasi wie eine Sortierung, aber auf die bereiche, die betroffen sind, bezogen.

**Umgesetzt:** `changelog.md` ist die kategorisierte, vollständige Quelle. Sie wird als Embedded Resource in den Ingame-Changelog-Dialog geladen; die Plugin-Metadaten enthalten eine kompakte, darauf verweisende Zusammenfassung. Die Bereiche sind nun in einer konsistenten Produktreihenfolge sortiert; Detailkorrekturen sind ausschließlich im abschließenden Punkt `Different minor optical and logical fixes.` zusammengefasst.

## Aktueller Verifikationsstand

- `git diff --check` ist fehlerfrei.
- Die lokale JSON-Pluginmetadatei wurde syntaktisch geprüft.
- `dotnet build BlackJackButtler/BlackJackButtler.csproj --configuration Debug --no-restore --verbosity minimal` ist erfolgreich: **0 Warnungen, 0 Fehler**.
- Der fehlerhafte On/Off-Style-Pop beim Wechsel von Off nach On wurde behoben. Alle Aufrufer verwenden nun den zentralen, verbundenen `BJBOnOffSwitch`; orange ausgewählte Hälften haben schwarzen Text. Schrittzahlfelder verwenden global das zusammenhängende `− | Eingabe | +`-Layout mit quadratischem, mindestens 20 px breitem Mittelsegment; der VIP-Rang im V3-Betting-Editor ist doppelt so breit. Die Custom-Button-Styleüberschrift trennt sichtbaren Text und interne ID sauber und lautet wieder nur `Default`. Die Nearby-Child-Ansicht lehnt nicht-positive verfügbare Größen sicher ab. Einzeldatei-Lautstärken sind Slider, und der Test-Button meldet fehlende/unspielbare Dateien statt still abzubrechen. Ein gezielter Ingame-Retest dieser Pfade steht aus.
- Der Systembereich für die Card-Companion-App und der Defaults-Datei-Reset sind in V1/V2 als kommentierter, nicht gerenderter Code erhalten. Die Draw-Logic- und Commands-Hilfetexte sind entfernt; der Message-Reset heißt `Restore default messages (Hard reset)`.
- Ausstehend ist die manuelle Ingame-Abnahme aller Varianten, Migrationsfälle und Interaktionen aus dem Verifikationsplan.
