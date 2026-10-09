# ModularTrade

`ModularTrade` is a GUI-free, referenceable .NET library for a single FFXIV Gil-transfer session. It is derived from the operational parts of Dropbox, but intentionally does not use Dropbox's UI, task queue, or its unsafe "trade window closed means success" rule.

## Host integration

The host supplies an `ITradeGameAdapter` and calls `Update()` from its Dalamud framework update handler:

```csharp
TradeManager.Initialize(adapter);
TradeManager.onTradeChange += OnTradeChange;

// Once per framework update, on the framework thread:
TradeManager.Update();

TradeManager.Trade("Player Name@World", 2_350_001);
```

The compatibility callback uses these values:

- `2, []` — accepted and started
- `3, [step, totalSteps, confirmedGil, totalGil]` — a part-trade was verified
- `1, []` — every part-trade was verified
- `0, []` — stopped or failed; inspect `TradeManager.LastResult`

`TradePlanner.CreatePlan` splits an amount into no-more-than-1,000,000 Gil parts without integer overflow. Only one session can be active at a time.

## Safety contract

The library does not treat a closed trade window as proof of a payment. A part is recorded only when the adapter supplies a success outcome that belongs to the active session. Once the trade button was clicked, a timeout, user cancellation, or ambiguous outcome is reported as `UnknownOutcome`; the amount is never automatically retried.

`RemainingAmount` following `UnknownOutcome` means *not yet verified*, not *safe to pay again*.

## BJB adapter

`BlackJackButtler/ModularTrade/BjbDalamudTradeAdapter.cs` is BJB's no-GUI implementation of the game adapter. It follows Dropbox's InputNumeric callback path and treats the localized post-trade system message as the authoritative outcome; a subsequent trade starts only after that success outcome. In Debug builds, **Payout executor → Normal** tries it before external Dropbox and the local fallback. Individual Debug choices force one executor for controlled testing. The production build keeps the existing payout path until that in-game validation is complete.

Other Dalamud plugins can reference `ModularTrade.csproj`, implement `ITradeGameAdapter` for their service lifecycle, and use the same `TradeManager` API without depending on BJB.
