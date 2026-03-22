using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlackJackButtler.Chat;

public static class DiceResultHandler
{
    public static void HandleDiceResult(int cardValue, Configuration cfg, List<PlayerState> players, PlayerState dealer)
    {
        var window = Plugin.Instance.GetMainWindow();

        if (!CommandExecutor.IsRunning || CommandExecutor.IsCancelling)
        {
            window.AddDebugLog("[DiceHandler] Ignored - executor not running or cancelled");
            return;
        }

        window.AddDebugLog($"[DiceHandler] Processing card value: {cardValue}");

        GameEngine.ApplyCardToCurrentTarget(cardValue, players, dealer);

        var targetName = GameEngine.GetCurrentTargetName();
        PlayerState? target =
            (!string.IsNullOrWhiteSpace(targetName) && dealer.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
                ? dealer
                : players.FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

        target ??= players.FirstOrDefault(p => p.IsCurrentTurn) ?? dealer;

        if (target.Hands.Count == 0)
        {
            window.AddDebugLog("[DiceHandler] No hands found for target");
            CommandExecutor.NotifyDiceResult();
            return;
        }

        if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
            target.CurrentHandIndex = 0;

        var hand = target.Hands[target.CurrentHandIndex];
        var (min, max) = target.CalculatePoints(target.CurrentHandIndex);
        var best = (max.HasValue && max.Value <= 21) ? max.Value : min;

        bool isDealer = ReferenceEquals(target, dealer) || target.Name.Equals(dealer.Name, StringComparison.OrdinalIgnoreCase);

        bool shouldCancel = false;
        string? newGroup = null;

        if (isDealer && GameEngine.CurrentPhase == GamePhase.DealerTurn)
        {
            if (best == 21)
            {
                shouldCancel = true;
                newGroup = "DealerBJ";
                window.AddDebugLog("[DiceHandler] Dealer hit 21 - triggering DealerBJ");
            }
            else if (best > 21 || hand.IsBust)
            {
                shouldCancel = true;
                newGroup = "DealerBust";
                window.AddDebugLog("[DiceHandler] Dealer bust - triggering DealerBust");
            }
        }
        else if (!isDealer)
        {
            if (GameEngine.CurrentPhase == GamePhase.InitialDeal)
            {
                if (hand.Cards.Count == 2)
                {
                    target.HasInitialHandDealt = true;

                    if (best == 21)
                    {
                        hand.IsStand = true;
                        hand.IsNaturalBlackJack = true;
                        shouldCancel = true;
                        newGroup = "PlayerBJ";
                        window.AddDebugLog("[DiceHandler] Player natural blackjack - triggering PlayerBJ");
                    }
                    else
                    {
                        window.AddDebugLog("[DiceHandler] Initial deal complete, moving to next turn");
                        CommandExecutor.NotifyDiceResult();
                        CommandExecutor.SignalFollowUpPending();
                        Task.Run(async () =>
                        {
                            try
                            {
                                window.AddDebugLog($"[DiceHandler-FollowUp] Task started, IsRunning={CommandExecutor.IsRunning}");
                                if (CommandExecutor.IsRunning)
                                {
                                    var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                                    Action handler = () => tcs.TrySetResult(true);
                                    CommandExecutor.OnGroupCompleted += handler;
                                    window.AddDebugLog("[DiceHandler-FollowUp] Subscribed to OnGroupCompleted, awaiting...");
                                    try   { await tcs.Task; }
                                    finally { CommandExecutor.OnGroupCompleted -= handler; }
                                    window.AddDebugLog("[DiceHandler-FollowUp] OnGroupCompleted fired, calling NextTurn");
                                }
                                else
                                {
                                    window.AddDebugLog("[DiceHandler-FollowUp] Skipped wait (executor already finished), calling NextTurn");
                                }
                                GameEngine.NextTurn(players, cfg);
                                window.AddDebugLog("[DiceHandler-FollowUp] NextTurn completed");
                            }
                            catch (Exception ex)
                            {
                                window.AddDebugLog($"[DiceHandler-FollowUp] EXCEPTION: {ex.Message}");
                            }
                            finally
                            {
                                window.AddDebugLog("[DiceHandler-FollowUp] ClearFollowUpPending");
                                CommandExecutor.ClearFollowUpPending();
                            }
                        });
                        return;
                    }
                }
            }
            else if (GameEngine.CurrentPhase == GamePhase.PlayersTurn)
            {
                if (best > 21)
                {
                    hand.IsBust = true;
                    hand.IsStand = true;
                    shouldCancel = true;
                    newGroup = "PlayerBust";
                    window.AddDebugLog($"[DiceHandler] Player bust ({best} points) - canceling current chain and triggering PlayerBust");
                }
                else if (cfg.EnableCharlie && hand.Cards.Count >= cfg.CharlieCardCount && !hand.IsBust)
                {
                    hand.IsCharlie = true;
                    hand.IsStand = true;
                    hand.ActionLog.Add("Charlie");
                    shouldCancel = true;
                    newGroup = "PlayerCharlie";
                    window.AddDebugLog($"[DiceHandler] Player Charlie ({hand.Cards.Count} cards) - triggering PlayerCharlie");
                }
                else if (best == 21)
                {
                    hand.IsStand = true;
                    shouldCancel = true;
                    newGroup = (hand.Cards.Count == 2 && hand.IsNaturalBlackJack) ? "PlayerBJ" : "PlayerDirtyBJ";
                    window.AddDebugLog($"[DiceHandler] Player hit 21 - triggering {newGroup}");
                }
                else if (hand.IsDoubleDown)
                {
                    hand.IsStand = true;
                    shouldCancel = true;
                    newGroup = "PlayerDDForcedStand";
                    window.AddDebugLog("[DiceHandler] Double down final card - triggering forced stand");
                }
            }
        }

        if (shouldCancel && !string.IsNullOrEmpty(newGroup))
        {
            window.AddDebugLog($"[DiceHandler] Completing current group before starting: {newGroup}");
            CommandExecutor.NotifyDiceResult();
            CommandExecutor.SignalFollowUpPending();

            Task.Run(async () =>
            {
                try
                {
                    window.AddDebugLog($"[DiceHandler-Cancel] Task started for {newGroup}, IsRunning={CommandExecutor.IsRunning}");
                    if (CommandExecutor.IsRunning)
                    {
                        var tcs = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);
                        Action handler = () => tcs.TrySetResult(true);
                        CommandExecutor.OnGroupCompleted += handler;
                        window.AddDebugLog("[DiceHandler-Cancel] Subscribed to OnGroupCompleted, awaiting...");
                        try   { await tcs.Task; }
                        finally { CommandExecutor.OnGroupCompleted -= handler; }
                        window.AddDebugLog("[DiceHandler-Cancel] OnGroupCompleted fired");
                    }
                    else
                    {
                        window.AddDebugLog("[DiceHandler-Cancel] Skipped wait (executor already finished)");
                    }

                    window.AddDebugLog($"[DiceHandler-Cancel] Executing internal group: {newGroup}");
                    await CommandExecutor.ExecuteInternalGroup(newGroup, target.Name, cfg);
                    window.AddDebugLog($"[DiceHandler-Cancel] Internal group {newGroup} completed");

                    if (!isDealer && (newGroup == "PlayerBust" || newGroup == "PlayerBJ" ||
                        newGroup == "PlayerDirtyBJ" || newGroup == "PlayerDDForcedStand" || newGroup == "PlayerCharlie"))
                    {
                        window.AddDebugLog($"[DiceHandler-Cancel] Calling NextTurn after {newGroup}");
                        GameEngine.NextTurn(players, cfg);
                        window.AddDebugLog("[DiceHandler-Cancel] NextTurn completed");
                    }
                    else if (isDealer && (newGroup == "DealerBJ" || newGroup == "DealerBust"))
                    {
                        window.AddDebugLog($"[DiceHandler-Cancel] Dealer {newGroup}, transitioning to Payout");
                        GameEngine.CurrentPhase = GamePhase.Payout;
                        await GameEngine.EvaluateFinalResults(players, dealer, cfg);
                        window.AddDebugLog("[DiceHandler-Cancel] EvaluateFinalResults completed");
                    }
                }
                catch (Exception ex)
                {
                    window.AddDebugLog($"[DiceHandler-Cancel] EXCEPTION: {ex.Message}");
                }
                finally
                {
                    window.AddDebugLog("[DiceHandler-Cancel] ClearFollowUpPending");
                    CommandExecutor.ClearFollowUpPending();
                }
            });
        }
        else
        {
            window.AddDebugLog("[DiceHandler] No special action required, notifying executor to continue");
            CommandExecutor.NotifyDiceResult();

            if (!isDealer && GameEngine.CurrentPhase == GamePhase.PlayersTurn &&
                !hand.IsBust && best < 21 && !hand.IsStand)
            {
                string promptGroup = GameEngine.GetStatePromptGroup(target, cfg);
                Task.Run(async () => await CommandExecutor.ExecuteGroup(promptGroup, target.DisplayName, cfg));
            }
        }
    }
}
