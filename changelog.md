# BlackJack Buttler – Version 1.9.0.3

## Configuration Changes

- Replaced the BlackJack Tie Rule with the compact **BlackJack Priority** selector: `Push | Player | Dealer | Regular`.
- Reworked Betting entry types. The single Minimum row is fixed as Minimum; every other row uses `NRM | VIP`. Duplicate Minimum rows are repaired to Normal automatically.
- New Betting entries now follow the sequence Minimum, Normal, VIP 1, VIP 2, and so on. VIP numbering continues from the highest existing level.
- Consolidated General controls into segmented User Level, Main View, and Menu Style selectors. Main View now reads `Classic | Compacted | Modern`, and Dev is presented as Profi.
- Replaced Dealing Behavior action buttons with a `Dealing order: Individual | Party` selector and labeled On/Off switches for Player Self Rolling and Hide Card Suits.
- Converted Visual and System checkboxes, including Wait Range Expanded, to consistent On/Off switches.
- Removed the Alliance and Preset Setup settings tabs. Alliance Nearby J Command now lives in the Nearby Players CFG window.

## Fixes

- The configured Nearby range now remains visible when the Nearby Players list is collapsed and no longer depends on the dealer / Group Detector being open.
- Rectangle range edges now render when their corner points are outside the visible screen area.
- Fixed the shared command selector ID collision that prevented the adjacent **Command on insufficient Bank** dropdown from being operated.

## Quality-of-Life Changes

- Moved the Triple Down points-limit On/Off switch before its numeric input.
- Moved the Messages and Regex `Standard | Custom` tabs above their search bars. Standard-only reset/edit actions and Custom-only creation actions now stay in their respective tabs.
- Time-zone groups now keep their UTC header and choices on one row and provide horizontal scrolling.
- Nearby sound mode is now a joined three-way selector.
- Nearby Players now uses a `Party Nearby J Command` selector with `Party Invite` as its default and `None` to hide the Party J button. The former extra custom-command button was removed, while Area Shape, Fixed World Position, Auto Act, and related options use compact joined controls.
- Moved default Message Preset creation beside the Presets page actions. The button creates only missing Standard/Fast presets and disappears once both exist. Standard now snapshots the original defaults while Fast keeps the current setup; both enable the first five Assignment Rules.
- Renamed **Insert regular Regex entries** to **Create standard Automation Regex Entries**.
