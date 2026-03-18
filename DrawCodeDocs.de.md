# DrawLogic Scripting-Referenz (Deutsch)

DrawLogic ist ein skriptbares Welt-Zeichensystem fuer BlackJack Buttler. Scripts werden pro Frame ausgefuehrt und zeichnen 3D-Formen an Spielerpositionen in der Spielwelt.

## Externe Script-Dateien

Scripts werden als `.txt`-Dateien im `drawlogic/`-Unterverzeichnis des Plugin-Config-Ordners gespeichert. Jeder DrawLogic-Eintrag referenziert eine Datei ueber die `ScriptPath`-Eigenschaft.

- **Reload-Button**: Liest die Datei erneut von der Festplatte (leert den Cache).
- **Auto-Reload**: Wenn aktiviert (nur im Debug-Modus), wird die Datei auf Aenderungen ueberwacht und bei Modifikation automatisch pro Frame neu geladen. Nur ein Eintrag kann gleichzeitig Auto-Reload aktiv haben.
- **Browse (...)**: Aendert den relativen Dateipfad fuer einen Eintrag.

Beim Erstellen eines neuen Eintrags wird automatisch eine `.txt`-Datei generiert. Beim Loeschen wird die Datei in `{name}.del.{datum}.{uhrzeit}.txt` umbenannt (Soft-Delete).

### Migration

Bestehende Inline-Scripts werden beim ersten Laden nach dem Update automatisch in `.txt`-Dateien migriert.

---

## Zeichenfunktionen

| Funktion | Beschreibung |
|----------|-------------|
| `BeginShape(x, y, z)` | Neue Form an Weltposition starten |
| `SetDrawColor(r, g, b, a)` | Zeichenfarbe setzen (0.0-1.0 pro Komponente) |
| `BeginPath()` | Neuen Pfad innerhalb der aktuellen Form starten |
| `MoveTo(x, y, z)` | Zu lokaler Offset-Position bewegen |
| `LineTo(x, y, z)` | Linie zu lokaler Offset-Position zeichnen |
| `EndPath()` | Pfad abschliessen (offene Polylinie) |
| `ClosePath()` | Pfad abschliessen (geschlossenes Polygon) |
| `FinishShape()` | Aktuelle Form finalisieren |

## Form-Funktionen

| Funktion | Beschreibung |
|----------|-------------|
| `Draw()` | Aktuelle Form rendern und alle Variablen loeschen |
| `Move(x, y, z)` | Aktuelle Form verschieben |
| `Rotate(winkel)` | Form um Winkel in Radiant drehen |
| `RotateTowards(x, z)` | Form in Richtung Welt-X/Z-Position drehen |
| `SetLineThickness(t)` | Linienstaerke in Pixeln setzen |

## Steuerungsfunktionen

| Funktion | Beschreibung |
|----------|-------------|
| `CallDrawLogic("name")` | Anderen DrawLogic-Eintrag aufrufen |
| `CallDrawLogic("name", x, y, z)` | Aufruf mit eigener Weltposition |

- Iterate-Eintraege iterieren ueber alle aktiven Spieler + Dealer.
- Nicht-Iterate-Eintraege verwenden den aktuellen Kontext.
- Maximale Aufruftiefe: 10.

---

## Block-Anweisungen

### IterateHand

Fuehrt den Body einmal pro Hand des aktuellen Spielers aus.

```
IterateHand {
    // Body wird einmal pro Hand ausgefuehrt
    // Setzt <HandIndex>, <HandsTotal>, <HandPoints>, etc.
}
```

### IterateCard

Fuehrt den Body einmal pro Karte der aktuellen Hand aus (oder aktive Hand wenn ausserhalb von IterateHand).

```
IterateCard {
    // Body wird einmal pro Karte ausgefuehrt
    // Setzt <CardIndex>, <CardsTotal>, <CardNumber>, etc.
}
```

Bloecke koennen verschachtelt werden: `IterateHand { IterateCard { ... } }`.

### if (Bedingungsblock)

