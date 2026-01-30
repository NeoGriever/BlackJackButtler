using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    private static PlayerState? _pendingSplitPlayer = null;
    private static Configuration? _pendingSplitConfig = null;
    private static List<PlayerState>? _pendingSplitPlayers = null;
    private static PlayerState? _pendingDDPlayer = null;

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

            if (dealer.Hands.Count > 0 && dealer.Hands[0].Cards.Count > 0)
            {
                var (min, max) = dealer.CalculatePoints(0);
                int dealerScore = (max.HasValue && max.Value <= 21) ? max.Value : min;
                VariableManager.SetVariable("dealerpoints", dealerScore.ToString());
            }
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
        var activePlayers = GetActivePlayers(players);
        var benchPlayers = GetBenchPlayers(players);

        foreach (var pl in activePlayers)
            pl.ResetHighlightsOnceConsistent();

        if (activePlayers.Count == 0 && benchPlayers.Count == 0)
        {
            CurrentPhase = GamePhase.Waiting;
            return;
        }

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

                foreach (var pl in activePlayers) pl.IsCurrentTurn = false;
                activePlayers[0].IsCurrentTurn = true;

                if (IsPlayerFinished(activePlayers[0])) {
                    NextTurn(players, cfg);
                } else {
                    SwitchTurnTo(activePlayers[0], activePlayers, cfg);
                }
            }
            else
            {
                CurrentPhase = GamePhase.PlayersTurn;
                if (current != null && IsPlayerFinished(current)) NextTurn(players, cfg);
            }
            return;
        }

        if (current != null)
        {
            current.CurrentHandIndex++;
            if (current.CurrentHandIndex < current.Hands.Count)
            {
                if (current.Hands[current.CurrentHandIndex].IsStand || current.Hands[current.CurrentHandIndex].IsBust)
                {
                    NextTurn(players, cfg);
                }
                return;
            }

            current.CurrentHandIndex = 0;
            current.IsCurrentTurn = false;
        }

        int currentIndex = current != null ? activePlayers.IndexOf(current) : -1;
        if (currentIndex + 1 < activePlayers.Count)
        {
            var next = activePlayers[currentIndex + 1];
            next.IsCurrentTurn = true;

            if (IsPlayerFinished(next))
            {
                NextTurn(players, cfg);
            }
            else
            {
                SwitchTurnTo(next, activePlayers, cfg);
            }
        }
        else
        {
            if (benchPlayers.Count > 0)
            {
                ActivateAllBenchPlayers(players);

                var firstFromBench = GetActivePlayers(players).FirstOrDefault(p => p.WasOnHoldThisRound);
                if (firstFromBench != null)
                {
                    Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] Starting with bench player: {firstFromBench.DisplayName}", false);
                    firstFromBench.IsCurrentTurn = true;

                    if (IsPlayerFinished(firstFromBench))
                    {
                        NextTurn(players, cfg);
                    }
                    else
                    {
                        SwitchTurnTo(firstFromBench, GetActivePlayers(players), cfg);
                    }
                    return;
                }
            }

            var allActivePlayers = GetActivePlayers(players);
            var anyPlayerAlive = allActivePlayers.Any(p => p.Hands.Any(h => !h.IsBust));

            if (!anyPlayerAlive)
            {
                Plugin.Instance.GetMainWindow().AddDebugLog("[Engine] All players busted. Skipping Dealer turn.");
                CurrentPhase = GamePhase.Payout;
                Task.Run(async () => await EvaluateFinalResults(players, _ctxDealer!, cfg));
            }
            else
            {
                CurrentPhase = GamePhase.DealerTurn;
                if (_ctxDealer != null)
                {
                    TargetPlayer(_ctxDealer.Name);

                    if (_ctxDealer.Hands.Count > 0)
                    {
                        var (min, max) = _ctxDealer.CalculatePoints(0);
                        int dealerScore = (max.HasValue && max.Value <= 21) ? max.Value : min;
                        VariableManager.SetVariable("dealerpoints", dealerScore.ToString());
                    }
                }
            }
        }
        SaveSessionIfNeeded(players);
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
        SaveSessionIfNeeded(players);
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
        SaveSessionIfNeeded(players);
    }

    public static async Task ActionDD(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if(p.Bank < p.CurrentBet)
        {
            long missingAmount = p.CurrentBet - p.Bank;
            Plugin.Instance.GetMainWindow().OpenDDMoneyPopup(p, missingAmount);
            _pendingDDPlayer = p;
            return;
        }

        await ExecuteActualDD(p, cfg, players);
    }

    public static async void ContinueDDAfterPayment(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p == null)
        {
            Plugin.Log.Error("[BJB] ContinueDDAfterPayment was called with a null player!");
            return;
        }

        try
        {
            if (p.Bank < p.CurrentBet)
            {
                Plugin.Log.Warning($"[DD] Still not enough money for {p.Name}");
                return;
            }

            await ExecuteActualDD(p, cfg, players);

            _pendingDDPlayer = null;
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[BJB] Error in ContinueDDAfterPayment: {ex}");
        }
    }


    private static async Task ExecuteActualDD(PlayerState p, Configuration cfg, List<PlayerState> players)
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
        SaveSessionIfNeeded(players);
    }

    public static async Task ActionSplit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.Hands.Count >= cfg.MaxHandsPerPlayer) return;

        Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"Split:{p.Name}");

        if (p.Bank < p.CurrentBet)
        {
            long missingAmount = p.CurrentBet - p.Bank;

            Plugin.Instance.GetMainWindow().OpenSplitMoneyPopup(p, missingAmount);

            _pendingSplitPlayer = p;
            _pendingSplitConfig = cfg;
            _pendingSplitPlayers = players;

            return;
        }

        await ExecuteSplit(p, cfg, players);
    }

    public static async void ContinueSplitAfterPayment(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p == null)
        {
            Plugin.Log.Error("[BJB] ContinueSplitAfterPayment: Player object is null!");
            return;
        }

        try
        {
            if (p.Bank < p.CurrentBet)
            {
                Plugin.Log.Warning($"[Split] Still not enough money for {p.Name} after payment check. (Bank: {p.Bank}, Needed: {p.CurrentBet})");
                return;
            }

            await ExecuteSplit(p, cfg, players);

            _pendingSplitPlayer = null;
            _pendingSplitConfig = null;
            _pendingSplitPlayers = null;

            Plugin.Log.Debug($"[Split] Continuation successful for {p.Name}");
        }
        catch (Exception ex)
        {
            Plugin.Log.Error($"[BJB] CRITICAL ERROR in ContinueSplitAfterPayment: {ex}");
        }
    }

    private static async Task ExecuteSplit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
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

        SaveSessionIfNeeded(players);

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Split] {p.DisplayName} successfully split hand", false);
    }

    public static async Task DealerHit(Configuration cfg, List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        CurrentPhase = GamePhase.DealerTurn;

        TargetPlayer(dealer.Name);
        SetForcedRecipient(dealer.Name);
        try { await CommandExecutor.ExecuteGroup("DealHit", dealer.Name, cfg); }
        finally { ClearForcedRecipient(); }
        SaveSessionIfNeeded(players);
    }

    public static async Task DealerStand(Configuration cfg, List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        CurrentPhase = GamePhase.Payout;

        TargetPlayer(dealer.Name);
        SetForcedRecipient(dealer.Name);
        try { await CommandExecutor.ExecuteGroup("DealStand", dealer.Name, cfg); }
        finally { ClearForcedRecipient(); }
        SaveSessionIfNeeded(players);
    }

    public static string GetStatePromptGroup(PlayerState p, Configuration cfg)
    {
        if (p.Hands.Count == 0) return string.Empty;
        var hand = p.Hands[p.CurrentHandIndex];

        bool canSplit = false;
        if (hand.Cards.Count == 2 && p.Hands.Count < cfg.MaxHandsPerPlayer)
        {
            if (cfg.IdenticalSplitOnly)
                canSplit = hand.Cards[0].Value == hand.Cards[1].Value;
            else
                canSplit = PlayerState.GetCardScoreValue(hand.Cards[0].Value) == PlayerState.GetCardScoreValue(hand.Cards[1].Value);
        }

        bool isSplitHand = p.Hands.Count > 1;
        bool canDD = hand.Cards.Count == 2;
        if (isSplitHand && !cfg.AllowDoubleDownAfterSplit)
            canDD = false;

        if (canSplit) return "StateHSDS";
        if (canDD)    return "StateHSD";
        return "StateHS";
    }

    private static bool IsPlayerFinished(PlayerState p)
    {
        return p.Hands.Count > 0 && p.Hands.All(h => h.IsStand || h.IsBust || h.IsNaturalBlackJack);
    }

    private static List<PlayerState> GetActivePlayers(List<PlayerState> players)
    {
        return players.Where(p => p.IsActivePlayer && !p.IsOnHold && !p.IsOnBench).ToList();
    }

    private static List<PlayerState> GetBenchPlayers(List<PlayerState> players)
    {
        return players.Where(p => p.IsActivePlayer && p.IsOnBench).ToList();
    }

    public static bool CanMovePlayerToBench(PlayerState player, List<PlayerState> allPlayers)
    {
        if (player.WasOnHoldThisRound) return false;

        var activePlayers = GetActivePlayers(allPlayers);
        var benchPlayers = GetBenchPlayers(allPlayers);

        if (activePlayers.Count == 1 && benchPlayers.Count == 0)
            return false;

        return true;
    }

    public static void MovePlayerToBench(PlayerState player, List<PlayerState> allPlayers)
    {
        if (!CanMovePlayerToBench(player, allPlayers)) return;

        player.IsOnBench = true;
        player.WasOnHoldThisRound = true;
        player.IsCurrentTurn = false;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] {player.DisplayName} moved to bench.", false);
    }

    public static void MovePlayerFromBench(PlayerState player)
    {
        if (!player.IsOnBench) return;

        player.IsOnBench = false;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] {player.DisplayName} returned from bench.", false);
    }

    private static void ActivateAllBenchPlayers(List<PlayerState> players)
    {
        var benchPlayers = GetBenchPlayers(players);
        if (benchPlayers.Count == 0) return;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] Activating all {benchPlayers.Count} bench players.", false);

        foreach (var p in benchPlayers)
        {
            MovePlayerFromBench(p);
        }
    }

    private static void SaveSessionIfNeeded(List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        var mainWindow = Plugin.Instance.GetMainWindow();
        SessionManager.SaveSession(
            players,
            dealer,
            CurrentPhase,
            mainWindow.IsRecognitionActive
        );
    }

    private static List<PlayerState> GetPlayersFromContext()
    {
        lock (_ctxLock)
        {
            return _ctxPlayers ?? new List<PlayerState>();
        }
    }
}
