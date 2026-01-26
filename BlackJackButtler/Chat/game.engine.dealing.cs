using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task StartInitialDeal(List<PlayerState> players, Configuration cfg)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null)
        {
            CurrentPhase = GamePhase.Waiting;
            return;
        }

        var activePlayers = players.Where(p => p.IsActivePlayer).ToList();
        if (activePlayers.Count == 0)
        {
            CurrentPhase = GamePhase.Waiting;
            return;
        }

        dealer.Hands.Clear();
        dealer.CurrentHandIndex = 0;

        foreach (var p in activePlayers)
        {
            p.ResetForNewRound();
            p.IsCurrentTurn = false;
            p.CurrentHandIndex = 0;
            p.HasInitialHandDealt = false;
        }

        CurrentPhase = GamePhase.InitialDeal;

        TargetPlayer(dealer.Name);
        SetForcedRecipient(dealer.Name);
        try
        {
            await CommandExecutor.ExecuteGroup("DealStart", dealer.Name, cfg);
        }
        finally
        {
            ClearForcedRecipient();
        }

        foreach (var p in activePlayers) p.IsCurrentTurn = false;
        var first = activePlayers[0];
        first.IsCurrentTurn = true;
        first.CurrentHandIndex = 0;
        TargetPlayer(first.Name);

        CurrentPhase = GamePhase.InitialDeal;
    }

    public static async Task ActionDealHand(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        await ExecutePlayerAction(p, "Initial", cfg, players, async () => {
            TargetPlayer(p.Name);
            SetForcedRecipient(p.Name);
            try { await CommandExecutor.ExecuteGroup("Initial", p.Name, cfg); }
            finally { ClearForcedRecipient(); }
            p.HasInitialHandDealt = true;
        });
    }

    public static void NextTurn(List<PlayerState> players, Configuration cfg)
    {
        var activePlayers = players.Where(p => p.IsActivePlayer).ToList();
        if (activePlayers.Count == 0) { CurrentPhase = GamePhase.Waiting; return; }

        var current = activePlayers.FirstOrDefault(p => p.IsCurrentTurn);

        if (CurrentPhase == GamePhase.InitialDeal)
        {
            if (cfg.FirstDealThenPlay)
            {
                var nextToDeal = activePlayers.FirstOrDefault(p => !p.HasInitialHandDealt);
                if (nextToDeal != null)
                {
                    SwitchTurnTo(nextToDeal, activePlayers, cfg);
                    return;
                }
                CurrentPhase = GamePhase.PlayersTurn;
                SwitchTurnTo(activePlayers[0], activePlayers, cfg);
            }
            else
            {
                CurrentPhase = GamePhase.PlayersTurn;
            }
            return;
        }

        if (current != null)
        {
            current.CurrentHandIndex++;
            if (current.CurrentHandIndex < current.Hands.Count) return;

            current.CurrentHandIndex = 0;
            current.IsCurrentTurn = false;
        }

        int currentIndex = current != null ? activePlayers.IndexOf(current) : -1;
        if (currentIndex + 1 < activePlayers.Count)
        {
            var next = activePlayers[currentIndex + 1];
            if (cfg.FirstDealThenPlay)
            {
                SwitchTurnTo(next, activePlayers, cfg);
            }
            else
            {
                CurrentPhase = GamePhase.InitialDeal;
                SwitchTurnTo(next, activePlayers, cfg);
            }
        }
        else
        {
            var anyPlayerAlive = activePlayers.Any(p => p.Hands.Any(h => !h.IsBust));

            if (!anyPlayerAlive)
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[Engine] All players busted. Skipping Dealer turn.");
                CurrentPhase = GamePhase.Payout;
                Task.Run(async () => await EvaluateFinalResults(players, _ctxDealer!, cfg));
            }
            else
            {
                CurrentPhase = GamePhase.DealerTurn;
                if (_ctxDealer != null) TargetPlayer(_ctxDealer.Name);
            }
        }

    }

    private static void SwitchTurnTo(PlayerState target, List<PlayerState> allActive, Configuration cfg)
    {
        foreach (var pl in allActive) pl.IsCurrentTurn = false;
        target.IsCurrentTurn = true;
        target.CurrentHandIndex = 0;
        TargetPlayer(target.Name);
        if (target.Hands.Count > 0 && target.Hands[target.CurrentHandIndex].Cards.Count >= 2)
        {
            string promptGroup = GetStatePromptGroup(target, cfg);
            Task.Run(async () => await CommandExecutor.ExecuteGroup(promptGroup, target.DisplayName, cfg));
        }
    }

    public static async Task ActionHit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        await ExecutePlayerAction(p, "Hit", cfg, players, async () => {
            TargetPlayer(p.Name);
            SetForcedRecipient(p.Name);
            try { await CommandExecutor.ExecuteGroup("Hit", p.Name, cfg); }
            finally { ClearForcedRecipient(); }
        });
    }

    public static async Task ActionStand(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.CurrentHandIndex >= 0 && p.CurrentHandIndex < p.Hands.Count)
            p.Hands[p.CurrentHandIndex].IsStand = true;

        TargetPlayer(p.Name);
        SetForcedRecipient(p.Name);
        try { await CommandExecutor.ExecuteGroup("Stand", p.Name, cfg); }
        finally { ClearForcedRecipient(); }

        NextTurn(players, cfg);
    }

    public static async Task ActionDD(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        await ExecutePlayerAction(p, "DD", cfg, players, async () => {
            var hand = p.Hands[p.CurrentHandIndex];
            hand.IsDoubleDown = true;
            hand.Bet *= 2;
            TargetPlayer(p.Name);
            SetForcedRecipient(p.Name);
            try { await CommandExecutor.ExecuteGroup("DD", p.Name, cfg); }
            finally { ClearForcedRecipient(); }
            hand.IsStand = true;
        });
    }

    public static async Task ActionSplit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.Hands.Count >= cfg.MaxHandsPerPlayer) return;

        Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"Split:{p.Name}");

        if (p.Bank < p.CurrentBet)
        {
            /*
            TODO: game.engine.dealing.cs - ActionSplit - Einbau eines blockierenden Popups für den Dealer, welches anzeigt, wieviel Gil fehlen. - Wenn dann währenddessen ein Trade vom aktuellen Spieler kommt und den Betrag ausgleicht (oder mehr), wird nicht abgebrochen und das Popup schließt sich selbstständig. Andernfalls kann der Dealer das Popup schließen und der Vorgang wird abgebrochen.
            */
            return;
        }
        p.Bank -= p.CurrentBet;

        var currentHand = p.Hands[p.CurrentHandIndex];
        if (currentHand.Cards.Count != 2) return;

        var cardToMove = currentHand.Cards[1];
        currentHand.Cards.RemoveAt(1);

        var newHand = new HandState(p.CurrentBet);
        newHand.Cards.Add(cardToMove);
        p.Hands.Add(newHand);

        TargetPlayer(p.Name);
        SetForcedRecipient(p.Name);
        try { await CommandExecutor.ExecuteGroup("Split", p.Name, cfg); }
        finally { ClearForcedRecipient(); }
    }

    public static async Task DealerHit(Configuration cfg)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        CurrentPhase = GamePhase.DealerTurn;

        TargetPlayer(dealer.Name);
        SetForcedRecipient(dealer.Name);
        try { await CommandExecutor.ExecuteGroup("DealHit", dealer.Name, cfg); }
        finally { ClearForcedRecipient(); }
    }

    public static async Task DealerStand(Configuration cfg)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        CurrentPhase = GamePhase.Payout;

        TargetPlayer(dealer.Name);
        SetForcedRecipient(dealer.Name);
        try { await CommandExecutor.ExecuteGroup("DealStand", dealer.Name, cfg); }
        finally { ClearForcedRecipient(); }
    }

    private static string GetStatePromptGroup(PlayerState p, Configuration cfg)
    {
        if (p.Hands.Count == 0) return string.Empty;
        var hand = p.Hands[p.CurrentHandIndex];

        bool canSplit = false;
        if (hand.Cards.Count == 2 && p.Hands.Count < cfg.MaxHandsPerPlayer)
        {
            if (cfg.IdenticalSplitOnly)
                canSplit = hand.Cards[0] == hand.Cards[1];
            else
                canSplit = PlayerState.GetCardScoreValue(hand.Cards[0]) == PlayerState.GetCardScoreValue(hand.Cards[1]); // Nach Score (z.B. J & K)
        }

        bool isSplitHand = p.Hands.Count > 1;
        bool canDD = hand.Cards.Count == 2;
        if (isSplitHand && !cfg.AllowDoubleDownAfterSplit)
            canDD = false;

        if (canSplit) return "StateHSDS";
        if (canDD)    return "StateHSD";
        return "StateHS";
    }
}
