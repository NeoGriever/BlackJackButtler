# DrawLogic Scripting Reference (English)

DrawLogic is a scriptable world-drawing system for BlackJack Buttler. Scripts execute per frame and draw 3D shapes at player positions in the game world.

## External Script Files

Scripts are stored as `.txt` files in the `drawlogic/` subdirectory of your plugin config folder. Each DrawLogic entry references a file via its `ScriptPath` property.

- **Reload Button**: Re-reads the file from disk (clears cache).
- **Auto-Reload**: When enabled (Debug Mode only), the file is watched for changes and automatically reloaded every frame when modified. Only one entry can have Auto-Reload active at a time.
- **Browse (...)**: Change the relative file path for an entry.

When a new entry is created, a `.txt` file is automatically generated. When an entry is deleted, the file is renamed to `{name}.del.{date}.{time}.txt` (soft delete).

### Migration

Existing inline scripts are automatically migrated to `.txt` files on first load after the update.

---

## Drawing Functions

| Function | Description |
|----------|-------------|
| `BeginShape(x, y, z)` | Start a new shape at world position |
| `SetDrawColor(r, g, b, a)` | Set draw color (0.0-1.0 per component) |
| `BeginPath()` | Start a new path within the current shape |
| `MoveTo(x, y, z)` | Move to local offset position |
| `LineTo(x, y, z)` | Draw line to local offset position |
| `EndPath()` | Finish path (open polyline) |
| `ClosePath()` | Finish path (closed polygon) |
| `FinishShape()` | Finalize the current shape |

## Shape Functions

| Function | Description |
|----------|-------------|
| `Draw()` | Render the current shape and clear all variables |
| `Move(x, y, z)` | Translate current shape |
| `Rotate(angle)` | Rotate shape by angle in radians |
| `RotateTowards(x, z)` | Rotate shape towards world X/Z position |
| `SetLineThickness(t)` | Set line thickness in pixels |

## Control Functions

| Function | Description |
|----------|-------------|
| `CallDrawLogic("name")` | Call another DrawLogic entry by name |
| `CallDrawLogic("name", x, y, z)` | Call with custom world position |

- Iterate entries iterate over all active players + dealer.
- Non-iterate entries use the current context.
- Maximum call depth: 10.

---

## Block Statements

### IterateHand

Runs the body once per hand of the current player.

```
IterateHand {
    // body runs once per hand
    // sets <HandIndex>, <HandsTotal>, <HandPoints>, etc.
}
```

### IterateCard

Runs the body once per card of the current hand (or active hand if outside IterateHand).

```
IterateCard {
    // body runs once per card
    // sets <CardIndex>, <CardsTotal>, <CardNumber>, etc.
}
```

Blocks can be nested: `IterateHand { IterateCard { ... } }`.

### if (Conditional Block)

Runs the body only when the condition is met. Tokens are resolved before comparison.

```
if <token> = <expected> {
    // body runs only when token equals expected
}
```

**Examples:**
```
if <isdealer> = 1 {
    SetDrawColor(1, 0, 0, 1)
}

if <HandBusted> = 0 {
    // draw only non-busted hands
}

if <IsPlaying> = 1 {
    // draw only for active players
}
```

The comparison is an exact string match (case-sensitive) after token replacement.

---

## Variable Functions

| Function | Description |
|----------|-------------|
| `SetVar("name", value)` | Set variable at current scope (player + hand/card context) |
| `UnVar("name")` | Remove variable at current scope |
| `setVarH("name", handIdx, value)` | Set variable at hand scope |
| `unVarH("name", handIdx)` | Remove variable at hand scope |
| `setVarC("name", hIdx, cIdx, value)` | Set variable at card scope |
| `unVarC("name", hIdx, cIdx)` | Remove variable at card scope |

- Variables are cleared after each `Draw()` call.
- `GetVar` fallback chain: card scope > hand scope > player scope > 0.

---

## Math Functions

Available inside any numeric argument:

