# BlackJack Buttler (BJB)

A Dalamud plugin for FFXIV that provides a full-featured Blackjack game engine for in-game dealer hosts. It handles card tracking, point calculation, bankroll management via trade detection, and automated dealer communication through configurable command chains.

## Table of Contents

- [Features](#features)
- [Installation](#installation)
- [Commands](#commands)
- [Usage](#usage)
  - [Group Setup](#group-setup)
  - [Handling Bets](#handling-bets)
  - [Starting a Round](#starting-a-round)
  - [Player Turns](#player-turns)
  - [Payouts](#payouts)
- [Game Phases](#game-phases)
- [Deck Simulation](#deck-simulation)
- [Trade Detection](#trade-detection)
- [Command Chain System](#command-chain-system)
  - [Command Groups](#command-groups)
  - [Placeholders and Tokens](#placeholders-and-tokens)
  - [Message Batches](#message-batches)
- [Regex Engine](#regex-engine)
  - [Default Patterns](#default-patterns)
  - [Supported Actions](#supported-actions)
- [Configuration](#configuration)
  - [Game Rules](#game-rules)
  - [Payout Multipliers](#payout-multipliers)
  - [Auto-Dealing](#auto-dealing)
  - [Bet Limits](#bet-limits)
  - [User Levels](#user-levels)
- [State Management](#state-management)
  - [Snapshots and Timeline](#snapshots-and-timeline)
  - [Defaults Migration](#defaults-migration)
- [Dropbox Integration](#dropbox-integration)
- [UI Tabs](#ui-tabs)
- [Technical Stack](#technical-stack)
- [License](#license)
- [Repository](#repository)

## Features

- **Card Tracking** - Detects `/dice party 13` rolls and maps values 1-13 to card faces (Ace through King). Tracks all hands including split hands.
- **Bankroll Management** - Intercepts FFXIV trade addon events to parse Gil transfers and automatically update player balances.
- **Command Chains** - Configurable sequences of chat commands, emotes, and delays that execute automatically during game actions.
- **Full Blackjack Rules** - Supports Hit, Stand, Double Down, and Split. Splits can produce up to 10 hands per player (configurable).
- **Timeline Control** - Snapshot-based history system that allows rewinding the game state to any previous action.
- **Payout Helper** - Integration with the Dropbox plugin for Gil transfers exceeding 1,000,000. Falls back to a manual multi-trade payout system.
- **Regex Engine** - Pattern-based message detection with compiled regex caching. Recognizes player intents in multiple languages.
- **Statistics** - Tracks income, expenses, and round counts across sessions.

## Installation

Add the following custom repository URL in Dalamud's plugin installer:

```
https://puni.sh/api/repository/vali
```

Search for "BlackJack Buttler" and install.

**Dependency:** [ECommons](https://github.com/NightmareXIV/ECommons) (resolved automatically via NuGet).

## Commands

| Command | Description |
|---------|-------------|
| `/bjb` | Opens the BlackJack Buttler main window. |

## Usage

### Group Setup

Open the Main tab and use the Party Sync button. The plugin reads `IPartyList` and populates the player table with all current party members. Players can also be added manually.

### Handling Bets

When a player trades Gil to the dealer, the plugin detects the trade via `AddonLifecycle` hooks on the Trade addon. The transferred amount is parsed from chat messages using regex patterns and added to the player's bank. Set the bet amount per player in the main table before starting a round.

### Starting a Round

Click "Start New Round" to transition into the `InitialDeal` phase. The plugin executes the `DealStart` command group (dealer card reveal) followed by the `Initial` command group for each player (two `/dice party 13` rolls per player).

### Player Turns

During the `PlayersTurn` phase, the plugin displays each player's hand and available actions. Players communicate their intent via party chat (detected by the regex engine) or the dealer clicks the corresponding action button (Hit, Stand, DD, Split). Each action triggers its associated command group.

### Payouts

After the dealer's turn completes, the plugin calls `EvaluateFinalResults()`. This compares each player hand against the dealer's final score, applies the configured win multipliers, and updates bank balances. The `Pay Out` button initiates Gil transfers.

## Game Phases

The game engine operates as a state machine with the following phases:

```
Waiting --> InitialDeal --> PlayersTurn --> DealerTurn --> Payout --> Waiting
```

| Phase | Description |
|-------|-------------|
| `Waiting` | No active round. Players can be added, bets set. |
| `InitialDeal` | Two cards dealt to each player via `/dice party 13`. |
| `PlayersTurn` | Players act on their hands (Hit, Stand, DD, Split). |
| `DealerTurn` | Dealer draws until reaching the configured threshold. |
| `Payout` | Results evaluated, winnings calculated, balances updated. |

## Deck Simulation

The plugin simulates a 12-deck shoe (624 cards total). When a `/dice party 13` result arrives, the corresponding card value (1-13) is drawn from the shoe. If all cards of a given value are exhausted, the shoe reshuffles automatically.

| Dice Value | Card | Blackjack Points |
|------------|------|-------------------|
| 1 | Ace | 1 or 11 |
| 2-10 | 2-10 | Face value |
| 11 | Jack | 10 |
| 12 | Queen | 10 |
| 13 | King | 10 |

Card suits (Diamonds, Clubs, Spades, Hearts) are assigned randomly from available cards of the rolled value.

**Hand Scoring:**
- The engine calculates both the minimum score (all Aces as 1) and the maximum score (all Aces as 11).
- The best score is `max` if `max <= 21`, otherwise `min`.
- A hand is bust when `min > 21`.
- A natural Blackjack requires exactly 2 cards summing to 21.

## Trade Detection

Trade detection operates via three `AddonLifecycle` hooks:

| Event | Hook | Action |
|-------|------|--------|
| Trade window opens | `PostSetup("Trade")` | Initializes buffer, sets trade active. |
| Trade updates | `PostRequestedUpdate("Trade")` | Tracks Gil amounts via regex. |
| Trade window closes | `PreFinalize("Trade")` | Commits buffer to player bank. |

The flow:

1. `OnTradeOpened()` resets the internal Gil buffer and marks the trade as active.
2. Chat messages during the trade are matched against Gil-related regex patterns (`TradeGilIn`, `TradeGilOut`).
3. `OnTradeClosed()` applies the accumulated buffer to the matched player's bank via `CommitTrade()`.

Trades targeting a Dropbox payout recipient are excluded from bank updates.

## Command Chain System

### Command Groups

Command groups are ordered sequences of `PluginCommand` entries. Each entry consists of a chat command string and a delay (in seconds). The executor processes entries sequentially, replacing all placeholders before sending.

| Group | Trigger |
|-------|---------|
| `Initial` | Dealing the starting hand to a player. |
| `Hit` | Player requests a hit. |
| `Stand` | Player stands. |
| `DD` | Player doubles down. |
| `Split` | Player splits a pair. |
| `PlayerBJ` | Player has a natural Blackjack (2 cards = 21). |
| `PlayerDirtyBJ` | Player reaches 21 with more than 2 cards. |
| `PlayerBust` | Player busts (exceeds 21). |
| `PlayerDDForcedStand` | Hand locks after Double Down card. |
| `DealStart` | Dealer reveals opening card for a new round. |
| `DealHit` | Dealer draws an additional card. |
| `DealStand` | Dealer stands. |
| `DealerBJ` | Dealer has Blackjack. |
| `DealerBust` | Dealer busts. |
| `ResultSmall` | Combined result summary. |
| `ResultPlayerWin` | Individual win announcement. |
| `ResultPlayerPush` | Individual push announcement. |
| `ResultPlayerBusted` | Individual bust announcement. |
| `ResultPlayerLost` | Individual loss announcement. |
| `StateHSDS` | Prompt: Hit, Stand, DD, or Split available. |
| `StateHSD` | Prompt: Hit, Stand, or DD available. |
| `StateHS` | Prompt: Hit or Stand available. |
| `HandStateHSDS` | Prompt for split hand: Hit, Stand, DD, or Split. |
| `HandStateHSD` | Prompt for split hand: Hit, Stand, or DD. |
| `HandStateHS` | Prompt for split hand: Hit or Stand. |
| `BankTell` | Bank and bet display. |

**Dice Wait System:** When a command contains `/dice party 13`, the executor pauses (up to 30 seconds) and waits for the regex engine to capture the dice result before continuing.

**Speed Multiplier:** All delays are scaled by `CommandSpeedMultiplier` (range: 0.1x to 4.0x). A minimum effective delay of 0.3 seconds is enforced.

### Placeholders and Tokens

These tokens are replaced at runtime before a command is sent to chat:

| Token | Replaced With |
|-------|---------------|
| `<t>` | Player alias (if set) or character name. |
| `<points>` | Current hand score, e.g. `15` or `11/21` for soft hands. |
| `<cards>` | Card list, e.g. `Spades A, Hearts 5 and Clubs K`. |
| `<dealer>` | Local player name (the dealer). |
| `<handnumber>` | Current hand index (1-based, relevant for splits). |
| `<totalhands>` | Total number of hands for the current player. |
| `<results>` | Formatted round results (winners, pushes, losses, busts). |
| `+{PlayerScore}` | Numeric score only (no formatting). |
| `#{BatchName}` | Draws a line from the named Message Batch. |
| `${variableName}` | Resolves a session variable from the Variables system. |

### Message Batches

Message Batches are named pools of strings. When referenced via `#{BatchName}`, the executor selects a line based on the configured selection mode:

| Mode | Behavior |
|------|----------|
| `Random` | Picks a random entry from the pool. |
| `First` | Always returns the first entry. |
| `Iterative` | Cycles through entries in order, wrapping around. |

Each message string can contain the same placeholders listed above. Messages in the default configuration are wrapped with the FFXIV private-use character U+E070 for visual formatting.

## Regex Engine

The regex engine (`RegexEngine`) iterates over all enabled `UserRegexEntry` patterns against each incoming chat message. It uses compiled regex instances with per-pattern caching for performance.

### Default Patterns

| Name | Pattern (EN) | Action |
|------|-------------|--------|
| Trade: Inbound | `^(.+) wishes to trade with you\.$` | `TradePartner` |
| Trade: Outbound | `^Trade request sent to (.+)$` | `TradePartner` |
| Trade: Gil In | `^You receive ([\d.]+) gil\.$` | `TradeGilIn` |
| Trade: Gil Out | `^You hand over ([\d.]+) gil\.$` | `TradeGilOut` |
| Trade: Success | `^Trade complete\.$` | `TradeCommit` |
| Trade: Cancel | `^Trade canceled\.$` | `TradeCancel` |
| Dice: Blackjack Logic | `Random! .*?\) (\d+)\s*$` | `DiceRollValue` |

German equivalents are included for all trade and dice patterns.

### Supported Actions

Regex matches can trigger one of the following action categories:

| Category | Actions |
|----------|---------|
| Game Actions | `WantHit`, `WantStand`, `WantDD`, `WantSplit`, `BankOut` |
| Dice | `DiceRollValue` |
| Trade | `TradePartner`, `TradeGilIn`, `TradeGilOut`, `TradeCommit`, `TradeCancel` |
| UI Highlights | `HighlightBet`, `HighlightPayout`, `HighlightAlias`, `HighlightHit`, `HighlightStand`, `HighlightDD`, `HighlightSplit`, `HighlightPause`, `HighlightLeave`, `HighlightJoin` |
| Other | `BetInformationChange`, `TakeBatch` |

**Entry Modes:**
- `Trigger` - Executes the associated action on match.
- `SetVariable` - Stores the matched text into a named session variable.

## Configuration

Configuration is persisted via the Dalamud plugin configuration system (`IPluginConfiguration`). All settings are accessible through the Settings tab in the UI.

### Game Rules

| Setting | Default | Description |
|---------|---------|-------------|
| `FirstDealThenPlay` | - | Deal all players first, then start turns (vs. deal-and-play per player). |
| `IdenticalSplitOnly` | - | Require identical card values for splitting (vs. same score). |
| `AllowDoubleDownAfterSplit` | - | Allow Double Down on split hands. |
| `MaxHandsPerPlayer` | 2 | Maximum number of split hands per player (range: 2-10). |
| `RefundFullDoubleDownOnPush` | - | Return the full DD bet on push instead of the base bet. |

### Payout Multipliers

| Setting | Default | Description |
|---------|---------|-------------|
| `MultiplierNormalWin` | 1.0x | Payout multiplier for standard wins. |
| `MultiplierBlackjackWin` | 1.5x | Payout multiplier for natural Blackjack (2 cards = 21). |
| `MultiplierDirtyBlackjackWin` | 1.0x | Payout multiplier for reaching 21 with 3+ cards. |

### Auto-Dealing

| Setting | Default | Description |
|---------|---------|-------------|
| `AutoInitialDeal` | - | Automatically deal cards when the phase is `InitialDeal`. |
| `AutoDealerDraw` | - | Automatically draw dealer cards during `DealerTurn`. |
| `DealerDrawsUntil` | 17 | Dealer stands on this value or higher (soft stand threshold). |

### Bet Limits

| Setting | Default |
|---------|---------|
| `MinBet` | 50,000 Gil |
| `MaxBet` | 500,000 Gil |

### User Levels

The plugin exposes different amounts of configuration depending on the selected user level:

| Level | Description |
|-------|-------------|
| `Beginner` | Basic gameplay controls only. |
| `Advanced` | Full rule customization, multipliers, and automation settings. |
| `Dev` | Debug features, speed mode, and diagnostic tools. |

## State Management

### Snapshots and Timeline

Every game action (deal, hit, split, etc.) pushes a `GameSnapshot` onto the game log. Each snapshot contains:

- Timestamp
- Reason string (e.g. `"Hit:PlayerName"`)
- Current `GamePhase`
- Deep copies of all player states and the dealer state
- Full deck shoe state

The Round Log tab provides a timeline view. Selecting a previous snapshot restores the full game state to that point, enabling undo/redo for mistake correction.

### Defaults Migration

The plugin uses a version-based migration system to update default messages, commands, and regex patterns without overwriting user customizations.

1. On first install, all defaults from `manager.defaults.cs` are seeded into the configuration and a `defaults.json` snapshot is created.
2. On version change, the system compares the new defaults against the snapshot. New entries are added to the user's configuration; existing entries remain untouched.
3. If versions match, no migration runs.

Users can manually reset to defaults via the UI at any time.

## Dropbox Integration

If the [Dropbox](https://github.com/NightmareXIV/Dropbox) plugin is installed, the payout system copies the Gil amount to the clipboard and executes `/dropbox` for a streamlined transfer.

If Dropbox is not available, the plugin falls back to a manual payout helper that splits large amounts into 1,000,000 Gil chunks and initiates sequential trades.

## UI Tabs

The main window is organized into the following tabs:

| Tab | Description |
|-----|-------------|
| Main | Player table, action buttons, phase indicator, party sync. |
| Regexes | Regex pattern editor with action assignment and case-sensitivity toggle. |
| Messages | Message Batch editor with selection mode configuration. |
| Commands | Command Group editor with per-entry delay and enable/disable controls. |
| Own Buttons | Custom quick-action button configuration. |
| Settings | Game rules, multipliers, speed, automation, and UI customization. |
| Variables | Session variable viewer and editor. |
| Round Log | Snapshot timeline with undo/redo navigation. |
| Stats | Income, expense, and round count summary. |
| Import/Export | JSON-based configuration import and export. |
| Debug | Speed mode toggle, debug log popout, phase display (Dev level only). |
| Thanks | Credits. |

## Technical Stack

| Component | Value |
|-----------|-------|
| SDK | Dalamud.NET.Sdk 14.0.1 |
| Framework | .NET 10.0 |
| Language | C# |
| UI | ImGui (via Dalamud) |
| Dependencies | ECommons 3.1.0.18 |
| Chat Dispatch | ECommons.Automation.Chat |

## License

This project is licensed under the [Apache License 2.0](LICENSE).

## Repository

REPO: [https://puni.sh/api/repository/vali](https://puni.sh/api/repository/vali)
