using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using BlackJackButtler.Chat;

namespace BlackJackButtler;

public static partial class GameEngine
{
    public static void SetRuntimeContext(List<PlayerState> players, PlayerState dealer)
    {
        lock (_ctxLock)
        {
            _ctxPlayers = players;
            _ctxDealer = dealer;
        }
    }

    private static string ResolveRecipientNameForCard()
    {
        lock (_ctxLock)
        {
            if (!string.IsNullOrWhiteSpace(_forcedRecipientName))
                return _forcedRecipientName;
        }

        var real = Plugin.TargetManager.Target?.Name.TextValue ?? string.Empty;
        if (!string.IsNullOrWhiteSpace(real))
            return real;

        return _virtualTargetName;
    }

    public static void ApplyCardToCurrentTarget(int cardValue, List<PlayerState> players, PlayerState dealer)
    {
        if (cardValue <= 0) return;
        var deckCard = DeckManager.PullCard(cardValue);
        var recipientName = ResolveRecipientNameForCard();

        PlayerState? target = null;

        if (!string.IsNullOrWhiteSpace(recipientName) &&
            dealer.Name.Equals(recipientName, StringComparison.OrdinalIgnoreCase))
        {
            target = dealer;
        }
        else if (!string.IsNullOrWhiteSpace(recipientName))
        {
            target = players.FirstOrDefault(p =>
                p.Name.Equals(recipientName, StringComparison.OrdinalIgnoreCase));
        }

        target ??= players.FirstOrDefault(p => p.IsCurrentTurn) ?? dealer;

        if (target.Hands.Count == 0)
            target.Hands.Add(new HandState(target.CurrentBet));

        if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
            target.CurrentHandIndex = 0;

        var hand = target.Hands[target.CurrentHandIndex];

        hand.Cards.Add(deckCard);

        var (min, max) = target.CalculatePoints(target.CurrentHandIndex);
        hand.IsBust = min > 21 && (!max.HasValue || max.Value > 21);

        if (hand.Cards.Count == 2)
        {
            hand.IsNaturalBlackJack =
                (max.HasValue && max.Value == 21) ||
                (!max.HasValue && min == 21);

            if (hand.IsNaturalBlackJack)
                hand.IsStand = true;
        }
    }

    public static bool TryApplyCardToCurrentTargetFromRuntime(int cardValue)
    {
        lock (_ctxLock)
        {
            if (_ctxDealer == null || _ctxPlayers == null) return false;
            ApplyCardToCurrentTarget(cardValue, _ctxPlayers, _ctxDealer);
            return true;
        }
    }

