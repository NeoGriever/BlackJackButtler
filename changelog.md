# BlackJack Buttler – Version 1.9.0.0

## Version 1.9.0.0 and configuration

- Added the third view variant with its own Version 3 Settings layout while keeping the Classic and Version 2 views available.
- Version 3 reorganizes Settings into dedicated General, Automation, Rules, Betting, Time & Delay, Nearby Players, Visual, Alliance, Preset Setup, and System tabs.
- General navigation now uses the labels **Side**, **Burger**, and **Tabs**; **Top Tabs** no longer carries an experimental marker.
- New structured configuration values are migrated safely from existing data. New data takes priority after migration while legacy values remain compatible with imports, exports, and presets.
- The version is now **1.9.0.0** in the plugin metadata and in this in-game changelog.

## Automation and payouts

- Automation separates **Message Reaction**, **Dealer Draw**, **Player Draw**, and **Continue after**. The continuation delay accepts 10–300 seconds even when the feature is off.
- Message Reaction gates only manually configured gameplay regex actions; system, trade, and variable regex processing remains available independently.
- **Insert regular Regex entries** adds the standard Draw, Stand, Double Down, Split, Ready, Set Bet, Bank Tell, and Withdraw groups without duplicating existing entries.
- Command-after-bet-change and insufficient-bank command selection moved to Automation. Command Speed remains a multiplier, and Recall Unlock remains configurable with reset controls.
- **Auto Activate Trading Players** is enabled by default in all views. Members who join after the initial group snapshot are activated once upon their first detected trade; manual removal opts them out until they leave and join again.
- Added the **Withdraw** regex action. It supports numeric amounts, `k`/`m`, `all`, and `everything`; validates the bank balance; and pays exactly the requested amount across normal payout chunks.

## Regexes, messages, and commands

- Regexes are split into **Standard Regex Entries** and **Custom Regex Entries** tabs.
- Messages are split into **Standard Message Batches** and **Custom Message Batches** tabs.
- Message batches support a multiline editor with Save and Back confirmation. Empty lines are ignored.
- Multiline Anti-Double notation is explicit: `[AD]Message` enables Anti-Double, while `\[AD]Message` writes a literal `[AD]Message`.
- Message Settings was removed. Anti-Double and second-snapping behavior are now always active while older configuration files remain readable.

## Own buttons

- Own-button groups, breaks, and their order now share one integrated, filterable list.
- Entries can be moved, deleted with CTRL, shown or hidden, and edited from the expandable list. Breaks are non-expandable entries that affect the button-bar line break only when visible.
- Group names and optional labels save immediately without closing or rebuilding an open editor.
- Hidden or inactive entries are visually distinct, and legacy order data and breaks are migrated safely.

## Rules and betting

- Rules use consistent On/Off controls. **First Deal, then play**, **Player self-rolling**, **Hide card suits**, and **Soft** use direct toggle buttons.
- Rules terminology was clarified, including **Short Result Messages**, the Blackjack tie-rule labels, and the Version 3 dealer row order.
- Result preview now demonstrates four winner, push, loss, and bust combinations separated by `---`.
- Betting uses explicit **Minimum**, **Normal**, and **VIP** entry types. Minimum cannot be deleted, and legacy VIP entries with rank zero migrate to Normal.
- Betting presets can be saved with an automatic name, renamed, colored, deleted, and loaded by clicking the same preset twice within two seconds.

## Time, nearby players, and sound

- Time & Delay provides a large named city/UTC-offset selection, including half-hour and quarter-hour offsets. Legacy offsets migrate to the first matching entry; unmatched values use an editable Custom UTC value.
- A separate **Summer/Winter Time** switch applies the deliberate one-hour adjustment.
- Nearby Players uses **Enabled** and **Columns**. Range-circle visibility and the nearby custom command moved to Range Settings, while automatic dequeue remains disabled.
- Nearby sound entries show their full path, enabled state, individual volume, and removal control. Playback multiplies global and per-file volume, and Test reports missing or unplayable files.
- Sound cooldown supports 0.02–30.00 seconds and a double-click numeric editor with OK.

## Statistics and main view

- Tip shortcuts are ordered from **1k** through **1m**, and the displayed tip total supports double-click editing with OK.
- Non-fixed wages support Gil per minute, 15 minutes, 30 minutes, hour, or two hours; the selected interval is persisted and used for the calculation.
- Gil displays are clickable, right-aligned formatted labels that switch to a selected plain-integer editor and return to the configured display after Enter.
- Rotation is configured from the main header with an editable current slider and a read-only saved-value slider.
- Disabling Nearby Players hides the nearby popout, configuration, and sticky-order controls. The **NEARBY PLAYERS** title toggles the list for the current session.

## Minor fixes

- Different minor optical and logical fixes.