| Function | Description |
|----------|-------------|
| `Ceil(a)` | Round up |
| `Floor(a)` | Round down |
| `Sin(a)` | Sine |
| `Cos(a)` | Cosine |
| `Sqrt(a)` | Square root |
| `Min(a, b)` | Minimum |
| `Max(a, b)` | Maximum |
| `Clamp(a, min, max)` | Clamp value to range |
| `Mul(a, b)` | Multiply |
| `Div(a, b)` | Divide (safe, returns 0 on /0) |
| `Mod(a, b)` | Modulo |
| `Plus(a, b)` | Add |
| `Minus(a, b)` | Subtract |
| `GetVar("name")` | Read variable (with fallback) |
| `getVarH("name", h)` | Read variable at hand scope |
| `getVarC("name", h, c)` | Read variable at card scope |

Operators: `+`, `-`, `*`, `/`, `%` and parentheses are also supported.

---

## Tokens

### Position Tokens

| Token | Description |
|-------|-------------|
| `<pos>.x` | Player X world position |
| `<pos>.y` | Player Y world position |
| `<pos>.z` | Player Z world position |
| `<rotation>` | Player rotation (radians) |

### Player Tokens

| Token | Description |
|-------|-------------|
| `<name>` | Player display name |
| `<NameW>` | Player name@world |
| `<score>` | Current hand score |
| `<cards>` | Current hand cards string |
| `<cardcount>` | Number of cards in current hand |
| `<bank>` | Player bank (numeric) |
| `<BankF>` | Player bank (formatted: 1,000,000) |
| `<bet>` | Player bet (numeric) |
| `<BetF>` | Player bet (formatted) |
| `<MaxBet>` | Effective max bet (numeric) |
| `<MaxBetF>` | Effective max bet (formatted) |
| `<handindex>` | Current hand index |
| `<handcount>` | Number of hands |
| `<isdealer>` | 1 if dealer, 0 if player |
| `<IsPlaying>` | 1 if active (not hold/bench) |
| `<IsCurrentTurn>` | 1 if player's turn |

### Config Tokens

| Token | Description |
|-------|-------------|
| `<Scale>` | DrawLogic scale setting |
| `<OffsetX>` | DrawLogic X offset |
| `<OffsetY>` | DrawLogic Y offset |
| `<OffsetZ>` | DrawLogic Z offset |
| `<OffsetR>` | DrawLogic rotation offset |

### State Flags (1 = true, 0 = false)

| Token | Description |
|-------|-------------|
| `<focused>` | Player's turn |
| `<nearby>` | Within distance cap |
| `<visible>` | WorldToScreen visible |
| `<online>` | In ObjectTable |
| `<ingroup>` | In party |
| `<groupexists>` | Party exists |

### Game State (1 = true, 0 = false)

| Token | Description |
|-------|-------------|
| `<isbust>` | Hand is bust |
| `<isstand>` | Hand is standing |
| `<isblackjack>` | Natural blackjack |
| `<isdone>` | Player is done |
| `<isdd>` | Double down active |

### Hand Tokens (inside IterateHand)

| Token | Description |
|-------|-------------|
| `<HandIndex>` | Current hand loop index |
| `<HandsTotal>` | Total number of hands |
| `<HandPoints>` | Best score of this hand |
| `<HandPointsB>` | Lower soft-hand score (0 if hard) |
| `<HandActive>` | 1 if this is the active hand |
| `<HandBusted>` | 1 if this hand is bust |

### Card Tokens (inside IterateCard)

| Token | Description |
|-------|-------------|
| `<CardIndex>` | Current card loop index |
| `<CardsTotal>` | Total cards in this hand |
| `<CardNumber>` | Card value (1=A, 2-10, 11=J, 12=Q, 13=K) |
| `<CardColor>` | Suit (0=Spades, 1=Clubs, 2=Hearts, 3=Diamonds) |
| `<CardColorR>` | Suit color red component |
| `<CardColorG>` | Suit color green component |
| `<CardColorB>` | Suit color blue component |
| `<CardAge>` | 0 to 1 over 3 seconds since card was drawn |

---

## Example: Golden Cross per Hand

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

## Example: Dealer-only Red Circle

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