    public static bool TryGetBestScoreForCurrentTarget(out int score)
    {
        score = 0;

        lock (_ctxLock)
        {
            if (_ctxDealer == null || _ctxPlayers == null) return false;

            var name = GetCurrentTargetName();

            PlayerState? target =
                (!string.IsNullOrWhiteSpace(name) && _ctxDealer.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                    ? _ctxDealer
                    : _ctxPlayers.FirstOrDefault(p => p.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

            target ??= _ctxPlayers.FirstOrDefault(p => p.IsCurrentTurn) ?? _ctxDealer;

            if (target.Hands.Count == 0) return false;

            if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
                target.CurrentHandIndex = 0;

            var (min, max) = target.CalculatePoints(target.CurrentHandIndex);
            score = (max.HasValue && max.Value <= 21) ? max.Value : min;
            return true;
        }
    }

    public static int MapDice13ToCardValue(int rolled)
    {
        return rolled;
    }

    private static bool IsPlayerDone(PlayerState p)
    {
        if (!p.IsActivePlayer) return true;
        if (!p.HasInitialHandDealt) return false;
        if (p.Hands.Count == 0) return false;

        return p.Hands.All(IsHandDone);
    }

    public static void HandlePostCardEvents(Configuration cfg, List<PlayerState> players, PlayerState dealer)
    {
        var targetName = GetCurrentTargetName();
        PlayerState? target =
        (!string.IsNullOrWhiteSpace(targetName) && dealer.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase))
        ? dealer
        : players.FirstOrDefault(p => p.Name.Equals(targetName, StringComparison.OrdinalIgnoreCase));

        target ??= players.FirstOrDefault(p => p.IsCurrentTurn) ?? dealer;

        if (target.Hands.Count == 0) return;

        if (target.CurrentHandIndex < 0 || target.CurrentHandIndex >= target.Hands.Count)
        target.CurrentHandIndex = 0;

        var hand = target.Hands[target.CurrentHandIndex];

        var (min, max) = target.CalculatePoints(target.CurrentHandIndex);
        var best = (max.HasValue && max.Value <= 21) ? max.Value : min;

        bool isDealer = ReferenceEquals(target, dealer) || target.Name.Equals(dealer.Name, StringComparison.OrdinalIgnoreCase);

        if (isDealer && CurrentPhase == GamePhase.DealerTurn)
        {
            if (best == 21)
            {
                Task.Run(async () => {
                    await CommandExecutor.ExecuteGroup("DealerBJ", dealer.Name, cfg);
                    await EvaluateFinalResults(players, dealer, cfg);
                });
                CurrentPhase = GamePhase.Payout;
                return;
            }

            if (best > 21 || hand.IsBust)
            {
                Task.Run(async () => {
                    await CommandExecutor.ExecuteGroup("DealerBust", dealer.Name, cfg);
                    await EvaluateFinalResults(players, dealer, cfg);
                });
                CurrentPhase = GamePhase.Payout;
                return;
            }
            return;
        }

        if (!isDealer)
        {
            if (CurrentPhase == GamePhase.InitialDeal)
            {
                if (hand.Cards.Count == 2)
                {
                    target.HasInitialHandDealt = true;

                    if (best == 21)
                    {
                        hand.IsStand = true;
                        hand.IsNaturalBlackJack = true;
                        Task.Run(async () => {
                            await CommandExecutor.ExecuteGroup("PlayerBJ", target.Name, cfg);
                            NextTurn(players, cfg);
                        });
                    }
                    else
                    {
                        NextTurn(players, cfg);
                    }
                }
                return;
            }

            if (CurrentPhase == GamePhase.PlayersTurn)
            {
                if (best > 21) {
                    hand.IsBust = true;
                    hand.IsStand = true;
                    Task.Run(async () => {
                        await CommandExecutor.ExecuteGroup("PlayerBust", target.Name, cfg);
                        NextTurn(players, cfg);
                    });
                    return;
                }
                if (best == 21) {
                    hand.IsStand = true;
                    string group = (hand.Cards.Count == 2 && hand.IsNaturalBlackJack) ? "PlayerBJ" : "PlayerDirtyBJ";
                    Task.Run(async () => {
                        await CommandExecutor.ExecuteGroup(group, target.Name, cfg);
                        NextTurn(players, cfg);
                    });
                    return;
                }
                if (hand.IsDoubleDown) {
                    hand.IsStand = true;
                    Task.Run(async () => {
                        await CommandExecutor.ExecuteGroup("PlayerDDForcedStand", target.Name, cfg);
                        NextTurn(players, cfg);
                    });
                    return;
                }
                if (!hand.IsBust && best < 21 && !hand.IsStand)
                {
                    string promptGroup = GetStatePromptGroup(target, cfg);
                    Task.Run(async () => await CommandExecutor.ExecuteGroup(promptGroup, target.DisplayName, cfg));
                }
            }
        }
    }

    private static async Task ExecutePlayerAction(PlayerState p, string actionName, Configuration cfg, List<PlayerState> players, Func<Task> logic)
    {
        Chat.GameLog.PushSnapshot(players, _ctxDealer!, CurrentPhase, $"{actionName}:{p.Name}");

        long cost = 0;
        if (actionName == "Initial") cost = p.CurrentBet;
        if (actionName == "DD") cost = p.CurrentBet;
        if (actionName == "Split") cost = p.CurrentBet;

        p.Bank -= cost;

        if (p.Bank < 0)
        {
            ChatCommandRouter.Send($"/p [BJB] <t>, please transfer {Math.Abs(p.Bank)} gil to continue.", cfg);
            return;
        }

        await logic();
    }

    public static async Task EvaluateFinalResults(List<PlayerState> players, PlayerState dealer, Configuration cfg)
    {
        CurrentPhase = GamePhase.Payout;

        int dealerScore = dealer.GetBestScore(0);
        bool dealerBust = dealer.Hands.Count > 0 && dealer.Hands[0].IsBust;

        if (cfg.SmallResult)
        {
            var winList = new List<string>();
            var pushList = new List<string>();
            var lossList = new List<string>();
            var bustList = new List<string>();
            var activeList = players.Where(x => x.IsActivePlayer).ToList();

            foreach (var p in players.Where(x => x.IsActivePlayer))
            {
                p.IsCurrentTurn = false;
                string shortName = p.GetShortName(activeList); 

                foreach (var hand in p.Hands)
                {
                    int pScore = p.GetBestScore(p.Hands.IndexOf(hand));

                    if (hand.IsBust)
                    {
                        bustList.Add(shortName);
                    }
                    else if (dealerBust || pScore > dealerScore)
                    {
                        winList.Add(shortName);
                        float mult = cfg.MultiplierNormalWin;
                        if (hand.IsNaturalBlackJack) mult = cfg.MultiplierBlackjackWin;
                        else if (pScore == 21) mult = cfg.MultiplierDirtyBlackjackWin;

                        long winAmount = (long)(hand.Bet * mult);
                        p.Bank += (hand.Bet + winAmount);
                    }
                    else if (pScore == dealerScore)
                    {
                        pushList.Add(shortName);
                        p.Bank += hand.Bet;
                    }
                    else
                    {
                        lossList.Add(shortName);
                    }
                }
            }

            VariableManager.SetVariable("winners", FormatResultCategory(winList.Distinct().ToList(), "Winner", "Winners"));
            VariableManager.SetVariable("pushed", FormatResultCategory(pushList.Distinct().ToList(), "Pushed", "Pushed"));
            VariableManager.SetVariable("loosers", FormatResultCategory(lossList.Distinct().ToList(), "Loosed", "Loosed")); // "Loosed" wie gewünscht
            VariableManager.SetVariable("busted", FormatResultCategory(bustList.Distinct().ToList(), "Busted", "Busted"));

            var parts = new List<string>();
            string GetV(string n) => VariableManager.Variables.FirstOrDefault(v => v.Name == n)?.Value ?? "";

            if (winList.Any())  parts.Add(GetV("winners"));
            if (pushList.Any()) parts.Add(GetV("pushed"));
            if (lossList.Any()) parts.Add(GetV("loosers"));
            if (bustList.Any()) parts.Add(GetV("busted"));

            VariableManager.SetVariable("results", string.Join(" | ", parts));

            await CommandExecutor.ExecuteGroup("ResultSmall", dealer.Name, cfg);
        }
        else
        {
            foreach (var p in players.Where(x => x.IsActivePlayer))
            {
                p.IsCurrentTurn = false;

                foreach (var hand in p.Hands)
                {
                    int pScore = p.GetBestScore(p.Hands.IndexOf(hand));

                    if (hand.IsBust)
                    {
                        await CommandExecutor.ExecuteGroup("ResultPlayerBusted", p.DisplayName, cfg);
                    }
                    else if (dealerBust || pScore > dealerScore)
                    {
                        float mult = cfg.MultiplierNormalWin;
                        if (hand.IsNaturalBlackJack) mult = cfg.MultiplierBlackjackWin;
                        else if (pScore == 21) mult = cfg.MultiplierDirtyBlackjackWin;

                        long winAmount = (long)(hand.Bet * mult);
                        p.Bank += (hand.Bet + winAmount);
                        await CommandExecutor.ExecuteGroup("ResultPlayerWin", p.DisplayName, cfg);
                    }
                    else if (pScore == dealerScore)
                    {
                        p.Bank += hand.Bet;
                        await CommandExecutor.ExecuteGroup("ResultPlayerPush", p.DisplayName, cfg);
                    }
                    else
                    {
                        await CommandExecutor.ExecuteGroup("ResultPlayerLost", p.DisplayName, cfg);
                    }
                }
            }
        }
    }

    private static string FormatResultCategory(List<string> names, string singular, string plural)
    {
        if (!names.Any()) return "";
        string label = names.Count == 1 ? singular : plural;
        return $"{label}: {string.Join(", ", names)}";
    }
}
