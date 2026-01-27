using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace BlackJackButtler.Chat;

/// <summary>
/// Handles dice roll results and synchronizes them with the CommandExecutor.
/// This class bridges the gap between chat-based dice rolls and game logic,
/// preventing race conditions by properly canceling and triggering new command chains.
/// </summary>
public static class DiceResultHandler
{
    /// <summary>
    /// Processes a dice roll result, applies the card to the game state,
    /// and manages the command execution flow based on the outcome.
    /// </summary>
    /// <param name="cardValue">The card value derived from the dice roll</param>
    /// <param name="cfg">Configuration containing command groups</param>
    /// <param name="players">List of active players</param>
    /// <param name="dealer">The dealer state</param>
    public static void HandleDiceResult(int cardValue, Configuration cfg, List<PlayerState> players, PlayerState dealer)
    {
        var window = Plugin.Instance.GetMainWindow();
        window.AddDebugLog($"[DiceHandler] Processing card value: {cardValue}");

        // Step 1: Apply the card to the current target
        GameEngine.ApplyCardToCurrentTarget(cardValue, players, dealer);

        // Step 2: Determine the current target and their hand state
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

        // Step 3: Check if we need to cancel the current command group and trigger a new one
        bool shouldCancel = false;
        string? newGroup = null;

        // Check dealer scenarios
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
        // Check player scenarios
        else if (!isDealer)
        {
            // During initial deal
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
                        // No cancel needed, just move to next turn
                        window.AddDebugLog("[DiceHandler] Initial deal complete, moving to next turn");
                        CommandExecutor.NotifyDiceResult();
                        GameEngine.NextTurn(players, cfg);
                        return;
                    }
                }
            }
            // During player's turn
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

        // Step 4: Execute the cancel and trigger logic
        if (shouldCancel && !string.IsNullOrEmpty(newGroup))
        {
            // Cancel the current command group (e.g., the "Hit" chain)
            CommandExecutor.CancelCurrentGroup();
            window.AddDebugLog($"[DiceHandler] Canceled current group, starting new group: {newGroup}");

            // Start the new group using internal execution (no dice wait logic)
            Task.Run(async () =>
            {
                await CommandExecutor.ExecuteInternalGroup(newGroup, target.Name, cfg);

                // After the new group completes, handle the next turn for player scenarios
                if (!isDealer && (newGroup == "PlayerBust" || newGroup == "PlayerBJ" ||
                    newGroup == "PlayerDirtyBJ" || newGroup == "PlayerDDForcedStand"))
                {
                    GameEngine.NextTurn(players, cfg);
                }
                // For dealer scenarios, evaluate final results
                else if (isDealer && (newGroup == "DealerBJ" || newGroup == "DealerBust"))
                {
                    GameEngine.CurrentPhase = GamePhase.Payout;
                    await GameEngine.EvaluateFinalResults(players, dealer, cfg);
                }
            });
        }
        else
        {
            // No special action needed, just notify the executor to continue
            window.AddDebugLog("[DiceHandler] No special action required, notifying executor to continue");
            CommandExecutor.NotifyDiceResult();

            // If player is not done and phase is PlayersTurn, trigger the state prompt
            if (!isDealer && GameEngine.CurrentPhase == GamePhase.PlayersTurn &&
                !hand.IsBust && best < 21 && !hand.IsStand)
            {
                string promptGroup = GameEngine.GetStatePromptGroup(target, cfg);
                Task.Run(async () => await CommandExecutor.ExecuteGroup(promptGroup, target.DisplayName, cfg));
            }
        }
    }
}