Fuehrt den Body nur aus wenn die Bedingung erfuellt ist. Tokens werden vor dem Vergleich aufgeloest.

```
if <token> = <erwartet> {
    // Body wird nur ausgefuehrt wenn Token gleich erwartet
}
```

**Beispiele:**
```
if <isdealer> = 1 {
    SetDrawColor(1, 0, 0, 1)
}

if <HandBusted> = 0 {
    // Nur nicht-geplatzte Haende zeichnen
}

if <IsPlaying> = 1 {
    // Nur fuer aktive Spieler zeichnen
}
```

Der Vergleich ist ein exakter String-Match (Gross-/Kleinschreibung beachten) nach Token-Ersetzung.

---

## Variablen-Funktionen

| Funktion | Beschreibung |
|----------|-------------|
| `SetVar("name", wert)` | Variable im aktuellen Scope setzen (Spieler + Hand/Karten-Kontext) |
| `UnVar("name")` | Variable im aktuellen Scope entfernen |
| `setVarH("name", handIdx, wert)` | Variable im Hand-Scope setzen |
| `unVarH("name", handIdx)` | Variable im Hand-Scope entfernen |
| `setVarC("name", hIdx, cIdx, wert)` | Variable im Karten-Scope setzen |
| `unVarC("name", hIdx, cIdx)` | Variable im Karten-Scope entfernen |

- Variablen werden nach jedem `Draw()`-Aufruf geloescht.
- `GetVar`-Fallback-Kette: Karten-Scope > Hand-Scope > Spieler-Scope > 0.

---

## Mathematische Funktionen

Verfuegbar in jedem numerischen Argument:

| Funktion | Beschreibung |
|----------|-------------|
| `Ceil(a)` | Aufrunden |
| `Floor(a)` | Abrunden |
| `Sin(a)` | Sinus |
| `Cos(a)` | Cosinus |
| `Sqrt(a)` | Quadratwurzel |
| `Min(a, b)` | Minimum |
| `Max(a, b)` | Maximum |
| `Clamp(a, min, max)` | Wert auf Bereich begrenzen |
| `Mul(a, b)` | Multiplizieren |
| `Div(a, b)` | Dividieren (sicher, gibt 0 bei /0 zurueck) |
| `Mod(a, b)` | Modulo |
| `Plus(a, b)` | Addieren |
| `Minus(a, b)` | Subtrahieren |
| `GetVar("name")` | Variable lesen (mit Fallback) |
| `getVarH("name", h)` | Variable im Hand-Scope lesen |
| `getVarC("name", h, c)` | Variable im Karten-Scope lesen |

Operatoren: `+`, `-`, `*`, `/`, `%` und Klammern werden ebenfalls unterstuetzt.

---

## Tokens

### Positions-Tokens

| Token | Beschreibung |
|-------|-------------|
| `<pos>.x` | Spieler X-Weltposition |
| `<pos>.y` | Spieler Y-Weltposition |
| `<pos>.z` | Spieler Z-Weltposition |
| `<rotation>` | Spieler-Rotation (Radiant) |

### Spieler-Tokens

| Token | Beschreibung |
|-------|-------------|
| `<name>` | Spieler-Anzeigename |
| `<NameW>` | Spieler Name@Welt |
| `<score>` | Aktueller Hand-Score |
| `<cards>` | Aktuelle Hand-Karten als String |
| `<cardcount>` | Anzahl Karten in aktueller Hand |
| `<bank>` | Spieler-Bank (numerisch) |
| `<BankF>` | Spieler-Bank (formatiert: 1,000,000) |
| `<bet>` | Spieler-Einsatz (numerisch) |
| `<BetF>` | Spieler-Einsatz (formatiert) |
| `<MaxBet>` | Effektiver Max-Einsatz (numerisch) |
| `<MaxBetF>` | Effektiver Max-Einsatz (formatiert) |
| `<handindex>` | Aktueller Hand-Index |
| `<handcount>` | Anzahl der Haende |
| `<isdealer>` | 1 wenn Dealer, 0 wenn Spieler |
| `<IsPlaying>` | 1 wenn aktiv (nicht Hold/Bench) |
| `<IsCurrentTurn>` | 1 wenn Spieler an der Reihe |

