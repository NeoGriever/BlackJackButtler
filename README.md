`v1.4.0.0`

# BlackJack Buttler

BlackJack Buttler (BJB) is a Dalamud plugin for FFXIV that turns you into a fully automated Blackjack dealer. It tracks cards via `/dice party 13` rolls, manages player bankrolls through trade detection, and sends customizable chat messages and emotes on your behalf. The plugin handles the entire flow from initial deal through payout, including splits, double downs, and multi-hand play.

Whether you run a casual table for friends or a high-stakes venue for dozens of rounds per night, BJB removes the mental overhead of tracking points, calculating payouts, and remembering whose turn it is. Three user levels (Beginner, Advanced, Dev) progressively reveal more configuration so you can start simple and grow into full customization.

## Table of Contents

- [1 Getting Started](#1-getting-started)
  - [1.1 Installation](#11-installation)
  - [1.2 Opening the Plugin](#12-opening-the-plugin)
  - [1.3 User Levels](#13-user-levels)
- [2 Main Game Page](#2-main-game-page)
  - [2.1 Group Detector](#21-group-detector)
  - [2.2 Auto Toggles](#22-auto-toggles)
  - [2.3 Dealer Section](#23-dealer-section)
  - [2.4 Player Table](#24-player-table)
  - [2.5 Game Phases](#25-game-phases)
- [3 Player Actions](#3-player-actions)
  - [3.1 Hit](#31-hit)
  - [3.2 Stand](#32-stand)
  - [3.3 Double Down](#33-double-down)
  - [3.4 Split](#34-split)
- [4 Betting and Bank](#4-betting-and-bank)
  - [4.1 Bet Limits](#41-bet-limits)
  - [4.2 Bank Input and Trade Detection](#42-bank-input-and-trade-detection)
- [5 Payouts](#5-payouts)
  - [5.1 Pay Out Button](#51-pay-out-button)
  - [5.2 Dropbox Integration](#52-dropbox-integration)
  - [5.3 Manual Trade (Payout Helper)](#53-manual-trade-payout-helper)
- [6 Round History](#6-round-history)
  - [6.1 Timeline and Snapshots](#61-timeline-and-snapshots)
  - [6.2 Rewinding](#62-rewinding)
- [7 Backup Viewer](#7-backup-viewer)
- [8 Statistics](#8-statistics)
  - [8.1 Session Stats](#81-session-stats)
  - [8.2 Overall Stats](#82-overall-stats)
- [9 Notepad](#9-notepad)
- [10 Settings (Beginner)](#10-settings-beginner)
  - [10.1 User Level](#101-user-level)
  - [10.2 Command Speed](#102-command-speed)
  - [10.3 Bet Limits](#103-bet-limits)
- [11 Settings (Advanced)](#11-settings-advanced)
  - [11.1 Gameplay Rules](#111-gameplay-rules)
  - [11.2 Max Hands per Player](#112-max-hands-per-player)
  - [11.3 UI Colors](#113-ui-colors)
  - [11.4 Multipliers](#114-multipliers)
  - [11.5 Defaults Reset](#115-defaults-reset)
- [12 Messages](#12-messages)
  - [12.1 Message Batches](#121-message-batches)
  - [12.2 Selection Modes](#122-selection-modes)
  - [12.3 Standard vs Custom Batches](#123-standard-vs-custom-batches)
  - [12.4 Default Batch List](#124-default-batch-list)
- [13 Commands](#13-commands)
  - [13.1 Command Groups and Steps](#131-command-groups-and-steps)
  - [13.2 Message References](#132-message-references)
  - [13.3 Context Tokens](#133-context-tokens)
  - [13.4 Processing Pipeline](#134-processing-pipeline)
  - [13.5 Built-in Command Groups](#135-built-in-command-groups)
- [14 Own Buttons](#14-own-buttons)
- [15 Regex](#15-regex)
  - [15.1 Regex Entries](#151-regex-entries)
  - [15.2 SetVariable Mode](#152-setvariable-mode)
  - [15.3 Trigger Actions](#153-trigger-actions)
  - [15.4 Default Patterns](#154-default-patterns)
- [16 Variables](#16-variables)
  - [16.1 Variable Syntax](#161-variable-syntax)
  - [16.2 Built-in Variables](#162-built-in-variables)
  - [16.3 Manual Variables](#163-manual-variables)
- [17 Debug](#17-debug)
  - [17.1 Debug Mode and Test Data](#171-debug-mode-and-test-data)
  - [17.2 Log Viewer](#172-log-viewer)
- [18 Macro Import](#18-macro-import)
  - [18.1 Import Process](#181-import-process)
  - [18.2 Wait Time Parsing](#182-wait-time-parsing)
- [Appendix](#appendix)
  - [A - Processing Pipeline Summary](#a---processing-pipeline-summary)
  - [B - Cross-Reference Index](#b---cross-reference-index)
  - [C - Technical Notes](#c---technical-notes)

---

## 1 Getting Started

### 1.1 Installation

Install through the Dalamud plugin installer. BJB requires **ECommons** as a dependency (installed automatically). Optional: install the **Dropbox** plugin for streamlined payouts.

### 1.2 Opening the Plugin

Use the chat command:

```
/bjb
```

This opens the main window. The sidebar on the left provides navigation to all pages.

### 1.3 User Levels

BJB uses a three-tier user level system that controls which sidebar pages and settings are visible.

| Level | Sidebar Pages | Settings Shown |
|---|---|---|
| **Beginner** | Main, Settings, Stats, Round History | User Level, Command Speed, Bet Limits, Natural BJ Multiplier |
| **Advanced** | + Messages, Commands, Own Buttons | + All Gameplay Rules, UI Colors, All Multipliers, Defaults Reset |
| **Dev** | + Regex, Variables, Debug, Macro Import | + Clipboard Export/Import |

Change the level in **Settings > User Level**. Lowering the level resets any advanced settings to their defaults.

---

## 2 Main Game Page

### 2.1 Group Detector

The **Group Detector** button syncs your FFXIV party list into the plugin. The party leader becomes the dealer; all other members become players. The detector polls every second while active.

When deactivated, the session is cleared and non-active players with zero bank are removed.

### 2.2 Auto Toggles

Three toggle buttons appear at the top of the main page:

| Toggle | Effect |
|---|---|
| **Auto Player Hand** | Automatically deals the initial hand to each player during the InitialDeal phase. |
| **Auto Dealer Draw** | Automatically draws cards for the dealer until 17, then stands. |
| **Auto Run** | Executes player action triggers (Hit/Stand/DD/Split) automatically when detected. Only appears when regex triggers for player actions exist (see [15 Regex](#15-regex)). When OFF, detected actions highlight the corresponding button instead. |

### 2.3 Dealer Section

A 4-column table showing the dealer's name, cards, points, and controls.

- **Waiting/Payout phase**: Shows the "Start New Round" button.
- **DealerTurn phase**: Shows "Hit" and "Stand" buttons (or "Auto..." if Auto Dealer Draw is on).

### 2.4 Player Table

A 9-column table for all players:

| Column | Description |
|---|---|
| **A** | Alias button. Opens a popup to set a short name used in `<t>` replacements. |
| **J** | Join/Leave toggle. `>` to add inactive player, `X` to deactivate. |
| **P** | Hold/Bench button. Toggles hold (skip next round), bench (pause mid-round), or return. |
| **Name** | Player display name (alias if set, otherwise character name). Yellow when it is their turn. |
| **Bank** | Gil balance. Editable when the Bank Input checkbox is enabled. Includes a "T" button for individual bank tell. |
| **Bet** | Current bet amount. Red `!` indicator appears when outside the configured min/max range. |
| **Cards** | Visual card display with suit colors (red for Hearts/Diamonds, white for Spades/Clubs). |
| **Points** | Calculated score. Shows "BJ" (green for natural, yellow for dirty), red strikethrough for busts, or min/max for soft hands. |
| **Controls** | Action buttons during PlayersTurn, "Pay Out" during Payout, or "Deal Hand" during InitialDeal. |

### 2.5 Game Phases

The game progresses through five phases. Each phase transition triggers the corresponding command groups (see [13.5 Built-in Command Groups](#135-built-in-command-groups)).

```
Waiting --> InitialDeal --> PlayersTurn --> DealerTurn --> Payout --> Waiting
```

| Phase | Description |
|---|---|
| **Waiting** | No active round. "Start New Round" is available. |
| **InitialDeal** | Dealer draws one card (triggers `DealStart`), then each player gets dealt (triggers `Initial`). |
| **PlayersTurn** | Each player acts in sequence. State prompt groups (`StateHSDS`, `StateHSD`, `StateHS`) announce available actions. |
| **DealerTurn** | Dealer draws until standing or busting (triggers `DealHit`, `DealStand`, `DealerBJ`, `DealerBust`). |
| **Payout** | Results evaluated, banks updated, result commands triggered. Returns to Waiting. |

---

## 3 Player Actions

All action buttons are disabled while a command chain is executing. Each action pushes a snapshot to the timeline before executing (see [6 Round History](#6-round-history)).

### 3.1 Hit

Draws one additional card for the current hand. Triggers the `Hit` command group (see [13.5 Built-in Command Groups](#135-built-in-command-groups)). If the hand busts, triggers `PlayerBust`. If the hand reaches exactly 21, triggers `PlayerDirtyBJ`.

### 3.2 Stand

Locks the current hand and advances to the next hand or next player. Triggers the `Stand` command group.

### 3.3 Double Down

Doubles the bet, draws exactly one card, then forces a stand. Triggers the `DD` command group. If the player's bank is insufficient, a payment popup appears requesting a trade.

Availability is controlled by the **Allow Double Down after Split** setting (see [11.1 Gameplay Rules](#111-gameplay-rules)). Only available when the hand has exactly 2 cards.

### 3.4 Split

Splits a two-card hand into two separate hands, each starting with one of the original cards. Requires an additional bet equal to the original. Triggers the `Split` command group, then `SplitDraw` for the new hand's second card.

Split availability is controlled by:
- **Identical Split Only** (see [11.1 Gameplay Rules](#111-gameplay-rules)): when on, only identical card values (e.g., J+J) can split; when off, same-score cards (e.g., J+K) can split.
- **Max Hands per Player**: limits total hand count (see [11.2 Max Hands per Player](#112-max-hands-per-player)).

---

## 4 Betting and Bank

### 4.1 Bet Limits

Each player's bet is validated against the configured minimum and maximum. A red `!` indicator appears next to out-of-range bets, and clicking it navigates to the corresponding setting.

### 4.2 Bank Input and Trade Detection

Enable the **Bank Input** checkbox (top-right of the player table) to allow manual editing of player banks.

Banks are also updated automatically through trade detection. When a player trades Gil to you, regex patterns detect the trade partner, amounts, and completion (see [15.4 Default Patterns](#154-default-patterns)). The trade amount is applied to the matching player's bank automatically.

---

## 5 Payouts

### 5.1 Pay Out Button

During the Payout phase, each player row shows a "Pay Out" button. Clicking it initiates a payout for that player's current bank balance.

### 5.2 Dropbox Integration

If the **Dropbox** plugin is installed and loaded, and the **Open Dropbox instead of trade** setting is enabled (see [11.1 Gameplay Rules](#111-gameplay-rules)), payouts use the Dropbox IPC to pre-fill Gil amounts and open the Dropbox window.

### 5.3 Manual Trade (Payout Helper)

When Dropbox is unavailable or disabled, the plugin opens a floating **Payout Helper** window that:

- Copies the remaining bank amount to clipboard (capped at 1,000,000 per trade).
- Initiates a `/trade <t>` command.
- Automatically re-opens trade if the bank has remaining balance.
- Shows a progress bar tracking payout completion.
- Auto-closes 4 seconds after the bank reaches zero.

---

## 6 Round History

### 6.1 Timeline and Snapshots

Every player action (hit, stand, DD, split, deal) pushes a snapshot of the entire game state to the timeline. Snapshots capture all player hands, banks, the dealer state, the deck shoe, and the current phase. Up to 100 snapshots are retained per session.

### 6.2 Rewinding

The **Round History** page displays snapshots grouped by participant. Each entry shows the timestamp, action reason, and a summary of the hand at that point.

Click `[` to jump backward to a snapshot, or `]` to jump forward. Rewinding restores the full table state (all players, dealer, deck) to that exact point. The timeline is locked once the dealer has drawn cards during the DealerTurn phase to prevent inconsistencies.

---

## 7 Backup Viewer

The **BackupViewer** is a standalone desktop application (Windows + Linux) for viewing and editing BlackJack Buttler plugin data outside of FFXIV. It auto-detects your plugin configuration path and provides read/write access to:

- **Configuration** — View and edit all plugin settings.
- **Session Data** — Browse saved game sessions and round history.
- **Logs and Statistics** — Read plugin logs and review accumulated stats.

No installation or .NET runtime required — the release zips are fully self-contained.

Download the latest release from [GitHub](https://github.com/NeoGriever/BlackJackButtler-BackupViewer/releases).

---

## 8 Statistics

### 8.1 Session Stats

Tracks rounds played, total income, total expense, and net profit/loss for the current session. Resets when the plugin is unloaded.

### 8.2 Overall Stats

Persisted across sessions. Shows the same metrics as session stats but accumulated over all time. Can be reset via the "Reset Stats" button (requires Ctrl+Shift, no active game, detector off, all banks at zero).

Both tabs include a "Copy" button that copies a formatted summary string to clipboard.

---

## 9 Notepad

A simple text area accessible from the main page via the sticky-note icon. Content is persisted across sessions. Useful for tracking house rules, player notes, or anything else.

---

## 10 Settings (Beginner)

These settings are visible at all user levels.

### 10.1 User Level

Switch between Beginner, Advanced, and Dev. Lowering the level resets affected settings to defaults.

### 10.2 Command Speed

A multiplier applied to all command step delays at execution time.

- `1.00x` = normal speed
- `0.50x` = twice as fast
- `2.00x` = twice as slow
- Minimum effective delay is always 0.3 seconds.

### 10.3 Bet Limits

Configure the minimum and maximum allowed bet. Out-of-range bets show a red indicator on the player table.

---

## 11 Settings (Advanced)

These settings require **Advanced** user level or higher.

### 11.1 Gameplay Rules

| Setting | Default | Description |
|---|---|---|
| First Deal, then Play | On | Deals hands to all players first, then starts turns. Off: deal and play per player. |
| Identical Split Only | On | Only identical cards can split (e.g., J+J). Off: same-score cards can split (e.g., J+K). |
| Allow Double Down after Split | Off | Permits DD on hands created by a split. |
| Refund DD on push | Off | On: full doubled bet is returned on push. Off: only original bet is returned. |
| Player BJ wins on tie | Off | On: player with 21 beats dealer with 21. Off: results in a push. |
| Autostart round only on multiple players | On | NextRound trigger only auto-starts with 2+ active player votes. With 1 player, highlights the button instead. |
| Open Dropbox instead of trade | On | Uses the Dropbox plugin for payouts when available. |
| Small Result Message | On | Sends one compressed result message instead of individual messages per player hand. |
| Dealer draws until | 17 | The point threshold at which Auto Dealer Draw stops hitting. |

### 11.2 Max Hands per Player

Controls the maximum number of hands a player can have through splits. Range: 2-10. Default: 2.

### 11.3 UI Colors

- **Highlight Color**: Background color for highlighted action buttons (default: yellow).
- **Highlight Text Color**: Text color on highlighted buttons (default: black).

### 11.4 Multipliers

| Multiplier | Default | Description |
|---|---|---|
| Normal Win | 1.00x | Payout multiplier for standard wins. |
| Natural BJ (2 cards) | 1.50x | Payout for a natural blackjack (Ace + 10-value in 2 cards). |
| Dirty BJ (3+ cards) | 1.00x | Payout for reaching 21 with 3+ cards. |

Payout formula: `bank += bet + (bet * multiplier)`.

### 11.5 Defaults Reset

**Reset Default Config File** (Ctrl+Shift to unlock): Resets the defaults snapshot file. This affects what "Reset to Default" uses as its baseline for messages, regex, and commands.

---

## 12 Messages

*Requires Advanced user level.*

### 12.1 Message Batches

A message batch is a named collection of text strings. When referenced from a command step, one message is selected from the batch according to its selection mode. This provides variety in dealer responses.

### 12.2 Selection Modes

| Mode | Behavior |
|---|---|
| **Random** | Picks a random message from the batch each time. |
| **First** | Always returns the first message. |
| **Iterative** | Cycles through messages in order, wrapping around. |

### 12.3 Standard vs Custom Batches

Standard batches ship with the plugin and can be hidden via the **Hide Standard Batches** setting. They can be reset to defaults independently. Custom batches are user-created and fully editable.

### 12.4 Default Batch List

The following batches are included by default:

| Batch Name | Used By | Purpose |
|---|---|---|
| Dividor | Multiple groups | Separator line (`=========================`). |
| Match Results | `ResultSmall` | Compressed result summary using `<results>`. |
| Player Deal Hand | `Initial` | Messages when dealing a player's starting hand. |
| Player State Messages HSDS | `StateHSDS` | Prompt when player can Hit, Stand, DD, or Split. |
| Player State Messages HSD | `StateHSD` | Prompt when player can Hit, Stand, or DD. |
| Player State Messages HS | `StateHS` | Prompt when player can Hit or Stand. |
| Player DD Forced Stand Messages | `PlayerDDForcedStand` | Announced when DD hand is locked. |
| Player Draw Messages | `Hit` | Messages when a player hits. |
| Player Stand Messages | `Stand` | Messages when a player stands. |
| Player DD Messages | `DD` | Messages when a player doubles down. |
| Player DD Messages Stand | `DD` | Confirmation after DD card is drawn. |
| Player Split Messages | `Split` | Messages when a player splits. |
| Player Split Draw Messages | `SplitDraw` | Messages when drawing for a split hand. |
| Player BlackJack Messages | `PlayerBJ` | Natural blackjack announcement (party chat). |
| Player BlackJack Messages Shout | `PlayerBJ` | Natural blackjack announcement (shout). |
| Player Dirty BlackJack Messages | `PlayerDirtyBJ` | Dirty blackjack announcement. |
| Player Busts Messages | `PlayerBust` | Bust announcement. |
| Dealer Draw Messages | `DealStart` | Round-start dealer draw announcement. |
| Dealer Hit Messages | `DealHit` | Dealer hit announcement. |
| Dealer Stands Messages | `DealStand` | Dealer stand announcement. |
| Dealer Blackjack Messages | `DealerBJ` | Dealer blackjack announcement. |
| Dealer Busts Messages | `DealerBust` | Dealer bust announcement. |
| Hand Reaction Messages | `Stand` | Commentary on the player's final hand. |
| Win Messages | `ResultPlayerWin` | Individual win announcement. |
| Push Messages | `ResultPlayerPush` | Individual push announcement. |
| Bust Messages | `ResultPlayerBusted` | Individual bust result. |
| Lost Messages | `ResultPlayerLost` | Individual loss announcement. |
| Payment Reminder | Payout flow | Tell sent when a player owes Gil for DD/Split. |
| Bank Tell Messages | `BankTell` | Bank/bet info posted to party chat. |

All default messages support context tokens like `<t>`, `<points>`, `<cards>`, and variable references like `${HandIndex}` and `${dealerpoints}` (see [13.3 Context Tokens](#133-context-tokens) and [16 Variables](#16-variables)).

---

## 13 Commands

*Requires Advanced user level.*

### 13.1 Command Groups and Steps

A command group is a named sequence of steps. Each step has:

- **Enabled**: Toggle to skip the step without deleting it.
- **Text**: The command or chat message to send (e.g., `/p Hello` or `/dice party 13`).
- **Delay**: Wait time after this step (0.5s - 8.0s), multiplied by the Command Speed setting.

Steps execute sequentially. If a step contains `/dice`, the executor pauses until a dice result is detected (30-second timeout).

### 13.2 Message References

Use `#{Batch Name}` in a command step to pull a message from a batch (see [12 Messages](#12-messages)).

```
/p #{Player Draw Messages}
```

This resolves to one message from the "Player Draw Messages" batch based on its selection mode. The pulled message is further processed through the full pipeline (context tokens, variable replacement).

### 13.3 Context Tokens

These tokens are replaced with live game data during command execution:

| Token | Value |
|---|---|
| `<t>` | Target player's alias (if set) or name. |
| `<points>` | Current hand's point total (e.g., `15` or `11/21` for soft hands). |
| `<cards>` | Card string (e.g., `Spades A, Hearts 5 and Clubs K`). |
| `<winners>` | Formatted winner list from round results. |
| `<pushed>` | Formatted push list. |
| `<loosers>` | Formatted loss list. |
| `<busted>` | Formatted bust list. |
| `<results>` | Combined result string (all categories joined by ` \| `). |
| `+{PlayerScore}` | Best score of the current target player. |

Context tokens also support variable syntax (`${...}`) for session variables (see [16 Variables](#16-variables)).

### 13.4 Processing Pipeline

Every command step goes through four processing stages in this exact order:

1. **Context Tokens** - `<t>`, `<points>`, `<cards>`, `<winners>`, etc. are replaced.
2. **PlayerScore** - `+{PlayerScore}` is replaced with the target's best score.
3. **Message References** - `#{Batch Name}` is resolved (the pulled message also goes through steps 1, 2, and 4).
4. **Variable Replacement** - `$${...}` (consume-on-read) and `${...}` (persistent read) are replaced.

See [Appendix A](#a---processing-pipeline-summary) for a visual summary.

### 13.5 Built-in Command Groups

| Internal Name | Display Name | Trigger |
|---|---|---|
| `Initial` | Player Start | Player's initial hand is dealt. |
| `Hit` | Player Hit | Player chooses to hit. |
| `Stand` | Player Stand | Player chooses to stand. |
| `DD` | Player Double Down | Player chooses to double down. |
| `Split` | Player Split | Player chooses to split. |
| `SplitDraw` | *(internal)* | Drawing second card for a split hand. |
| `PlayerBJ` | Player has Natural Blackjack | Player gets 21 with 2 cards during initial deal. |
| `PlayerDirtyBJ` | Player has Dirty Blackjack | Player gets 21 with 3+ cards. |
| `PlayerBust` | Player Busted | Player's hand exceeds 21. |
| `PlayerDDForcedStand` | *(internal)* | DD hand auto-stands after card draw. |
| `DealStart` | Dealer Start | Dealer draws the opening card for a new round. |
| `DealHit` | Dealer Hit | Dealer draws another card. |
| `DealStand` | Dealer Stand | Dealer stops drawing. |
| `DealerBJ` | Dealer Has Blackjack | Dealer gets 21. |
| `DealerBust` | Dealer Busted | Dealer exceeds 21. |
| `ResultSmall` | *(internal)* | Compressed result message (when Small Result is on). |
| `ResultPlayerWin` | *(internal)* | Individual player win (when Small Result is off). |
| `ResultPlayerPush` | *(internal)* | Individual player push. |
| `ResultPlayerBusted` | *(internal)* | Individual player bust result. |
| `ResultPlayerLost` | *(internal)* | Individual player loss. |
| `StateHSDS` | *(internal)* | Prompt: Hit, Stand, DD, Split available. |
| `StateHSD` | *(internal)* | Prompt: Hit, Stand, DD available. |
| `StateHS` | *(internal)* | Prompt: Hit, Stand available. |
| `BankTell` | Bank Tell (Individual) | Bank/bet info tell for a player. |

---

## 14 Own Buttons

*Requires Advanced user level.* See also [13 Commands](#13-commands) for syntax.

Custom command groups that appear as buttons above the dealer row on the main page. They use the same step format and processing pipeline as built-in command groups.

- Click "Add Group" to create a new custom group with a unique name.
- Each group has the same step editor (enable, text, delay, reorder, delete).
- When clicked on the main page, the group executes against the current turn player (or dealer if no player is active).
- Delete a group by holding Ctrl and clicking "Delete Group".

---

## 15 Regex

*Requires Dev user level.*

### 15.1 Regex Entries

Each regex entry has:

- **Name**: Identifier for the entry.
- **Enabled**: Toggle on/off.
- **Patterns**: One or more regex patterns (matched against sanitized chat messages). FFXIV Private Use Area characters are stripped and multiple spaces collapsed before matching.
- **Case Sensitive**: Toggle for case-sensitive matching.
- **Mode**: `SetVariable` or `Trigger`.
- **Action**: The action to execute when a pattern matches (Trigger mode only).
- **Action Param**: Additional parameter for certain actions (e.g., batch name for `TakeBatch`).

Patterns are evaluated in order. The first matching pattern within an entry wins, and the first matching entry wins (no further entries are checked for that message).

### 15.2 SetVariable Mode

When mode is `SetVariable`, a match stores the sanitized chat message text as a session variable with the entry's name. This variable can then be used in command steps via `${name}` (see [16 Variables](#16-variables)).

### 15.3 Trigger Actions

When mode is `Trigger`, the matched pattern executes one of these actions:

| Action | Description | Cross-ref |
|---|---|---|
| `None` | No action. | |
| `BetInformationChange` | Highlights the player's bet field. | [4.1](#41-bet-limits) |
| `WantHit` | If Auto Run is on, executes hit. If off, highlights the Hit button. | [3.1](#31-hit) |
| `WantStand` | If Auto Run is on, executes stand. If off, highlights the Stand button. | [3.2](#32-stand) |
| `WantDD` | If Auto Run is on, executes DD. If off, highlights the DD button. | [3.3](#33-double-down) |
| `WantSplit` | If Auto Run is on, executes split. If off, highlights the Split button. | [3.4](#34-split) |
| `BankOut` | Highlights the Pay Out button for the player. | [5.1](#51-pay-out-button) |
| `TradePartner` | Sets the trade partner name (captured from regex group 1). | [4.2](#42-bank-input-and-trade-detection) |
| `TradeGilIn` | Adds incoming Gil amount (group 1) to the trade buffer. | [4.2](#42-bank-input-and-trade-detection) |
| `TradeGilOut` | Subtracts outgoing Gil amount (group 1) from the trade buffer. | [4.2](#42-bank-input-and-trade-detection) |
| `TradeCommit` | Applies the trade buffer to the matched player's bank. | [4.2](#42-bank-input-and-trade-detection) |
| `TradeCancel` | Resets the trade state. | [4.2](#42-bank-input-and-trade-detection) |
| `TakeBatch` | Sends a message from the batch named in ActionParam to party chat. | [12 Messages](#12-messages) |
| `DiceRollValue` | Parses the dice result (group 1), maps it to a card, applies it to the current target. | |
| `HighlightBet` | Highlights the player's bet field. | |
| `HighlightPayout` | Highlights the Pay Out button. | |
| `HighlightAlias` | Highlights the Alias button. | |
| `HighlightPause` | Highlights the Pause/Hold button. | |
| `HighlightLeave` | Highlights the Leave button. | |
| `HighlightJoin` | Highlights the Join button. | |
| `HighlightHit` | Highlights the Hit button (once-consistent: only if no action button is already highlighted). | |
| `HighlightStand` | Highlights the Stand button (once-consistent). | |
| `HighlightDD` | Highlights the DD button (once-consistent). | |
| `HighlightSplit` | Highlights the Split button (once-consistent). | |
| `NextRound` | Counts as a "vote" for a new round. When all active players have voted, auto-starts (if Auto Run and 2+ players) or highlights the Start New Round button. | |
| `BankTell` | If Auto Run is on, sends bank/bet info to party chat. If off, highlights the Tell button. | |

### 15.4 Default Patterns

These regex entries are pre-configured and support both English and German game clients:

| Name | Action | Pattern Summary |
|---|---|---|
| Trade: Inbound | `TradePartner` | Captures player name from incoming trade request. |
| Trade: Outbound | `TradePartner` | Captures player name from outgoing trade request. |
| Trade: Gil In | `TradeGilIn` | Captures Gil amount received. |
| Trade: Gil Out | `TradeGilOut` | Captures Gil amount handed over. |
| Trade: Success | `TradeCommit` | Detects trade completion. |
| Trade: Cancel | `TradeCancel` | Detects trade cancellation. |
| Dice: Blackjack Logic | `DiceRollValue` | Captures the rolled number from `/dice party` results. |

---

## 16 Variables

*Requires Dev user level.*

### 16.1 Variable Syntax

Two replacement syntaxes are available in command steps and messages:

| Syntax | Behavior |
|---|---|
| `${name}` | Replaced with the variable's value. Value persists after replacement. |
| `$${name}` | Replaced with the variable's value. **Value is cleared after replacement** (consume-on-read). |

Variables are processed as the final step of the [processing pipeline](#134-processing-pipeline). `$${...}` replacements are processed before `${...}` to allow one-time values to take priority.

### 16.2 Built-in Variables

These variables are set automatically by the game engine:

| Variable | Set By | Value |
|---|---|---|
| `bankamount` | Player turn start / bank tell | Formatted bank (e.g., `120,000 Gil`). |
| `betamount` | Player turn start / bank tell | Formatted bet (e.g., `50,000 Gil`). |
| `lastwin` | Payout evaluation | Formatted last round result. |
| `dealerpoints` | Dealer card draw | Dealer's current best score. |
| `HandIndex` | Command execution | `[Hand N] ` for split hands, empty string for single hands. |
| `playerCards` | Context token processing | Card string for the current hand. |
| `winners` | Payout evaluation | Formatted winner names (e.g., `Winner: Alice`). |
| `pushed` | Payout evaluation | Formatted push names. |
| `loosers` | Payout evaluation | Formatted loss names. |
| `busted` | Payout evaluation | Formatted bust names. |
| `results` | Payout evaluation | All categories joined (e.g., `Winner: Alice \| Lost: Bob`). |
| `missingGil` | Payment reminder | Amount owed for DD/Split. |
| `action` | Payment reminder | `Double Down` or `Split`. |

### 16.3 Manual Variables

On the Variables page, click "+ Add Manual Variable" to create custom variables. These can be referenced in command steps and messages via `${name}` or `$${name}`.

Each variable row shows:
- Editable name and value fields.
- Copy buttons for `${name}` and `$${name}` syntax.
- Delete button.

---

## 17 Debug

*Requires Dev user level.*

### 17.1 Debug Mode and Test Data

Enabling Debug Mode creates test player data (7 mock players with varying bank/bet values) and allows playing a full game without a real party. Dice commands are simulated internally.

**Fast Tests** mode reduces all command delays to 0.2 seconds.

### 17.2 Log Viewer

The debug page shows a reverse-chronological log of all internal events:

- Command execution steps and processed text.
- Regex matches and trade events.
- Targeting changes and engine state transitions.
- Chat messages (flagged separately).

Controls:
- **Verbose** toggle: Show all log entries vs. chat-only.
- **Popout Log**: Opens the log in a separate window.
- **Copy All**: Copies all visible entries to clipboard with a header.
- **Clear Log**: Empties the log buffer (max 15,000 entries).
- **Run /xllog**: Opens the Dalamud log.

---

## 18 Macro Import

*Requires Dev user level.* Imports FFXIV macros into command groups (see [13 Commands](#13-commands)).

### 18.1 Import Process

1. The page lists all non-empty macros from both Character (C) and Global (G) macro sets.
2. For each macro, select a target command group from the dropdown (built-in or custom).
3. A preview table shows how the macro will be parsed into command steps.
4. Click "Try to import" to replace the target group's steps with the parsed macro content.

Clicking a macro's tag button (e.g., `C00`) opens the in-game macro editor for that macro.

### 18.2 Wait Time Parsing

The importer recognizes two wait time formats:

| Format | Example | Result |
|---|---|---|
| Inline `<wait.N>` | `/p Hello <wait.2.5>` | Text: `/p Hello`, Delay: 2.5s |
| Standalone `/wait N` | `/wait 3` | No text (wait only), adds 3.0s delay to previous step |

Also recognized: `/pause N` and `/warte N` (German equivalent).

---

## Appendix

### A - Processing Pipeline Summary

```
Command Step Text
      |
      v
[1] Context Tokens        <t>, <points>, <cards>, <winners>, etc.
      |
      v
[2] PlayerScore            +{PlayerScore} --> best score
      |
      v
[3] Message References     #{Batch Name} --> pull message
      |                         |
      |                    [1] Context Tokens (on pulled message)
      |                    [2] PlayerScore
      |                    [4] Variable Replacement
      |                         |
      |   <--- inline result ---+
      |
      v
[4] Variable Replacement   $${...} (consume) then ${...} (persist)
      |
      v
   Final Text --> sent to chat
```

### B - Cross-Reference Index

| Section | References |
|---|---|
| [2.2 Auto Run](#22-auto-toggles) | [15 Regex](#15-regex) (requires regex triggers) |
| [2.5 Game Phases](#25-game-phases) | [13.5 Built-in Command Groups](#135-built-in-command-groups) (triggers command groups) |
| [3.1-3.4 Actions](#3-player-actions) | [13.5 Built-in Command Groups](#135-built-in-command-groups) (triggers the corresponding command group) |
| [3.3 Double Down](#33-double-down) | [11.1 Gameplay Rules](#111-gameplay-rules) (controlled by Allow DD after Split) |
| [3.4 Split](#34-split) | [11.1 Gameplay Rules](#111-gameplay-rules) (controlled by Identical Split Only, Max Hands) |
| [4.2 Bank Input](#42-bank-input-and-trade-detection) | [15.3 Trigger Actions](#153-trigger-actions) (auto-updates via Trade regex actions) |
| [5.2 Dropbox](#52-dropbox-integration) | [11.1 Gameplay Rules](#111-gameplay-rules) (Open Dropbox setting) |
| [12.4 Default Batches](#124-default-batch-list) | [13.2 Message References](#132-message-references) (consumed via `#{...}`) |
| [13.2 Message References](#132-message-references) | [12 Messages](#12-messages) (pulls from batch) |
| [13.3 Context Tokens](#133-context-tokens) | [16 Variables](#16-variables) (also supports `${...}`) |
| [13.4 Processing Pipeline](#134-processing-pipeline) | [12 Messages](#12-messages) + [16 Variables](#16-variables) (full processing order) |
| [14 Own Buttons](#14-own-buttons) | [13 Commands](#13-commands) (same syntax and pipeline) |
| [15.2 SetVariable](#152-setvariable-mode) | [16 Variables](#16-variables) (creates variables) |
| [15.3 TakeBatch](#153-trigger-actions) | [12 Messages](#12-messages) (executes batch) |
| [15.3 Trade actions](#153-trigger-actions) | [4 Betting and Bank](#4-betting-and-bank) (updates banks) |
| [15.3 Auto actions](#153-trigger-actions) | [3 Player Actions](#3-player-actions) (executes game actions) |
| [16 Variables](#16-variables) | [13.4 Processing Pipeline](#134-processing-pipeline) (consumed during pipeline) |
| [18 Macro Import](#18-macro-import) | [13 Commands](#13-commands) (imports into groups) |

### C - Technical Notes

- **Deck**: 12-deck shoe (624 cards). Cards are pulled by value and removed from the shoe. When a requested value is exhausted, the shoe reshuffles.
- **Persistence**: Game state is saved after every action (players, dealer, phase, deck). On next launch, a "Restore Previous Session" button appears if a saved session exists. The session file is cleared when the Group Detector is deactivated.
- **Activity Log**: Player joins, leaves, bank changes, bet changes, and round results are logged internally for debugging purposes.
- **Dependencies**: Requires ECommons. Optional: Dropbox plugin for streamlined payouts.

## Commands

- `/bjb` - Opens the BlackJack Buttler main window.

## License

This project is licensed under the AGPL-3.0-or-later License.
