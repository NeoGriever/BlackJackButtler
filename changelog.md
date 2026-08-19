# BlackJack Buttler – Version 1.9.0.2

## Configuration Changes

- Announced the planned retirement of the Classic and Version 2 views in favor of Version 3. Users can now switch to Version 3 directly from the in-game notice.
- Restored Auto Continue's minimum-player setting as a shared `1+ | 2+ | 3+ | 4+` choice in Version 2 and Version 3. Existing 2-player settings migrate automatically.

## Fixes

- Fixed the formatted Betting amount editor: opening or submitting an entry now affects only that specific row instead of sharing the editor state with other entries.
- Fixed the Rules payout controls so Normal Win, Natural BlackJack, Dirty BlackJack, Charlie, Split, Double Down, and Triple Down each update their own multiplier.
- Fixed payout-on-push handling for Triple Down and completed its dedicated settings, import/export, preset, logging, and Draw Logic integration.

## Quality-of-Life Changes

- Added **Imaginary Players (Ghosts)**: Hold CTRL beside a real player's Hold control to add one linked `<FirstName> Ghost`. Ghosts have their own hand, bet, bank, and alias while the real player controls their messages and rolls; all tells and payouts remain routed to the real player. Thanks to **Dissendra Blackthorn@Zodiark** for the idea.
- Added **Ghost Bank Routing**: Regex BankTells now report a real player's bank followed by their imaginary player's bank, route both `/tell`s to the real player, expose `<n>` for the displayed account name, and enforce a 1.1-second queue pause after every tell. The new `transfer <amount|half|50%|min|max>` regex manages funds between both banks, and `withdraw all` / `withdraw everything` pays out both accounts in sequence.
- Added optional **Triple Down** support. It adds a 3× total stake, requires two consecutive cards once chosen, and can be limited by points or disabled after Split. It is disabled by default.
- Added the `TD` main action and the regular `td` / `triple down` message-reaction commands.
- Reorganized the Rules settings hierarchy for clearer BlackJack, Charlie, Split, Double Down, and Triple Down configuration.
- Number steppers with a configured default now reveal an integrated **RESET** action while holding CTRL. Reset mode blocks decrementing; controls without a default retain their normal behavior.
- Standardized the Payout input width to match the Cards field.
