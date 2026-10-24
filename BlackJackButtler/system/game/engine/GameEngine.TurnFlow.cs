using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
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
                    GameActionQueueManager.RunContinuation(
                        $"SplitDraw:{p.Name}",
                        async () =>
                        {
                            CommandExecutor.SetPreActionSnapshot(Chat.GameLog.CurrentIndex);
                            TargetPlayer(p);
                            SetForcedRecipient(p);
                            try
                            {
                                await CommandExecutor.ExecuteGroup("SplitDraw", p.DisplayName, cfg);
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
                        }
                    );
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
                TargetPlayer(next);
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
                        TargetPlayer(firstFromBench);
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
                GameActionQueueManager.RunContinuation(
                    "AllPlayersBustedPayout",
                    () => EvaluateFinalResults(
                        players,
                        _ctxDealer!,
                        cfg
                    )
                );
            }
            else
            {
                window.AddDebugLog("[NextTurn] All players done, transitioning to DealerTurn");
                CurrentPhase = GamePhase.DealerTurn;
                if (_ctxDealer != null)
                {
                    _ctxDealer.IsCurrentTurn = true;
                    _ctxDealer.CurrentHandIndex = 0;
                    TargetPlayer(_ctxDealer);

                    if (_ctxDealer.Hands.Count > 0)
                    {
                        var (min, max) = _ctxDealer.CalculatePoints(0);
                        int dealerScore = (max.HasValue && max.Value <= 21) ? max.Value : min;
                        VariableManager.SetVariable("dealerpoints", dealerScore.ToString());
                        VariableManager.SetVariable("dealerHand", _ctxDealer.GetCardsString(0));
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
        TargetPlayer(target);
        VariableManager.SetPlayerVariables(target);
        SendCompanionTableUpdate(cfg, allActive);
        if (target.Hands.Count > 0 && target.Hands[target.CurrentHandIndex].Cards.Count >= 2)
        {
            string promptGroup = GetStatePromptGroup(target, cfg);
            window.AddDebugLog($"[SwitchTurn] Triggering prompt group: {promptGroup}");
            GameActionQueueManager.RunContinuation(
                $"TurnPrompt:{target.Name}",
                async () =>
                {
                    await CommandExecutor.ExecuteGroup(promptGroup, target.DisplayName, cfg);
                    SendCompanionTableUpdate(cfg, allActive);
                }
            );
        }
    }
}
