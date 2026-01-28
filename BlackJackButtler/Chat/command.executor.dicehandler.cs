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
                        GameEngine.NextTurn(players, cfg);
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
            CommandExecutor.CancelCurrentGroup();
            window.AddDebugLog($"[DiceHandler] Canceled current group, starting new group: {newGroup}");

            Task.Run(async () =>
            {
                await CommandExecutor.ExecuteInternalGroup(newGroup, target.Name, cfg);

                if (!isDealer && (newGroup == "PlayerBust" || newGroup == "PlayerBJ" ||
                    newGroup == "PlayerDirtyBJ" || newGroup == "PlayerDDForcedStand"))
                {
                    GameEngine.NextTurn(players, cfg);
                }
                else if (isDealer && (newGroup == "DealerBJ" || newGroup == "DealerBust"))
                {
                    GameEngine.CurrentPhase = GamePhase.Payout;
                    await GameEngine.EvaluateFinalResults(players, dealer, cfg);
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
