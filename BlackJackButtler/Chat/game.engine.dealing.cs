using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
        Regex.RegexEngine.ClearNextRoundVotes();
        StatsLogManager.OnRoundStarted();

        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null)
        {
            CurrentPhase = GamePhase.Waiting;
            return;
        }

        var heldPlayers = players.Where(p => p.IsActivePlayer && p.IsOnHold).ToList();
        var activePlayers = players.Where(p => p.IsActivePlayer && !p.IsOnHold).ToList();
        if (activePlayers.Count == 0 && heldPlayers.Count == 0)
        {
            CurrentPhase = GamePhase.Waiting;
            return;
        }

        dealer.Hands.Clear();
        dealer.CurrentHandIndex = 0;
        dealer.IsCurrentTurn = true;

        foreach (var p in activePlayers)
        {
            p.ResetForNewRound();
            p.IsCurrentTurn = false;
            p.CurrentHandIndex = 0;
            p.HasInitialHandDealt = false;
            p.BankAtRoundStart = p.Bank;
        }

        var benchSeed = DateTime.UtcNow;
        foreach (var p in heldPlayers)
        {
            p.IsOnBench = true;
            p.BenchedAt = benchSeed;
            p.WasOnHoldThisRound = true;
            p.HasInitialHandDealt = false;
            p.IsCurrentTurn = false;
            p.Hands.Clear();
            p.Hands.Add(new HandState(p.CurrentBet));
            p.CurrentHandIndex = 0;
            p.LastRoundResult = 0;
            p.JoinedMidRound = false;
            p.BankAtRoundStart = p.Bank;
        }
        dealer.BankAtRoundStart = 0;

        if (activePlayers.Count == 0)
        {
            CurrentPhase = GamePhase.Waiting;
            return;
        }

        Interlocked.Exchange(ref _payoutGuard, 0);
        CurrentPhase = GamePhase.InitialDeal;
        ViewDirectionManager.ApplyViewDirection(cfg);

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
        SendCompanionTableUpdate(cfg, activePlayers);

        foreach (var p in activePlayers) p.IsCurrentTurn = false;
        dealer.IsCurrentTurn = false;
        var first = activePlayers[0];
        first.IsCurrentTurn = true;
        first.CurrentHandIndex = 0;
        TargetPlayer(first.Name);

        CurrentPhase = GamePhase.InitialDeal;
        SendCompanionTableUpdate(cfg, activePlayers);
    }

    public static bool HasPlayerUnableToCoverBet(IEnumerable<PlayerState> players)
    {
        return players.Any(IsPlayerUnableToCoverBet);
    }

    public static bool IsPlayerUnableToCoverBet(PlayerState p)
    {
        return p.IsActivePlayer && !p.IsOnHold && p.CurrentBet > 0 && p.Bank < p.CurrentBet;
    }

    public static async Task ActionDealHand(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        Plugin.Instance.GetMainWindow().AddDebugLog($"[ActionDealHand] Start: {p.DisplayName}");
        await ExecutePlayerAction(p, "Initial", cfg, players, async () => {
            TargetPlayer(p.Name);
            SetForcedRecipient(p.Name);
            try { await CommandExecutor.ExecuteGroup("Initial", p.Name, cfg); }
            finally { ClearForcedRecipient(); }
            p.HasInitialHandDealt = true;
        });
        Plugin.Instance.GetMainWindow().AddDebugLog($"[ActionDealHand] Complete: {p.DisplayName}, HasInitialHandDealt={p.HasInitialHandDealt}");
    }

    public static void NextTurn(List<PlayerState> players, Configuration cfg)
    {
        var window = Plugin.Instance.GetMainWindow();
        var activePlayers = GetActivePlayers(players);
        var benchPlayers = GetBenchPlayers(players);

        var currentDbg = activePlayers.FirstOrDefault(p => p.IsCurrentTurn);
        window.AddDebugLog($"[NextTurn] Entry: Phase={CurrentPhase}, ActiveCount={activePlayers.Count}, BenchCount={benchPlayers.Count}, Current={currentDbg?.DisplayName ?? "none"}");

        foreach (var pl in activePlayers)
            pl.ResetHighlightsOnceConsistent();

        if (activePlayers.Count == 0 && benchPlayers.Count == 0)
        {
            window.AddDebugLog("[NextTurn] No active or bench players, returning to Waiting");
            CurrentPhase = GamePhase.Waiting;
            return;
        }

        var current = activePlayers.FirstOrDefault(p => p.IsCurrentTurn);

        if (CurrentPhase == GamePhase.InitialDeal)
        {
            if (cfg.FirstDealThenPlay)
            {
                var nextToDeal = activePlayers.FirstOrDefault(p => !p.HasInitialHandDealt);
                window.AddDebugLog($"[NextTurn] InitialDeal/FirstDealThenPlay: nextToDeal={nextToDeal?.DisplayName ?? "none"}");
                if (nextToDeal != null)
                {
                    SwitchTurnTo(nextToDeal, activePlayers, cfg);
                    return;
                }

                window.AddDebugLog("[NextTurn] InitialDeal complete, switching to PlayersTurn");
                CurrentPhase = GamePhase.PlayersTurn;

                foreach (var pl in activePlayers) pl.IsCurrentTurn = false;
                activePlayers[0].IsCurrentTurn = true;

                if (IsPlayerFinished(activePlayers[0])) {
                    window.AddDebugLog($"[NextTurn] First player {activePlayers[0].DisplayName} already finished, recursing");
                    SendCompanionTableUpdate(cfg, activePlayers);
                    NextTurn(players, cfg);
                } else {
                    SwitchTurnTo(activePlayers[0], activePlayers, cfg);
                }
            }
            else
            {
                window.AddDebugLog($"[NextTurn] InitialDeal/DealAndPlay: current={current?.DisplayName ?? "none"}, hasDealt={current?.HasInitialHandDealt}");
                if (current != null && current.HasInitialHandDealt)
                {
                    CurrentPhase = GamePhase.PlayersTurn;
                    if (IsPlayerFinished(current))
                    {
                        window.AddDebugLog($"[NextTurn] Current player {current.DisplayName} already finished, recursing");
                        SendCompanionTableUpdate(cfg, activePlayers);
                        NextTurn(players, cfg);
                    }
                    else
                        SwitchTurnTo(current, activePlayers, cfg);
                }
            }
            return;
        }

        if (current != null)
        {
            current.CurrentHandIndex++;
            if (current.CurrentHandIndex < current.Hands.Count)
            {
                window.AddDebugLog($"[NextTurn] {current.DisplayName} advancing to hand {current.CurrentHandIndex}");
                SendCompanionTableUpdate(cfg, activePlayers);
                if (current.Hands[current.CurrentHandIndex].IsStand || current.Hands[current.CurrentHandIndex].IsBust)
                {
                    NextTurn(players, cfg);
                }
                else if (current.Hands[current.CurrentHandIndex].Cards.Count < 2)
                {
                    var p = current;
                    Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"SplitDraw:{p.Name}");
                    Task.Run(async () =>
                    {
                        CommandExecutor.SetPreActionSnapshot(Chat.GameLog.CurrentIndex);
                        TargetPlayer(p.Name);
                        SetForcedRecipient(p.Name);
                        try
                        {
                            await CommandExecutor.ExecuteGroup("SplitDraw", p.Name, cfg);
                            SaveSessionIfNeeded(players);

                            if (CurrentPhase == GamePhase.PlayersTurn
                                && p.IsCurrentTurn
                                && p.CurrentHandIndex >= 0
                                && p.CurrentHandIndex < p.Hands.Count)
                            {
                                var hand = p.Hands[p.CurrentHandIndex];
                                if (hand.Cards.Count >= 2
                                    && !hand.IsStand
                                    && !hand.IsBust
                                    && !hand.IsNaturalBlackJack
                                    && !hand.IsCharlie)
                                {
                                    var promptGroup = GetStatePromptGroup(p, cfg);
                                    CommandExecutor.SignalFollowUpPending();
                                    try
                                    {
                                        await CommandExecutor.ExecuteGroup(promptGroup, p.DisplayName, cfg);
                                        SendCompanionTableUpdate(cfg, activePlayers);
                                    }
                                    finally
                                    {
                                        CommandExecutor.ClearFollowUpPending();
                                    }
                                }
                            }
                        }
                        finally
                        {
                            ClearForcedRecipient();
                        }
                    });
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
            window.AddDebugLog($"[NextTurn] Advancing to next player: {next.DisplayName} (finished={IsPlayerFinished(next)}, hasDealt={next.HasInitialHandDealt})");

            if (IsPlayerFinished(next))
            {
                SendCompanionTableUpdate(cfg, activePlayers);
                NextTurn(players, cfg);
            }
            else if (!next.HasInitialHandDealt)
            {
                window.AddDebugLog($"[NextTurn] Player {next.DisplayName} needs initial deal, switching to InitialDeal phase");
                CurrentPhase = GamePhase.InitialDeal;
                next.CurrentHandIndex = 0;
                TargetPlayer(next.Name);
                VariableManager.SetPlayerVariables(next);
                SendCompanionTableUpdate(cfg, activePlayers);
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
                var firstFromBench = ActivateFirstBenchPlayer(players);

                if (firstFromBench != null)
                {
                    if (firstFromBench.Hands.Count == 0)
                        firstFromBench.ResetForNewRound();

                    var newActive = GetActivePlayers(players);
                    foreach (var pl in newActive) pl.IsCurrentTurn = false;
                    firstFromBench.IsCurrentTurn = true;

                    if (!firstFromBench.HasInitialHandDealt)
                    {
                        CurrentPhase = GamePhase.InitialDeal;
                        firstFromBench.CurrentHandIndex = 0;
                        firstFromBench.BankAtRoundStart = firstFromBench.Bank;
                        TargetPlayer(firstFromBench.Name);
                        VariableManager.SetPlayerVariables(firstFromBench);
                        SendCompanionTableUpdate(cfg, newActive);
                    }
                    else if (IsPlayerFinished(firstFromBench))
                    {
                        SendCompanionTableUpdate(cfg, newActive);
                        NextTurn(players, cfg);
                    }
                    else
                    {
                        SwitchTurnTo(firstFromBench, newActive, cfg);
                    }
                    return;
                }
            }

            var allActivePlayers = GetActivePlayers(players);
            var anyPlayerAlive = allActivePlayers.Any(p => p.Hands.Any(h => !h.IsBust));

            if (!anyPlayerAlive)
            {
                window.AddDebugLog("[NextTurn] All players busted, skipping Dealer turn");
                BeginPayoutOutput();
                Task.Run(async () => await EvaluateFinalResults(players, _ctxDealer!, cfg));
            }
            else
            {
                window.AddDebugLog("[NextTurn] All players done, transitioning to DealerTurn");
                CurrentPhase = GamePhase.DealerTurn;
                if (_ctxDealer != null)
                {
                    _ctxDealer.IsCurrentTurn = true;
                    _ctxDealer.CurrentHandIndex = 0;
                    TargetPlayer(_ctxDealer.Name);

                    if (_ctxDealer.Hands.Count > 0)
                    {
                        var (min, max) = _ctxDealer.CalculatePoints(0);
                        int dealerScore = (max.HasValue && max.Value <= 21) ? max.Value : min;
                        VariableManager.SetVariable("dealerpoints", dealerScore.ToString());
                    }
                }
                SendCompanionTableUpdate(cfg, allActivePlayers);
            }
        }
        SaveSessionIfNeeded(players);
    }

    private static void SwitchTurnTo(PlayerState target, List<PlayerState> allActive, Configuration cfg)
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[SwitchTurn] -> {target.DisplayName}, Hands={target.Hands.Count}, Phase={CurrentPhase}");
        foreach (var pl in allActive) pl.IsCurrentTurn = false;
        target.IsCurrentTurn = true;
        target.CurrentHandIndex = 0;
        TargetPlayer(target.Name);
        VariableManager.SetPlayerVariables(target);
        SendCompanionTableUpdate(cfg, allActive);
        if (target.Hands.Count > 0 && target.Hands[target.CurrentHandIndex].Cards.Count >= 2)
        {
            string promptGroup = GetStatePromptGroup(target, cfg);
            window.AddDebugLog($"[SwitchTurn] Triggering prompt group: {promptGroup}");
            Task.Run(async () =>
            {
                await CommandExecutor.ExecuteGroup(promptGroup, target.DisplayName, cfg);
                SendCompanionTableUpdate(cfg, allActive);
            });
        }
    }

    public static async Task ActionHit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        await ExecutePlayerAction(p, "Hit", cfg, players, async () => {
            if (p.CurrentHandIndex >= 0 && p.CurrentHandIndex < p.Hands.Count)
                p.Hands[p.CurrentHandIndex].ActionLog.Add("Hit");
            TargetPlayer(p.Name);
            SetForcedRecipient(p.Name);
            try { await CommandExecutor.ExecuteGroup("Hit", p.Name, cfg); }
            finally { ClearForcedRecipient(); }
            if (p.CurrentHandIndex >= 0 && p.CurrentHandIndex < p.Hands.Count && p.Hands[p.CurrentHandIndex].IsBust)
            {
                var log = p.Hands[p.CurrentHandIndex].ActionLog;
                if (log.Count > 0 && log[^1] == "Hit")
                    log[^1] = "Bust";
            }
        });
        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);
    }

    public static async Task ActionStand(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.CurrentHandIndex >= 0 && p.CurrentHandIndex < p.Hands.Count)
        {
            p.Hands[p.CurrentHandIndex].IsStand = true;
            p.Hands[p.CurrentHandIndex].ActionLog.Add("Stand");
        }

        TargetPlayer(p.Name);
        SetForcedRecipient(p.Name);
        try { await CommandExecutor.ExecuteGroup("Stand", p.Name, cfg); }
        finally { ClearForcedRecipient(); }

        NextTurn(players, cfg);
        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);
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
            hand.ActionLog.Add("DD");
            hand.Bet *= 2;
            TargetPlayer(p.Name);
            SetForcedRecipient(p.Name);
            try { await CommandExecutor.ExecuteGroup("DD", p.Name, cfg); }
            finally { ClearForcedRecipient(); }
            hand.IsStand = true;
        });
        SaveSessionIfNeeded(players);
        CompanionSyncManager.SendPlayerUpdate(cfg, p);
    }

    public static async Task ActionSplit(PlayerState p, Configuration cfg, List<PlayerState> players)
    {
        if (p.Hands.Count >= cfg.MaxHandsPerPlayer) return;

        Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"Split:{p.Name}");
        CommandExecutor.SetPreActionSnapshot(Chat.GameLog.CurrentIndex);

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
        currentHand.ActionLog.Add("Split");

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
        CompanionSyncManager.SendPlayerUpdate(cfg, p);

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Split] {p.DisplayName} successfully split hand", false);
    }

    public static async Task DealerHit(Configuration cfg, List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        CurrentPhase = GamePhase.DealerTurn;
        dealer.IsCurrentTurn = true;
        dealer.CurrentHandIndex = 0;

        if (dealer.Hands.Count > 0)
            dealer.Hands[0].ActionLog.Add("Hit");

        TargetPlayer(dealer.Name);
        SetForcedRecipient(dealer.Name);
        try { await CommandExecutor.ExecuteGroup("DealHit", dealer.Name, cfg); }
        finally { ClearForcedRecipient(); }

        if (dealer.Hands.Count > 0 && dealer.Hands[0].IsBust)
        {
            var log = dealer.Hands[0].ActionLog;
            if (log.Count > 0 && log[^1] == "Hit")
                log[^1] = "Bust";
        }

        SaveSessionIfNeeded(players);
        SendCompanionTableUpdate(cfg, players.Where(x => x.IsActivePlayer));
    }

    public static async Task DealerStand(Configuration cfg, List<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        if (dealer == null) return;

        BeginPayoutOutput();
        dealer.IsCurrentTurn = false;

        if (dealer.Hands.Count > 0)
        {
            dealer.Hands[0].ActionLog.Add("Stand");
            dealer.Hands[0].IsStand = true;
        }

        TargetPlayer(dealer.Name);
        SetForcedRecipient(dealer.Name);
        try { await CommandExecutor.ExecuteGroup("DealStand", dealer.Name, cfg); }
        finally { ClearForcedRecipient(); }
        SaveSessionIfNeeded(players);
        SendCompanionTableUpdate(cfg, players.Where(x => x.IsActivePlayer));
    }

    public static string GetStatePromptGroup(PlayerState p, Configuration cfg)
    {
        if (p.Hands.Count == 0) return string.Empty;
        var hand = p.Hands[p.CurrentHandIndex];

        bool canSplit = false;
        if (cfg.EnableSplit && hand.Cards.Count == 2 && p.Hands.Count < cfg.MaxHandsPerPlayer)
        {
            if (cfg.IdenticalSplitOnly)
                canSplit = hand.Cards[0].Value == hand.Cards[1].Value;
            else
                canSplit = PlayerState.GetCardScoreValue(hand.Cards[0].Value) == PlayerState.GetCardScoreValue(hand.Cards[1].Value);
        }

        bool isSplitHand = p.Hands.Count > 1;
        bool canDD = cfg.EnableDoubleDown && hand.Cards.Count == 2;
        if (isSplitHand && !cfg.AllowDoubleDownAfterSplit)
            canDD = false;

        if (canSplit) return "StateHSDS";
        if (canDD)    return "StateHSD";
        return "StateHS";
    }

    private static bool IsPlayerFinished(PlayerState p)
    {
        return p.Hands.Count > 0 && p.Hands.All(h => h.IsStand || h.IsBust || h.IsNaturalBlackJack || h.IsCharlie);
    }

    private static List<PlayerState> GetActivePlayers(List<PlayerState> players)
    {
        return players.Where(p => p.IsActivePlayer && !p.IsOnHold && !p.IsOnBench && !p.JoinedMidRound).ToList();
    }

    private static List<PlayerState> GetBenchPlayers(List<PlayerState> players)
    {
        return players.Where(p => p.IsActivePlayer && p.IsOnBench).ToList();
    }

    public static bool CanMovePlayerToBench(PlayerState player, List<PlayerState> allPlayers)
    {
        if (player.IsOnBench) return false;

        var activePlayers = GetActivePlayers(allPlayers);
        var benchPlayers = GetBenchPlayers(allPlayers);

        int remainingActive = activePlayers.Count(p => p != player);
        if (remainingActive == 0 && benchPlayers.Count == 0)
            return false;

        return true;
    }

    public static void MovePlayerToBench(PlayerState player, List<PlayerState> allPlayers)
    {
        if (!CanMovePlayerToBench(player, allPlayers)) return;

        player.IsOnBench = true;
        player.BenchedAt = DateTime.UtcNow;
        player.WasOnHoldThisRound = true;
        if (player.IsCurrentTurn)
            player.IsCurrentTurn = false;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] {player.DisplayName} moved to bench.", false);
    }

    public static void MovePlayerFromBench(PlayerState player)
    {
        if (!player.IsOnBench && !player.IsOnHold) return;

        player.IsOnBench = false;
        player.IsOnHold = false;
        player.BenchedAt = DateTime.MinValue;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] {player.DisplayName} returned from bench.", false);
    }

    private static PlayerState? ActivateFirstBenchPlayer(List<PlayerState> players)
    {
        var benchPlayers = GetBenchPlayers(players);
        if (benchPlayers.Count == 0) return null;

        var first = benchPlayers
            .OrderBy(p => p.BenchedAt == DateTime.MinValue ? DateTime.MaxValue : p.BenchedAt)
            .First();

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Bench] Activating first FIFO bench player: {first.DisplayName}.", false);
        MovePlayerFromBench(first);
        return first;
    }

    public static void DeactivatePlayerMidRound(PlayerState player, List<PlayerState> allPlayers, Configuration cfg)
    {
        bool wasCurrentTurn = player.IsCurrentTurn;

        if (player.HasInitialHandDealt)
        {
            foreach (var hand in player.Hands)
                player.Bank += hand.Bet;
        }

        RoundLogManager.RecordPlayerLeave(player.DisplayName);

        player.Hands.Clear();
        player.Hands.Add(new HandState(player.CurrentBet));
        player.IsActivePlayer = false;
        player.IsCurrentTurn = false;
        player.IsOnBench = false;
        player.HasInitialHandDealt = false;
        player.ReadySkip = false;

        Plugin.Instance.GetMainWindow().AddDebugLog($"[Engine] {player.DisplayName} deactivated mid-round (bet refunded: {player.HasInitialHandDealt}).", false);

        if (wasCurrentTurn && CurrentPhase != GamePhase.Waiting && CurrentPhase != GamePhase.Payout)
            NextTurn(allPlayers, cfg);

        SaveSessionIfNeeded(allPlayers);
        CompanionSyncManager.ClearPlayer(cfg, player);
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

    public static void SendCompanionTableUpdate(Configuration cfg, IEnumerable<PlayerState> players)
    {
        PlayerState? dealer;
        lock (_ctxLock) dealer = _ctxDealer;
        var snapshot = new List<PlayerState>();

        if (dealer != null)
            snapshot.Add(dealer);

        snapshot.AddRange(players
            .Where(p => p != null)
            .OrderBy(p => p.IsCurrentTurn ? 1 : 0)
            .ThenBy(p => p.UIID));

        CompanionSyncManager.SendPlayersUpdate(cfg, snapshot);
    }

    private static List<PlayerState> GetPlayersFromContext()
    {
        lock (_ctxLock)
        {
            return _ctxPlayers ?? new List<PlayerState>();
        }
    }
}
