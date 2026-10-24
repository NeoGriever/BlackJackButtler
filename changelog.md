# BlackJack Buttler – Version 2.0.0.1

- Refactoring: Entire code rework and refactoring. Structured code files with categorized classes and functions for better maintenance.
- Change: Removed legacy V1 and V2 Config/UI-Layout.
- Feature: Added a little dot next to bet- and bank-values. Drag&Drop allows to change the bet through a configurable pie-menu.
- Feature: Added pie-menu settings under Settings > System
- Fix: A logic error behind the 'Dealer stands at [ - | 17 | + ] [Soft]' setting caused the dealer to draw a card, even the 'Soft'-Button was active. This is now fixed.
- Fix: The layout of the SFX-Selections in Automation under Player chat sound effects got corrected.
- Fix: The rendering for 9 digits forced separator view ('***,***,***') got corrected.
- Fix: Stats -> The input field for the date/time input allows now europe date formats like 15.07.2026

---

# BlackJack Buttler – Version 1.9.1.2

## Trade automation

- Payouts now check through Dalamud whether Dropbox is installed and currently active. Active Dropbox is preferred automatically.
- Added the integrated BJB trade implementation as the automatic alternative when Dropbox is unavailable or inactive. Dropbox is no longer required for payouts.
- Corrected trade completion handling: the next partial trade begins only after the prior trade's success message was received in the system chat; cancellation and unknown outcomes stop the payout.
- Removed the obsolete local trade-automation fallback.
