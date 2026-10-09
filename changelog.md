# BlackJack Buttler – Version 1.9.1.2

## Trade automation

- Payouts now check through Dalamud whether Dropbox is installed and currently active. Active Dropbox is preferred automatically.
- Added the integrated BJB trade implementation as the automatic alternative when Dropbox is unavailable or inactive. Dropbox is no longer required for payouts.
- Corrected trade completion handling: the next partial trade begins only after the prior trade's success message was received in the system chat; cancellation and unknown outcomes stop the payout.
- Removed the obsolete local trade-automation fallback.