### Config-Tokens

| Token | Beschreibung |
|-------|-------------|
| `<Scale>` | DrawLogic-Skalierungseinstellung |
| `<OffsetX>` | DrawLogic X-Offset |
| `<OffsetY>` | DrawLogic Y-Offset |
| `<OffsetZ>` | DrawLogic Z-Offset |
| `<OffsetR>` | DrawLogic Rotations-Offset |

### Status-Flags (1 = wahr, 0 = falsch)

| Token | Beschreibung |
|-------|-------------|
| `<focused>` | Spieler ist an der Reihe |
| `<nearby>` | Innerhalb des Distanz-Limits |
| `<visible>` | WorldToScreen sichtbar |
| `<online>` | In ObjectTable vorhanden |
| `<ingroup>` | In der Gruppe |
| `<groupexists>` | Gruppe existiert |

### Spielstatus (1 = wahr, 0 = falsch)

| Token | Beschreibung |
|-------|-------------|
| `<isbust>` | Hand ist geplatzt |
| `<isstand>` | Hand steht |
| `<isblackjack>` | Natuerlicher Blackjack |
| `<isdone>` | Spieler ist fertig |
| `<isdd>` | Double Down aktiv |

### Hand-Tokens (innerhalb IterateHand)

| Token | Beschreibung |
|-------|-------------|
| `<HandIndex>` | Aktueller Hand-Schleifen-Index |
| `<HandsTotal>` | Gesamtanzahl der Haende |
| `<HandPoints>` | Bester Score dieser Hand |
| `<HandPointsB>` | Niedrigerer Soft-Hand-Score (0 wenn Hard) |
| `<HandActive>` | 1 wenn dies die aktive Hand ist |
| `<HandBusted>` | 1 wenn diese Hand geplatzt ist |

### Karten-Tokens (innerhalb IterateCard)

| Token | Beschreibung |
|-------|-------------|
| `<CardIndex>` | Aktueller Karten-Schleifen-Index |
| `<CardsTotal>` | Gesamtkarten in dieser Hand |
| `<CardNumber>` | Kartenwert (1=A, 2-10, 11=J, 12=Q, 13=K) |
| `<CardColor>` | Farbe (0=Pik, 1=Kreuz, 2=Herz, 3=Karo) |
| `<CardColorR>` | Farbton Rot-Komponente |
| `<CardColorG>` | Farbton Gruen-Komponente |
| `<CardColorB>` | Farbton Blau-Komponente |
| `<CardAge>` | 0 bis 1 ueber 3 Sekunden seit Karte gezogen |

---

## Beispiel: Goldenes Kreuz pro Hand

```
SetDrawColor(1.0, 0.85, 0.0, 0.7)
IterateHand {
    BeginShape(<pos>.x, <pos>.y, <pos>.z)
    BeginPath()
    MoveTo(-0.3, Mul(<HandIndex>, 0.2), -0.3)
    LineTo(0.3, Mul(<HandIndex>, 0.2), 0.3)
    EndPath()
    BeginPath()
    MoveTo(0.3, Mul(<HandIndex>, 0.2), -0.3)
    LineTo(-0.3, Mul(<HandIndex>, 0.2), 0.3)
    EndPath()
}
FinishShape()
Draw()
```

## Beispiel: Roter Kreis nur fuer Dealer

```
if <isdealer> = 1 {
    SetDrawColor(1, 0, 0, 0.8)
    BeginShape(<pos>.x, <pos>.y, <pos>.z)
    BeginPath()
    MoveTo(0.5, 0, 0)
    LineTo(0.35, 0, 0.35)
    LineTo(0, 0, 0.5)
    LineTo(-0.35, 0, 0.35)
    LineTo(-0.5, 0, 0)
    LineTo(-0.35, 0, -0.35)
    LineTo(0, 0, -0.5)
    LineTo(0.35, 0, -0.35)
    ClosePath()
    FinishShape()
    Draw()
}
```
