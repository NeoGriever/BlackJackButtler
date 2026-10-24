using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static async Task StartInitialDeal(List<PlayerState> players, Configuration cfg)
    {
        if (!CanStartInitialDeal(players, out var blockedReason))
        {
            Plugin.Instance.GetMainWindow().AddDebugLog(
                $"[RoundStart] Blocked: {blockedReason} | Allow0Bet={AllowZeroBetForSession}"
            );
            return;
        }

        PlayerState? rollbackDealer;
        lock (_ctxLock) rollbackDealer = _ctxDealer;
        if (rollbackDealer != null)
            RoundRollbackManager.Capture(players, rollbackDealer, CurrentPhase);

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
            p.IsOnBench = false;
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

        TargetPlayer(dealer);
        SetForcedRecipient(dealer);
        try
        {
            await CommandExecutor.ExecuteGroup("DealStart", dealer.DisplayName, cfg);

            if (dealer.Hands.Count > 0 && dealer.Hands[0].Cards.Count > 0)
            {
                var (min, max) = dealer.CalculatePoints(0);
                int dealerScore = (max.HasValue && max.Value <= 21) ? max.Value : min;
                VariableManager.SetVariable("dealerpoints", dealerScore.ToString());
                VariableManager.SetVariable("dealerHand", dealer.GetCardsString(0));
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
        TargetPlayer(first);

        CurrentPhase = GamePhase.InitialDeal;
        SendCompanionTableUpdate(cfg, activePlayers);
    }
}
