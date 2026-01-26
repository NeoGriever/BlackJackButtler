# BlackJack Buttler (BJB)

BlackJack Buttler is a comprehensive assistant plugin for FFXIV (Dalamud) designed for players hosting Blackjack games within the game's roleplaying community. It automates card tracking, point calculation, bankroll management via trade detection, and dealer communication through customizable command chains.

## Key Features

- Automated Tracking: Automatically detects /dice 13 rolls and converts them into card values (Ace through King) for players and the dealer.
- Bankroll Management: The integrated TradeManager parses incoming and outgoing trades to automatically track player balances.
- Customizable Command Chains: Define sequences of chat messages and emotes with specific delays to automate dealer interactions.
- Advanced Game Logic: Supports standard Blackjack rules including Hits, Stands, Double Downs, and Splits (up to 10 hands per player).
- Timeline Control: A snapshot-based history system allows the dealer to rewind the game state to any previous action to correct mistakes.
- Payout Helper: Integrated support for the Dropbox plugin and a manual payout helper to facilitate Gil transfers exceeding the 1,000,000 limit.
- Regex Engine: Highly flexible message detection to recognize player intents (like "hit" or "stay") in multiple languages.

## Commands

- /bjb: Opens the BlackJack Buttler main window and configuration interface.

## Usage Guide

1. Group Setup: Use the Group Detector in the Main tab to automatically synchronize your party members into the player list.
2. Handling Bets: When a player trades Gil to you, the plugin detects the amount and adds it to their bankroll. Set the current bet for each player in the main table.
3. Starting a Round: Click Start New Round. This triggers the DealStart command chain and sets the phase to Initial Deal.
4. Player Turns: As players roll or type commands, the plugin updates their hands. You can use the automated command buttons (Hit, Stand, etc.) to trigger your prepared dealer responses.
5. Payouts: Once the dealer hand is finished, the plugin evaluates winners and losers based on your configured multipliers. Use the Pay Out button to settle balances.

## Placeholders for Messages

Use these tokens in your Command Chains or Message Batches to create dynamic responses:

- <t>: The name or alias of the targeted player.
- <points>: The current point total of the active hand (e.g., "15" or "11/21").
- <cards>: A string representation of the cards in hand (e.g., "Spades A, Hearts 5").
- <results>: A summarized list of winners, losers, and busts for the final result message.
- ${variable}: Custom session variables defined in the Variables tab.
- #{BatchName}: Pulls a random or iterative line from a specific Message Batch.

## Command Groups

The plugin uses internal triggers to execute specific command sequences:
- Initial: Triggered when dealing the starting hand to a player.
- Hit / Stand / DD / Split: Triggered during player actions.
- PlayerBJ / PlayerDirtyBJ: Triggered when a player reaches 21.
- DealStart / DealHit / DealStand: Dealer-specific actions.
- ResultSmall / ResultPlayerWin / etc.: Triggered during the payout phase.

## Technical Details

- Deck Simulation: Uses a simulated shoe containing 12 decks to provide realistic card distribution.
- State Persistence: Game states are saved during the session, allowing for full recovery after crashes or accidental jumps in the timeline.
- Dependencies: Requires ECommons.

## License

This project is licensed under the AGPL-3.0-or-later License.
